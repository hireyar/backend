using HireYar.Application.Common;
using HireYar.Domain.Interviews;
using HireYar.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HireYar.Application.Notifications;

public sealed class NotificationService(IApplicationDbContext db, ICurrentUser user, TimeProvider time,
    INotificationRecipients recipients, INotificationPublisher publisher, ILogger<NotificationService> logger)
{
    // Joins the caller's transaction/save; publishing happens only after commit.
    public async Task<List<Notification>> AddInterviewAsync(InterviewSession session, string candidateName, bool completed, CancellationToken ct)
    {
        var added = new List<Notification>();
        foreach (var recipient in await recipients.ActiveMembersAsync(session.OrganizationId, ct))
        {
            var notification = await AddAsync(session.OrganizationId, recipient,
                completed ? "InterviewCompleted" : "InterviewStarted", session.Id.ToString("N"),
                completed ? $"مصاحبه {candidateName} پایان یافت" : $"مصاحبه {candidateName} شروع شد",
                completed ? "InterviewReport" : "Interview", session.Id, ct);
            if (notification is not null) added.Add(notification);
        }
        return added;
    }

    public async Task PublishAsync(IEnumerable<Notification> added)
    {
        foreach (var notification in added.DistinctBy(n => (n.OrganizationId, n.RecipientUserId)))
            await PublishAsync(notification.OrganizationId, notification.RecipientUserId);
    }

    internal async Task<Notification?> AddAsync(Guid org, Guid recipient, string type, string key, string title,
        string target, Guid? interviewId, CancellationToken ct)
    {
        if (await db.Notifications.AnyAsync(n => n.OrganizationId == org && n.RecipientUserId == recipient && n.Type == type && n.SourceKey == key, ct)) return null;
        var notification = new Notification { OrganizationId = org, RecipientUserId = recipient, Type = type, SourceKey = key,
            Title = title, Target = target, InterviewSessionId = interviewId, CreatedAtUtc = time.GetUtcNow() };
        db.Notifications.Add(notification);
        return notification;
    }

    private IQueryable<Notification> Mine() => db.Notifications.Where(n =>
        n.OrganizationId == user.OrganizationId && n.RecipientUserId == user.UserId);

    public async Task<NotificationListDto> ListAsync(string? status, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > 1_000_000) throw AppException.Invalid("invalid_pagination");
        var query = Mine().AsNoTracking();
        query = status switch
        {
            null or "all" => query,
            "read" => query.Where(n => n.ReadAtUtc != null),
            "unread" => query.Where(n => n.ReadAtUtc == null),
            _ => throw AppException.Invalid("invalid_notification_status")
        };
        var total = await query.CountAsync(ct);
        var unread = await Mine().CountAsync(n => n.ReadAtUtc == null, ct);
        var items = await query.OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Target, n.InterviewSessionId, n.CreatedAtUtc, n.ReadAtUtc)).ToListAsync(ct);
        return new(items, total, unread, page, pageSize);
    }

    public async Task<NotificationCountDto> CountAsync(CancellationToken ct) =>
        new(await Mine().CountAsync(n => n.ReadAtUtc == null, ct));

    public async Task SetReadAsync(Guid id, bool isRead, CancellationToken ct)
    {
        // Predicate makes repeated mark-read requests preserve the first read timestamp.
        var query = Mine().Where(n => n.Id == id);
        if (!await query.AnyAsync(ct)) throw AppException.NotFound();
        if (isRead)
            await query.Where(n => n.ReadAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAtUtc, time.GetUtcNow()), ct);
        else
            await query.Where(n => n.ReadAtUtc != null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAtUtc, (DateTimeOffset?)null), ct);
        await PublishAsync(user.OrganizationId, user.UserId);
    }

    public async Task PublishAsync(Guid organizationId, Guid recipientUserId)
    {
        // A transport failure must not turn a committed interview start into an HTTP failure.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await publisher.ChangedAsync(organizationId, recipientUserId, timeout.Token); }
        catch (Exception e) { logger.LogWarning("Notification delivery failed. ExceptionType={ExceptionType}", e.GetType().Name); }
    }
}
