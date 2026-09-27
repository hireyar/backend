using HireYar.Application.Common;
using HireYar.Application.Members;
using HireYar.Domain.Organizations;
using HireYar.Infrastructure.Auth;
using HireYar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace HireYar.Infrastructure.Members;

public sealed class TeamMembers(AppDbContext db, UserManager<ApplicationUser> users, IEmailSender email, IOptions<SecurityOptions> settings, TimeProvider time, ILogger<TeamMembers> logger) : ITeamMembers
{
    private DateTimeOffset Now => time.GetUtcNow();
    public async Task<Page<MemberDto>> ListAsync(Guid org, PageQuery p, CancellationToken ct)
    {
        var q = db.Users.AsNoTracking().Where(x => x.OrganizationId == org); var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(p.Skip).Take(p.PageSize).Select(x => new MemberDto(x.Id, x.Email, x.PhoneNumber, x.FullName, x.Role, x.IsActive)).ToListAsync(ct);
        return new(rows, total, p.Page, p.PageSize);
    }
    public async Task<Page<InvitationDto>> InvitationsAsync(Guid org, PageQuery p, CancellationToken ct)
    {
        var q = db.TeamInvitations.AsNoTracking().Where(x => x.OrganizationId == org); var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(p.Skip).Take(p.PageSize).Select(x => new InvitationDto(x.Id, x.Email, x.ExpiresAtUtc, x.AcceptedAtUtc, x.CancelledAtUtc)).ToListAsync(ct);
        return new(rows, total, p.Page, p.PageSize);
    }
    public async Task<InvitationDelivery> InviteAsync(Guid org, InviteRequest r, CancellationToken ct)
    {
        var normalized = users.NormalizeEmail(r.Email.Trim())!; var raw = TokenSecrets.Create();
        var invite = new TeamInvitation { OrganizationId = org, Email = r.Email.Trim(), NormalizedEmail = normalized, TokenHash = TokenSecrets.Hash(raw), CreatedAtUtc = Now, ExpiresAtUtc = Now.AddDays(3) };
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await LockOrganization(org, ct);
            if (await db.Users.AnyAsync(x => x.NormalizedEmail == normalized, ct)) throw AppException.Conflict();
            var previous = await db.TeamInvitations.SingleOrDefaultAsync(x => x.OrganizationId == org && x.NormalizedEmail == normalized && x.AcceptedAtUtc == null && x.CancelledAtUtc == null, ct);
            if (previous is not null) { if (previous.ExpiresAtUtc > Now) throw AppException.Conflict(); previous.CancelledAtUtc = Now; await db.SaveChangesAsync(ct); }
            db.TeamInvitations.Add(invite); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        return new(invite.Id, await Send(invite.Email, raw, ct));
    }
    public async Task<InvitationDelivery> ResendAsync(Guid org, Guid id, CancellationToken ct)
    {
        var raw = TokenSecrets.Create(); TeamInvitation row;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await LockOrganization(org, ct); row = await Find(org, id, ct);
            if (row.AcceptedAtUtc.HasValue || row.CancelledAtUtc.HasValue) throw AppException.Conflict();
            if (await db.Users.AnyAsync(x => x.NormalizedEmail == row.NormalizedEmail, ct)) throw AppException.Conflict();
            row.TokenHash = TokenSecrets.Hash(raw); row.ExpiresAtUtc = Now.AddDays(3); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        return new(row.Id, await Send(row.Email, raw, ct));
    }
    public async Task CancelAsync(Guid org, Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockOrganization(org, ct); var row = await Find(org, id, ct);
        if (row.AcceptedAtUtc.HasValue) throw AppException.Conflict(); row.CancelledAtUtc = Now; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task AcceptAsync(AcceptInviteRequest r, CancellationToken ct)
    {
        var hash = TokenSecrets.Hash(r.Token);
        var org = await db.TeamInvitations.AsNoTracking().Where(x => x.TokenHash == hash).Select(x => (Guid?)x.OrganizationId).SingleOrDefaultAsync(ct);
        if (org is null) throw AppException.Invalid("invalid_invitation");
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockOrganization(org.Value, ct);
        var row = await db.TeamInvitations.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (row is null || row.ExpiresAtUtc <= Now || row.AcceptedAtUtc.HasValue || row.CancelledAtUtc.HasValue) throw AppException.Invalid("invalid_invitation");
        if (await db.Users.AnyAsync(x => x.NormalizedEmail == row.NormalizedEmail, ct)) throw AppException.Conflict();
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = row.Email, UserName = row.Email, FullName = r.FullName.Trim(), OrganizationId = row.OrganizationId, Role = OrganizationRole.Recruiter, EmailConfirmed = true, CreatedAtUtc = Now, UpdatedAtUtc = Now };
        var result = await users.CreateAsync(user, r.Password); if (!result.Succeeded) throw AppException.Invalid("account_rejected");
        row.AcceptedAtUtc = Now; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task SetActiveAsync(Guid org, Guid id, bool active, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var u = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\"={id} AND \"OrganizationId\"={org} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw AppException.NotFound();
        if (u.Role == OrganizationRole.Owner) throw AppException.Invalid("owner_cannot_be_changed");
        u.IsActive = active; u.UpdatedAtUtc = Now; await db.SaveChangesAsync(ct);
        if (!active) await db.RefreshTokens.Where(x => x.UserId == id && x.RevokedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, Now), ct);
        await tx.CommitAsync(ct);
    }
    public Task<int> ActiveCountAsync(Guid org, CancellationToken ct) => db.Users.CountAsync(x => x.OrganizationId == org && x.IsActive, ct);
    public Task<int> PendingCountAsync(Guid org, CancellationToken ct) => db.TeamInvitations.CountAsync(x => x.OrganizationId == org && x.AcceptedAtUtc == null && x.CancelledAtUtc == null && x.ExpiresAtUtc > Now, ct);
    private async Task<TeamInvitation> Find(Guid org, Guid id, CancellationToken ct) => await db.TeamInvitations.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == org, ct) ?? throw AppException.NotFound();
    private async Task LockOrganization(Guid org, CancellationToken ct) => _ = await db.Organizations.FromSqlInterpolated($"SELECT * FROM \"Organizations\" WHERE \"Id\"={org} FOR UPDATE").SingleAsync(ct);
    private async Task<bool> Send(string recipient, string raw, CancellationToken ct)
    {
        try { await email.SendAsync(recipient, "دعوت به تیم HireYar", $"برای پیوستن به تیم، لینک زیر را باز کنید. اعتبار لینک سه روز است:\n{settings.Value.FrontendUrl.TrimEnd('/')}/accept-invitation?token={Uri.EscapeDataString(raw)}", ct); return true; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { logger.LogWarning("Invitation email delivery failed. EventCode={EventCode}", "email_delivery_failed"); return false; }
    }
}
