using HireYar.Application.Common;
using HireYar.Domain.Support;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Application.Support;

// administrator is supplied only by the two controllers' fixed authorization policies, never by request data.
public sealed class TicketService(IApplicationDbContext db, ICurrentUser user, TimeProvider time, ITicketAttachmentStorage storage)
{
    private const int MaxMessages = 200;
    private const long MaxTicketBytes = 100L * 1024 * 1024, MaxOrganizationBytes = 500L * 1024 * 1024;
    private IQueryable<SupportTicket> Scope(bool administrator) => administrator
        ? db.SupportTickets : db.SupportTickets.Where(x => x.OrganizationId == user.OrganizationId && (user.CanAccess("tickets", true) || x.CreatedByUserId == user.UserId));

    private IQueryable<TicketDto> Dtos(IQueryable<SupportTicket> query) =>
        from t in query
        join o in db.Organizations on t.OrganizationId equals o.Id
        select new TicketDto(t.Id, t.OrganizationId, o.Name, t.CreatedByUserId, t.Subject, t.Status, t.Priority,
            t.Revision, t.CreatedAtUtc, t.UpdatedAtUtc);

    public async Task<TicketPage<TicketDto>> ListAsync(TicketQuery query, bool administrator, CancellationToken ct)
    {
        ValidatePage(query);
        var tickets = Scope(administrator).AsNoTracking();
        if (query.Status is { } status) tickets = tickets.Where(x => x.Status == status);
        if (query.Priority is { } priority) tickets = tickets.Where(x => x.Priority == priority);
        var total = await tickets.CountAsync(ct);
        var items = await Dtos(tickets.OrderByDescending(x => x.UpdatedAtUtc).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)).ToArrayAsync(ct);
        return new(items, query.Page, query.PageSize, total);
    }
    public async Task<TicketDto> GetAsync(Guid id, bool administrator, CancellationToken ct) =>
        await Dtos(Scope(administrator).AsNoTracking().Where(x => x.Id == id)).SingleOrDefaultAsync(ct) ?? throw AppException.NotFound();

    public async Task<TicketPage<TicketMessageDto>> MessagesAsync(Guid id, TicketPageQuery query, bool administrator, CancellationToken ct)
    {
        ValidatePage(query);
        var ticket = await GetAsync(id, administrator, ct);
        var messages = db.TicketMessages.AsNoTracking().Where(x => x.OrganizationId == ticket.OrganizationId && x.TicketId == id);
        var total = await messages.CountAsync(ct);
        var page = await messages.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize).Select(x => new { x.Id, x.AuthorUserId, x.IsStaff, x.Body, x.CreatedAtUtc }).ToArrayAsync(ct);
        var ids = page.Select(x => x.Id).ToArray();
        // Project only attachment metadata; object keys are never returned in API responses.
        var files = await db.TicketAttachments.AsNoTracking()
            .Where(x => x.OrganizationId == ticket.OrganizationId && x.TicketId == id && ids.Contains(x.MessageId))
            .Select(x => new { x.MessageId, Metadata = new TicketAttachmentDto(x.Id, x.FileName, x.ContentType, x.Length) }).ToArrayAsync(ct);
        return new(page.Select(x => new TicketMessageDto(x.Id, x.AuthorUserId, x.IsStaff, x.Body, x.CreatedAtUtc,
            files.Where(f => f.MessageId == x.Id).Select(f => f.Metadata).ToArray())).ToArray(), query.Page, query.PageSize, total);
    }
    public async Task<TicketDto> CreateAsync(string subject, string body, TicketPriority priority, IReadOnlyList<TicketUpload> files, CancellationToken ct)
    {
        subject = Text(subject, 200); body = Text(body, 8000);
        if (!Enum.IsDefined(priority)) throw AppException.Invalid();
        TicketFileRules.Validate(files);
        var organizationId = user.OrganizationId;
        var stored = await UploadAsync(organizationId, files, ct);
        try { return await db.WithOrganizationLockAsync(organizationId, async () =>
        {
            if (await db.SupportTickets.CountAsync(x => x.OrganizationId == organizationId, ct) >= 1000 ||
                await db.SupportTickets.CountAsync(x => x.OrganizationId == organizationId && x.Status != TicketStatus.Closed, ct) >= 20)
                throw new AppException(409, "ticket_limit", "Organization ticket limit reached.");
            var now = time.GetUtcNow();
            var ticket = new SupportTicket { OrganizationId = organizationId, CreatedByUserId = user.UserId,
                Subject = subject, Priority = priority, CreatedAtUtc = now, UpdatedAtUtc = now };
            await CheckQuotaAsync(ticket, files, ct);
            db.SupportTickets.Add(ticket);
            AddMessage(ticket, body, files, stored, false, now);
            await db.SaveChangesAsync(ct);
            return await GetAsync(ticket.Id, false, ct);
        }, ct); } catch { await CleanupAsync(stored, checkReferences: true); throw; }
    }
    public async Task<TicketDto> ReplyAsync(Guid id, string body, IReadOnlyList<TicketUpload> files, bool administrator, CancellationToken ct)
    {
        var visible = await GetAsync(id, administrator, ct);
        body = Text(body, 8000); TicketFileRules.Validate(files);
        if (visible.Status == TicketStatus.Closed) throw new AppException(409, "ticket_closed", "Reopen the ticket before replying.");
        var stored = await UploadAsync(visible.OrganizationId, files, ct);
        try {
        return await db.WithOrganizationLockAsync(visible.OrganizationId, async () =>
        {
            var ticket = await Scope(administrator).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
            if (ticket.Status == TicketStatus.Closed) throw new AppException(409, "ticket_closed", "Reopen the ticket before replying.");
            if (await db.TicketMessages.CountAsync(x => x.OrganizationId == ticket.OrganizationId && x.TicketId == id, ct) >= MaxMessages)
                throw new AppException(409, "ticket_message_limit", "Ticket message limit reached.");
            await CheckQuotaAsync(ticket, files, ct);
            var now = time.GetUtcNow();
            AddMessage(ticket, body, files, stored, administrator, now);
            ticket.Status = administrator ? TicketStatus.Answered : TicketStatus.Open;
            ticket.UpdatedAtUtc = now; ticket.Revision++;
            await db.SaveChangesAsync(ct);
            return await GetAsync(id, administrator, ct);
        }, ct); } catch { await CleanupAsync(stored, checkReferences: true); throw; }
    }
    public async Task<TicketDto> SetStatusAsync(Guid id, TicketStatusRequest request, bool administrator, CancellationToken ct)
    {
        var visible = await GetAsync(id, administrator, ct);
        if (!Enum.IsDefined(request.Status) || request.Revision < 1) throw AppException.Invalid();
        if (!administrator && request.Status == TicketStatus.Answered) throw new AppException(403, "forbidden", "Only support may mark a ticket answered.");
        return await db.WithOrganizationLockAsync(visible.OrganizationId, async () =>
        {
            var ticket = await Scope(administrator).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
            if (ticket.Revision != request.Revision) throw new AppException(409, "concurrency_conflict", "Reload the ticket before changing its status.");
            if (ticket.Status == request.Status) return await GetAsync(id, administrator, ct);
            if (ticket.Status == TicketStatus.Closed && request.Status != TicketStatus.Closed &&
                await db.SupportTickets.CountAsync(x => x.OrganizationId == ticket.OrganizationId && x.Status != TicketStatus.Closed, ct) >= 20)
                throw new AppException(409, "ticket_limit", "Organization open ticket limit reached.");
            ticket.Status = request.Status; ticket.Revision++; ticket.UpdatedAtUtc = time.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return await GetAsync(id, administrator, ct);
        }, ct);
    }
    public async Task<TicketFile> DownloadAsync(Guid id, Guid attachmentId, bool administrator, CancellationToken ct)
    {
        var ticket = await GetAsync(id, administrator, ct);
        var file = await db.TicketAttachments.AsNoTracking()
            .Where(x => x.Id == attachmentId && x.OrganizationId == ticket.OrganizationId && x.TicketId == id)
            .Select(x => new { x.FileName, x.StorageKey, x.Length }).SingleOrDefaultAsync(ct) ?? throw AppException.NotFound();
        return new(file.FileName, await storage.OpenAsync(file.StorageKey, file.Length, ct));
    }
    private async Task CheckQuotaAsync(SupportTicket ticket, IReadOnlyList<TicketUpload> files, CancellationToken ct)
    {
        var bytes = files.Sum(x => (long)x.Content.Length);
        if (bytes == 0) return;
        var attachments = db.TicketAttachments.Where(x => x.OrganizationId == ticket.OrganizationId);
        var ticketBytes = await attachments.Where(x => x.TicketId == ticket.Id).SumAsync(x => (long?)x.Length, ct) ?? 0;
        var organizationBytes = await attachments.SumAsync(x => (long?)x.Length, ct) ?? 0;
        if (ticketBytes + bytes > MaxTicketBytes || organizationBytes + bytes > MaxOrganizationBytes)
            throw new AppException(409, "ticket_storage_limit", "Attachment storage limit reached.");
    }
    private void AddMessage(SupportTicket ticket, string body, IReadOnlyList<TicketUpload> files, IReadOnlyList<string> stored, bool staff, DateTimeOffset now)
    {
        var message = new TicketMessage { OrganizationId = ticket.OrganizationId, TicketId = ticket.Id,
            AuthorUserId = user.UserId, IsStaff = staff, Body = body, CreatedAtUtc = now };
        db.TicketMessages.Add(message);
        for (var i = 0; i < files.Count; i++) {
        var file = files[i];
        db.TicketAttachments.Add(new TicketAttachment {
            OrganizationId = ticket.OrganizationId, TicketId = ticket.Id, MessageId = message.Id,
            FileName = file.FileName, ContentType = file.ContentType.ToLowerInvariant(), Length = file.Content.Length, StorageKey = stored[i] });
        }
    }
    private async Task<IReadOnlyList<string>> UploadAsync(Guid organizationId, IReadOnlyList<TicketUpload> files, CancellationToken ct)
    {
        var keys = new List<string>();
        try
        {
            foreach (var file in files)
            {
                var key = $"tickets/{organizationId:N}/{Guid.NewGuid():N}";
                keys.Add(key); // Also clean up a PUT whose success response was lost.
                await storage.SaveAsync(key, file.Content, ct);
            }
            return keys;
        }
        catch { await CleanupAsync(keys); throw; }
    }
    private async Task CleanupAsync(IReadOnlyList<string> keys, bool checkReferences = false)
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        foreach (var key in keys)
        {
            if (checkReferences)
            {
                try
                {
                    // A lost commit response must not delete an object whose metadata was committed.
                    if (await db.TicketAttachments.AsNoTracking().AnyAsync(x => x.StorageKey == key, cleanup.Token)) continue;
                }
                catch (Exception) { return; } // Prefer a private orphan to breaking a possibly committed attachment.
            }
            try { await storage.DeleteAsync(key, cleanup.Token); }
            catch (Exception) { /* Storage outage can leave an orphan; no uncommitted object has a download endpoint. */ }
        }
    }
    private static string Text(string? value, int max)
    {
        value = value?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length > max || value.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t'))
            throw AppException.Invalid("ticket_text");
        return value;
    }
    private static void ValidatePage(TicketPageQuery query)
    {
        if (query.Page is < 1 or > 100000 || query.PageSize is < 1 or > 50 ||
            query is TicketQuery { Status: { } s } && !Enum.IsDefined(s) || query is TicketQuery { Priority: { } p } && !Enum.IsDefined(p)) throw AppException.Invalid();
    }
}

