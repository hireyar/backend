using HireYar.Application.Common;
using HireYar.Application.Organizations;
using HireYar.Domain.Organizations;
using HireYar.Infrastructure.Auth;
using HireYar.Infrastructure.Members;
using HireYar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Infrastructure.Organizations;

public sealed class TeamMembers(AppDbContext db, ICurrentUser user, UserManager<ApplicationUser> users, TimeProvider time, HireYar.Application.Subscriptions.SubscriptionService subscriptions, TeamInvitationEmail email, IAccessControl access) : ITeamMembers
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
    private static void ValidateInvitation(InviteTeamMemberRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email)) throw AppException.Invalid("invitation_email_required");
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(request.Email))
            throw AppException.Invalid("invitation_email_invalid");
        var errors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request, new(request), errors, true))
            throw AppException.Invalid("invalid_team_invitation");
    }
    private async Task<TeamInvitationCapacityDto> CapacityLockedAsync(CancellationToken ct)
    {
        var membership = await subscriptions.CurrentAsync(user.OrganizationId, ct); var now = Now;
        var active = await db.Users.CountAsync(x => x.OrganizationId == user.OrganizationId && x.IsActive, ct);
        var pending = await db.MobileTeamInvitations.CountAsync(x => x.OrganizationId == user.OrganizationId &&
            x.AcceptedAtUtc == null && x.CancelledAtUtc == null && x.ExpiresAtUtc > now, ct);
        var occupied = active + pending;
        int? available = membership is null ? 0 : membership.UnlimitedTeam ? null : Math.Max(0, membership.TeamUsers - occupied);
        return new(membership is not null, membership is null || membership.UnlimitedTeam ? null : membership.TeamUsers,
            active, pending, occupied, available, membership?.UnlimitedTeam ?? false, Math.Min(50, available ?? 50));
    }
    public async Task<TeamInvitationCapacityDto> InvitationCapacityAsync(CancellationToken ct)
    {
        RequireTeamAccess(false);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockOrganization(ct);
        var result = await CapacityLockedAsync(ct);
        await tx.CommitAsync(ct); return result;
    }
    public async Task<TeamInvitationDto> InviteAsync(InviteTeamMemberRequest request, CancellationToken ct)
    {
        RequireTeamAccess(); ValidateInvitation(request);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await AccessCatalogLocks.SharedAsync(db, ct);
        await TeamMembershipLocks.LockMobileAsync(db, request.Mobile, ct);
        await LockOrganization(ct);
        var invitation = await CreateInvitationLockedAsync(request, ct);
        await tx.CommitAsync(ct);
        return Map(invitation) with { EmailDelivery = await email.SendAsync(invitation, ct) };
    }
    public async Task<IReadOnlyList<BatchTeamInvitationResult>> InviteBatchAsync(BatchTeamInvitationsRequest request, CancellationToken ct)
    {
        RequireTeamAccess();
        if (request.Invitations is null || request.Invitations.Count is < 1 or > 50 || request.Invitations.Any(x => x is null))
            throw AppException.Invalid("invalid_invitation_batch");
        foreach (var item in request.Invitations) ValidateInvitation(item);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await AccessCatalogLocks.SharedAsync(db, ct);
        // Lock all mobiles in a stable order before the org: batches, single invites and OTP acceptance cannot deadlock.
        foreach (var mobile in request.Invitations.Select(x => x.Mobile).Distinct().Order(StringComparer.Ordinal))
            await TeamMembershipLocks.LockMobileAsync(db, mobile, ct);
        await LockOrganization(ct);
        var capacity = await CapacityLockedAsync(ct);
        if (!capacity.HasActiveSubscription) throw new AppException(403, "active_subscription_required", "An active subscription is required.");
        if (request.Invitations.Count > capacity.MaxBatchSize)
            throw new AppException(409, "team_capacity_exceeded", "تعداد دعوت‌ها بیشتر از ظرفیت باقی‌مانده سازمان است.");
        var outcomes = new List<(string Mobile, MobileTeamInvitation? Invitation, int Status, string? Error)>();
        foreach (var item in request.Invitations) {
            await tx.CreateSavepointAsync("team_batch_item", ct);
            try {
                outcomes.Add((item.Mobile, await CreateInvitationLockedAsync(item, ct), 201, null));
                await tx.ReleaseSavepointAsync("team_batch_item", ct);
            } catch (AppException e) when (e.Status is 400 or 403 or 404 or 409) {
                await tx.RollbackToSavepointAsync("team_batch_item", ct);
                await tx.ReleaseSavepointAsync("team_batch_item", ct);
                db.ChangeTracker.Clear();
                outcomes.Add((item.Mobile, null, e.Status, e.Code));
            }
        }
        await tx.CommitAsync(ct);
        var results = new List<BatchTeamInvitationResult>();
        // SMTP stays outside every lock and transaction. Delivery failure never silently removes a created invitation.
        foreach (var outcome in outcomes) {
            var result = outcome.Invitation is null ? null : Map(outcome.Invitation) with { EmailDelivery = await email.SendAsync(outcome.Invitation, ct) };
            results.Add(new(outcome.Mobile, result, outcome.Status, outcome.Error));
        }
        return results;
    }
    private async Task<MobileTeamInvitation> CreateInvitationLockedAsync(InviteTeamMemberRequest request, CancellationToken ct)
    {
        var selectedRole = await AssignableRole(request.AccessRoleCode, ct);
        var existing = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.PhoneNumber == request.Mobile, ct);
        if (existing is not null && (existing.OrganizationId != user.OrganizationId || existing.Role != OrganizationRole.Recruiter || existing.IsActive))
            throw new AppException(409, "mobile_unavailable", "This mobile cannot be invited.");
        var pending = await db.MobileTeamInvitations.Where(x => x.Mobile == request.Mobile && x.AcceptedAtUtc == null && x.CancelledAtUtc == null).SingleOrDefaultAsync(ct);
        if (pending is not null)
        {
            if (pending.ExpiresAtUtc > Now) throw new AppException(409, "invitation_pending", "A pending invitation already exists.");
            await RequireRoleManagementAsync(pending.AccessRoleCode, ct);
            pending.CancelledAtUtc = Now;
            await db.SaveChangesAsync(ct);
        }
        if (existing is not null) await RequireRoleManagementAsync(existing.AccessRoleCode, ct);
        await subscriptions.RequireTeamCapacityLockedAsync(user.OrganizationId, false, ct);
        var now = Now;
        var invitation = new MobileTeamInvitation { OrganizationId = user.OrganizationId, Mobile = request.Mobile, FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), AccessRoleCode = selectedRole.Code, Email = request.Email?.Trim(), Language = "fa", Message = request.Message?.Trim(), CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(3) };
        db.MobileTeamInvitations.Add(invitation);
        Audit("InvitationCreated", invitation.Id, selectedRole.Code);
        await db.SaveChangesAsync(ct);
        return invitation;
    }
    public async Task CancelAsync(Guid id, CancellationToken ct)
    {
        RequireTeamAccess();
        var mobile = await db.MobileTeamInvitations.AsNoTracking().Where(x => x.Id == id && x.OrganizationId == user.OrganizationId).Select(x => x.Mobile).SingleOrDefaultAsync(ct) ?? throw AppException.NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await AccessCatalogLocks.SharedAsync(db, ct);
        await TeamMembershipLocks.LockMobileAsync(db, mobile, ct); await LockOrganization(ct);
        var invitation = await db.MobileTeamInvitations.SingleAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId, ct);
        if (invitation.AcceptedAtUtc is not null) throw new AppException(409, "invitation_accepted", "Invitation already accepted.");
        await RequireRoleManagementAsync(invitation.AccessRoleCode, ct);
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
        await AccessCatalogLocks.SharedAsync(db, ct);
        if (target.PhoneNumber is not null) await TeamMembershipLocks.LockMobileAsync(db, target.PhoneNumber, ct);
        await LockOrganization(ct);
        var member = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {id} AND \"OrganizationId\" = {user.OrganizationId} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw AppException.NotFound();
        if (member.Role != OrganizationRole.Recruiter) throw AppException.Invalid("cannot_deactivate_owner");
        await RequireRoleManagementAsync(member.AccessRoleCode, ct);
        Audit("MemberDeactivated", member.Id, member.AccessRoleCode ?? "interview_manager");
        member.IsActive = false; member.UpdatedAtUtc = Now;
        var result = await users.UpdateSecurityStampAsync(member);
        if (!result.Succeeded) throw AppException.Invalid("account_rejected");
        await db.RefreshTokens.Where(x => x.UserId == id && x.RevokedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, Now), ct);
        await tx.CommitAsync(ct);
    }
    private void Audit(string action, Guid subject, string detail) => db.AccessAudits.Add(new() {
        ActorId = user.UserId, OrganizationId = user.OrganizationId, SubjectId = subject, Action = action, Detail = detail, CreatedAtUtc = Now });
    private async Task RequireRoleManagementAsync(string? code, CancellationToken ct)
    {
        var role = await db.AccessRoles.AsNoTracking().Include(x => x.Sections)
            .SingleOrDefaultAsync(x => x.Code == (code ?? "interview_manager"), ct) ?? throw AppException.Invalid("access_role_unavailable");
        await RequireRoleManagementAsync(role, ct);
    }
    private async Task RequireRoleManagementAsync(AccessRole role, CancellationToken ct)
    {
        if (user.Role == OrganizationRole.Owner) return;
        if (AccessRoleRules.RequiresOwnerAssignment(role))
            throw new AppException(403, "forbidden", "شما به این بخش دسترسی ندارید");
        var actor = (await access.PermissionsAsync(user.UserId, ct)).ToDictionary(x => x.Code, x => x.Access);
        // Read current DB permissions, not the request's earlier token-validation snapshot.
        if (actor.GetValueOrDefault("team") != SectionAccess.Write ||
            role.Sections.Any(x => x.Access > actor.GetValueOrDefault(x.SectionCode)))
            throw new AppException(403, "forbidden", "شما به این بخش دسترسی ندارید");
    }
    private async Task<AccessRole> AssignableRole(string code, CancellationToken ct)
    {
        var role = await db.AccessRoles.AsNoTracking().Include(x => x.Sections).SingleOrDefaultAsync(x => x.Code == code && x.IsActive, ct)
            ?? throw AppException.Invalid("access_role_unavailable");
        await RequireRoleManagementAsync(role, ct); return role;
    }
    public async Task ChangeAccessAsync(Guid id, ChangeMemberAccessRequest request, CancellationToken ct)
    {
        RequireTeamAccess();
        // Match deactivation lock order: mobile -> organization -> member.
        var target = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId, ct) ?? throw AppException.NotFound();
        if (target.Role == OrganizationRole.Owner) throw AppException.Invalid("cannot_change_owner");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await AccessCatalogLocks.SharedAsync(db, ct);
        if (target.PhoneNumber is not null) await TeamMembershipLocks.LockMobileAsync(db, target.PhoneNumber, ct);
        await LockOrganization(ct);
        var member = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {id} AND \"OrganizationId\" = {user.OrganizationId} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw AppException.NotFound();
        if (member.Role == OrganizationRole.Owner) throw AppException.Invalid("cannot_change_owner");
        await RequireRoleManagementAsync(member.AccessRoleCode, ct);
        var role = await AssignableRole(request.AccessRoleCode, ct);
        Audit("MemberAccessChanged", id, $"{member.AccessRoleCode ?? "interview_manager"}->{role.Code}");
        member.AccessRoleCode = role.Code; member.UpdatedAtUtc = Now;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<TeamInvitationDto> ResendAsync(Guid id, CancellationToken ct)
    {
        RequireTeamAccess();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await AccessCatalogLocks.SharedAsync(db, ct);
        await LockOrganization(ct);
        var invite = await db.MobileTeamInvitations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId, ct) ?? throw AppException.NotFound();
        await RequireRoleManagementAsync(invite.AccessRoleCode, ct);
        if (invite.AcceptedAtUtc is not null || invite.CancelledAtUtc is not null || invite.ExpiresAtUtc <= Now)
            throw new AppException(409, "invitation_unavailable", "Invitation is no longer available.");
        if (invite.Email is null) throw AppException.Invalid("invitation_email_required");
        // Do not hold catalog locks during SMTP I/O. A later cancellation still invalidates acceptance.
        await tx.CommitAsync(ct);
        // The original 72 hour deadline stays fixed; resend never reactivates a consumed invitation.
        return Map(invite) with { EmailDelivery = await email.SendAsync(invite, ct) };
    }
}
