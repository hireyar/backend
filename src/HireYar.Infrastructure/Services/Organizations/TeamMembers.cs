using HireYar.Application.Common;
using HireYar.Application.Organizations;
using HireYar.Domain.Organizations;
using HireYar.Infrastructure.Auth;
using HireYar.Infrastructure.Members;
using HireYar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Infrastructure.Organizations;

public sealed class TeamMembers(AppDbContext db, ICurrentUser user, UserManager<ApplicationUser> users, TimeProvider time, HireYar.Application.Subscriptions.SubscriptionService subscriptions, TeamInvitationEmail email) : ITeamMembers
{
    private DateTimeOffset Now => time.GetUtcNow();
    private void RequireTeamAccess(bool write = true)
    {
        if (!user.CanAccess("team", write)) throw new AppException(403, "forbidden", "شما به این بخش دسترسی ندارید");
    }
    private async Task LockOrganization(CancellationToken ct)
    {
        var org = await db.Organizations.FromSqlInterpolated($"SELECT * FROM \"Organizations\" WHERE \"Id\" = {user.OrganizationId} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct);
        if (org is null) throw AppException.NotFound();
    }
    private TeamInvitationDto Map(MobileTeamInvitation i) => new(i.Id, i.Mobile, i.CreatedAtUtc, i.ExpiresAtUtc,
        i.AcceptedAtUtc is not null ? "Accepted" : i.CancelledAtUtc is not null ? "Cancelled" : i.ExpiresAtUtc <= Now ? "Expired" : "Pending", i.FirstName, i.LastName, i.AccessRoleCode, i.Email, i.Language);
    public async Task<Page<TeamMemberDto>> ListAsync(PageQuery page, CancellationToken ct)
    {
        RequireTeamAccess(false);
        var query = db.Users.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId);
        return new(await query.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(page.Skip).Take(page.PageSize)
            .Select(x => new TeamMemberDto(x.Id, x.FullName, x.PhoneNumber, x.Role, x.IsActive, x.Role == OrganizationRole.Owner ? "admin" : x.AccessRoleCode ?? "interview_manager")).ToListAsync(ct), await query.CountAsync(ct), page.Page, page.PageSize);
    }
    public async Task<Page<TeamInvitationDto>> InvitationsAsync(PageQuery page, CancellationToken ct)
    {
        RequireTeamAccess(false);
        var query = db.MobileTeamInvitations.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId);
        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new(rows.Select(Map).ToList(), await query.CountAsync(ct), page.Page, page.PageSize);
    }
    public async Task<TeamInvitationDto> InviteAsync(InviteTeamMemberRequest request, CancellationToken ct)
    {
        RequireTeamAccess();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var selectedRole = await AssignableRole(request.AccessRoleCode, ct);
        await TeamMembershipLocks.LockMobileAsync(db, request.Mobile, ct);
        await LockOrganization(ct);
        var existing = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.PhoneNumber == request.Mobile, ct);
        if (existing is not null && (existing.OrganizationId != user.OrganizationId || existing.Role != OrganizationRole.Recruiter || existing.IsActive))
            throw new AppException(409, "mobile_unavailable", "This mobile cannot be invited.");
        var pending = await db.MobileTeamInvitations.Where(x => x.Mobile == request.Mobile && x.AcceptedAtUtc == null && x.CancelledAtUtc == null).SingleOrDefaultAsync(ct);
        if (pending is not null)
        {
            if (pending.ExpiresAtUtc > Now) throw new AppException(409, "invitation_pending", "A pending invitation already exists.");
            pending.CancelledAtUtc = Now;
            await db.SaveChangesAsync(ct);
        }
        if (existing is not null) await ProtectAdministrator(existing.AccessRoleCode, ct);
        await subscriptions.RequireTeamCapacityLockedAsync(user.OrganizationId, false, ct);
        var now = Now;
        var invitation = new MobileTeamInvitation { OrganizationId = user.OrganizationId, Mobile = request.Mobile, FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), AccessRoleCode = selectedRole.Code, Email = request.Email?.Trim(), Language = "fa", Message = request.Message?.Trim(), CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(3) };
        db.MobileTeamInvitations.Add(invitation);
        Audit("InvitationCreated", invitation.Id, selectedRole.Code);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Map(invitation) with { EmailDelivery = await email.SendAsync(invitation, ct) };
    }
    public async Task CancelAsync(Guid id, CancellationToken ct)
    {
        RequireTeamAccess();
        var mobile = await db.MobileTeamInvitations.AsNoTracking().Where(x => x.Id == id && x.OrganizationId == user.OrganizationId).Select(x => x.Mobile).SingleOrDefaultAsync(ct) ?? throw AppException.NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await TeamMembershipLocks.LockMobileAsync(db, mobile, ct); await LockOrganization(ct);
        var invitation = await db.MobileTeamInvitations.SingleAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId, ct);
        if (invitation.AcceptedAtUtc is not null) throw new AppException(409, "invitation_accepted", "Invitation already accepted.");
        await ProtectAdministrator(invitation.AccessRoleCode, ct);
        invitation.CancelledAtUtc ??= Now;
        Audit("InvitationCancelled", invitation.Id, invitation.AccessRoleCode);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        RequireTeamAccess();
        var target = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId, ct) ?? throw AppException.NotFound();
        if (target.Role != OrganizationRole.Recruiter) throw AppException.Invalid("cannot_deactivate_owner");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (target.PhoneNumber is not null) await TeamMembershipLocks.LockMobileAsync(db, target.PhoneNumber, ct);
        await LockOrganization(ct);
        var member = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {id} AND \"OrganizationId\" = {user.OrganizationId} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw AppException.NotFound();
        if (member.Role != OrganizationRole.Recruiter) throw AppException.Invalid("cannot_deactivate_owner");
        await ProtectAdministrator(member.AccessRoleCode, ct);
        Audit("MemberDeactivated", member.Id, member.AccessRoleCode ?? "interview_manager");
        member.IsActive = false; member.UpdatedAtUtc = Now;
        var result = await users.UpdateSecurityStampAsync(member);
        if (!result.Succeeded) throw AppException.Invalid("account_rejected");
        await db.RefreshTokens.Where(x => x.UserId == id && x.RevokedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, Now), ct);
        await tx.CommitAsync(ct);
    }
    private void Audit(string action, Guid subject, string detail) => db.AccessAudits.Add(new() {
        ActorId = user.UserId, OrganizationId = user.OrganizationId, SubjectId = subject, Action = action, Detail = detail, CreatedAtUtc = Now });
    private async Task ProtectAdministrator(string? code, CancellationToken ct)
    {
        if (user.Role != OrganizationRole.Owner && await db.AccessRoles.AnyAsync(x => x.Code == (code ?? "interview_manager") && x.IsAdministrator, ct))
            throw new AppException(403, "forbidden", "شما به این بخش دسترسی ندارید");
    }
    private async Task<AccessRole> AssignableRole(string code, CancellationToken ct)
    {
        var role = await db.AccessRoles.AsNoTracking().SingleOrDefaultAsync(x => x.Code == code && x.IsActive, ct)
            ?? throw AppException.Invalid("access_role_unavailable");
        await ProtectAdministrator(code, ct); return role;
    }
    public async Task ChangeAccessAsync(Guid id, ChangeMemberAccessRequest request, CancellationToken ct)
    {
        RequireTeamAccess();
        // Match deactivation lock order: mobile -> organization -> member.
        var target = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId, ct) ?? throw AppException.NotFound();
        if (target.Role == OrganizationRole.Owner) throw AppException.Invalid("cannot_change_owner");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (target.PhoneNumber is not null) await TeamMembershipLocks.LockMobileAsync(db, target.PhoneNumber, ct);
        await LockOrganization(ct);
        var member = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {id} AND \"OrganizationId\" = {user.OrganizationId} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw AppException.NotFound();
        await ProtectAdministrator(member.AccessRoleCode, ct);
        var role = await AssignableRole(request.AccessRoleCode, ct);
        Audit("MemberAccessChanged", id, $"{member.AccessRoleCode ?? "interview_manager"}->{role.Code}");
        member.AccessRoleCode = role.Code; member.UpdatedAtUtc = Now;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<TeamInvitationDto> ResendAsync(Guid id, CancellationToken ct)
    {
        RequireTeamAccess();
        var invite = await db.MobileTeamInvitations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId, ct) ?? throw AppException.NotFound();
        await ProtectAdministrator(invite.AccessRoleCode, ct);
        if (invite.AcceptedAtUtc is not null || invite.CancelledAtUtc is not null || invite.ExpiresAtUtc <= Now)
            throw new AppException(409, "invitation_unavailable", "Invitation is no longer available.");
        if (invite.Email is null) throw AppException.Invalid("invitation_email_required");
        // The original 72 hour deadline stays fixed; resend never reactivates a consumed invitation.
        return Map(invite) with { EmailDelivery = await email.SendAsync(invite, ct) };
    }
}
