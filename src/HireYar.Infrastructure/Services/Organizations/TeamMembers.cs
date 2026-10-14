using HireYar.Application.Common;
using HireYar.Application.Organizations;
using HireYar.Domain.Organizations;
using HireYar.Infrastructure.Auth;
using HireYar.Infrastructure.Members;
using HireYar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Infrastructure.Organizations;

public sealed class TeamMembers(AppDbContext db, ICurrentUser user, UserManager<ApplicationUser> users, TimeProvider time) : ITeamMembers
{
    private DateTimeOffset Now => time.GetUtcNow();
    private void Owner()
    {
        if (user.Role != OrganizationRole.Owner) throw new AppException(403, "forbidden", "Owner access required.");
    }
    private async Task LockOrganization(CancellationToken ct)
    {
        var org = await db.Organizations.FromSqlInterpolated($"SELECT * FROM \"Organizations\" WHERE \"Id\" = {user.OrganizationId} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct);
        if (org is null) throw AppException.NotFound();
    }
    private TeamInvitationDto Map(MobileTeamInvitation i) => new(i.Id, i.Mobile, i.CreatedAtUtc, i.ExpiresAtUtc,
        i.AcceptedAtUtc is not null ? "Accepted" : i.CancelledAtUtc is not null ? "Cancelled" : i.ExpiresAtUtc <= Now ? "Expired" : "Pending", i.FirstName, i.LastName);
    public async Task<Page<TeamMemberDto>> ListAsync(PageQuery page, CancellationToken ct)
    {
        Owner();
        var query = db.Users.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId);
        return new(await query.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(page.Skip).Take(page.PageSize)
            .Select(x => new TeamMemberDto(x.Id, x.FullName, x.PhoneNumber, x.Role, x.IsActive)).ToListAsync(ct), await query.CountAsync(ct), page.Page, page.PageSize);
    }
    public async Task<Page<TeamInvitationDto>> InvitationsAsync(PageQuery page, CancellationToken ct)
    {
        Owner();
        var query = db.MobileTeamInvitations.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId);
        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new(rows.Select(Map).ToList(), await query.CountAsync(ct), page.Page, page.PageSize);
    }
    public async Task<TeamInvitationDto> InviteAsync(InviteTeamMemberRequest request, CancellationToken ct)
    {
        Owner();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
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
        var invitation = new MobileTeamInvitation { OrganizationId = user.OrganizationId, Mobile = request.Mobile, FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), CreatedAtUtc = Now, ExpiresAtUtc = Now.AddDays(3) };
        db.MobileTeamInvitations.Add(invitation);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Map(invitation);
    }
    public async Task CancelAsync(Guid id, CancellationToken ct)
    {
        Owner();
        var mobile = await db.MobileTeamInvitations.AsNoTracking().Where(x => x.Id == id && x.OrganizationId == user.OrganizationId).Select(x => x.Mobile).SingleOrDefaultAsync(ct) ?? throw AppException.NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await TeamMembershipLocks.LockMobileAsync(db, mobile, ct); await LockOrganization(ct);
        var invitation = await db.MobileTeamInvitations.SingleAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId, ct);
        if (invitation.AcceptedAtUtc is not null) throw new AppException(409, "invitation_accepted", "Invitation already accepted.");
        invitation.CancelledAtUtc ??= Now;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        Owner();
        var target = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId, ct) ?? throw AppException.NotFound();
        if (target.Role != OrganizationRole.Recruiter) throw AppException.Invalid("cannot_deactivate_owner");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (target.PhoneNumber is not null) await TeamMembershipLocks.LockMobileAsync(db, target.PhoneNumber, ct);
        await LockOrganization(ct);
        var member = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {id} AND \"OrganizationId\" = {user.OrganizationId} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw AppException.NotFound();
        if (member.Role != OrganizationRole.Recruiter) throw AppException.Invalid("cannot_deactivate_owner");
        member.IsActive = false; member.UpdatedAtUtc = Now;
        var result = await users.UpdateSecurityStampAsync(member);
        if (!result.Succeeded) throw AppException.Invalid("account_rejected");
        await db.RefreshTokens.Where(x => x.UserId == id && x.RevokedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, Now), ct);
        await tx.CommitAsync(ct);
    }
}
