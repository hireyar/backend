using HireYar.Application.Administration;
using HireYar.Application.Common;
using HireYar.Application.Sales;
using HireYar.Domain.Organizations;
using HireYar.Domain.Sales;
using HireYar.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Infrastructure.Administration;
public sealed partial class AdminCustomers
{
    private async Task<CustomerRow> RowAsync(Guid id,CancellationToken ct)=>await BaseRows().SingleOrDefaultAsync(x=>x.Id==id,ct) ?? throw AppException.NotFound();
    private static void Expected(CustomerRow row,string expected) {
        if(ProfileVersion(row)!=expected) throw new AppException(409,"customer_changed","Refresh customer before editing.");
    }
    private static void Check(Microsoft.AspNetCore.Identity.IdentityResult result) {if(!result.Succeeded) throw AppException.Invalid("account_rejected");}
    private async Task LockMobiles(IEnumerable<string?> mobiles,CancellationToken ct) {
        foreach(var mobile in mobiles.Where(x=>x is not null).Select(x=>x!).Distinct().Order(StringComparer.Ordinal)) await TeamMembershipLocks.LockMobileAsync(db,mobile,ct);
    }
    public async Task<AdminCustomerMutationDto> ProfileAsync(Guid actor,Guid id,AdminCustomerProfileRequest r,CancellationToken ct)
    {
        CustomerSubscriptionAdministration.Validate(r.RequestId,r.Reason);SalesService.Text(r.FullName,200);if(r.Email is not null) SalesService.Text(r.Email,256);
        var metadata=await RowAsync(id,ct); SalesService.Text(r.OrganizationName ?? "",200,metadata.Kind=="User"); var hash=CustomerSubscriptionAdministration.Fingerprint(new {Action="ProfileUpdated",Request=r});
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await LockMobiles([metadata.Mobile,r.Mobile],ct);
        ApplicationUser? user=null;
        if(metadata.Kind=="User") {
            user=await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\"={id} FOR UPDATE").SingleAsync(ct);
            await db.Organizations.FromSqlInterpolated($"SELECT * FROM \"Organizations\" WHERE \"Id\"={user.OrganizationId} FOR UPDATE").LoadAsync(ct);
        }
        var result=await db.WithSalesLockAsync(async()=> {
            var old=await administration.ReplayAsync(id,r.RequestId,hash,ct);if(old is not null) return old;
            var before=await RowAsync(id,ct);Expected(before,r.ExpectedVersion);
            if(before.IsSystemAdmin) throw new AppException(403,"privileged_account_protected","Use the administrator account workflow for privileged identity changes.");
            if(before.Mobile!=metadata.Mobile) throw new AppException(409,"customer_changed","Refresh customer before editing.");
            if(await db.Users.AnyAsync(x=>x.PhoneNumber==r.Mobile && x.Id!=id,ct) || await db.SalesLeads.AnyAsync(x=>x.Mobile==r.Mobile && x.Id!=before.LeadId,ct))
                throw new AppException(409,"mobile_unavailable","Mobile already belongs to another customer or lead.");
            if(await db.MobileTeamInvitations.AnyAsync(x=>x.Mobile==r.Mobile && x.AcceptedAtUtc==null && x.CancelledAtUtc==null && x.ExpiresAtUtc>time.GetUtcNow(),ct))
                throw new AppException(409,"team_invitation_pending","Mobile has a pending team invitation.");
            var email=string.IsNullOrWhiteSpace(r.Email) ? null : r.Email.Trim();
            if(email is not null) {
                var normalized=users.NormalizeEmail(email);
                if(await db.Users.AnyAsync(x=>x.NormalizedEmail==normalized && x.Id!=id,ct)) throw new AppException(409,"email_unavailable","Email is already registered.");
            }
            if(user is not null) {
                var org=await db.Organizations.SingleAsync(x=>x.Id==user.OrganizationId,ct);
                var identityChanged=user.PhoneNumber!=r.Mobile || user.Email!=email;
                user.FullName=r.FullName.Trim();user.UpdatedAtUtc=time.GetUtcNow();org.Name=(r.OrganizationName ?? "").Trim();org.UpdatedAtUtc=time.GetUtcNow();
                if(user.Email!=email){user.Email=email;user.EmailConfirmed=false;}
                user.PhoneNumber=r.Mobile;user.UserName=r.Mobile;
                Check(await users.UpdateAsync(user));
                if(identityChanged) {
                    Check(await users.UpdateSecurityStampAsync(user));
                    var now=time.GetUtcNow();
                    await db.RefreshTokens.Where(x=>x.UserId==id && x.RevokedAtUtc==null).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.RevokedAtUtc,now),ct);
                    await db.OtpChallenges.Where(x=>x.Mobile==metadata.Mobile || x.Mobile==r.Mobile).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ConsumedAtUtc,now),ct);
                }
            } else {
                var lead=await db.SalesLeads.SingleAsync(x=>x.Id==id && x.OrganizationId==null,ct);
                lead.Mobile=r.Mobile;lead.CustomerName=r.FullName.Trim();lead.ContactOrganizationName=(r.OrganizationName ?? "").Trim();lead.ContactEmail=email;
            }
            var e=administration.Event(actor,id,before.OrganizationId,r.RequestId,hash,"ProfileUpdated",r.Reason,
                new {Before=new {before.FullName,before.OrganizationName,before.Mobile,before.Email},After=new {r.FullName,r.OrganizationName,r.Mobile,Email=email}},before.Kind);
            return new AdminCustomerMutationDto(e.Id,false);
        },ct);
        await tx.CommitAsync(ct);return result;
    }
    public async Task<AdminCustomerMutationDto> SellerAsync(Guid actor,Guid id,AdminCustomerSellerRequest r,CancellationToken ct)
    {
        CustomerSubscriptionAdministration.Validate(r.RequestId,r.Reason);
        var metadata=await RowAsync(id,ct);var hash=CustomerSubscriptionAdministration.Fingerprint(new {Action="SellerChanged",Request=r});
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await LockMobiles([metadata.Mobile],ct);
        if(metadata.OrganizationId is not null) await db.Organizations.FromSqlInterpolated($"SELECT * FROM \"Organizations\" WHERE \"Id\"={metadata.OrganizationId.Value} FOR UPDATE").AsNoTracking().LoadAsync(ct);
        var result=await db.WithSalesLockAsync(async()=> {
            var old=await administration.ReplayAsync(id,r.RequestId,hash,ct);if(old is not null) return old;
            var before=await RowAsync(id,ct);Expected(before,r.ExpectedVersion);
            if(r.AgentId is not null && !await db.SalesAgents.AnyAsync(x=>x.Id==r.AgentId && x.IsActive,ct)) throw AppException.Invalid("sales_agent_unavailable");
            SalesLead? lead=before.LeadId is null ? null : await db.SalesLeads.SingleAsync(x=>x.Id==before.LeadId,ct);
            if(lead is null && r.AgentId is not null) {
                if(before.Mobile is null) throw AppException.Invalid("mobile_required");
                lead=new() {AgentId=r.AgentId,OrganizationId=before.OrganizationId,RegisteredAtUtc=before.RegisteredAtUtc,CustomerName=before.FullName,
                    Mobile=before.Mobile,CreatedAtUtc=time.GetUtcNow(),LastContactAtUtc=time.GetUtcNow()};db.SalesLeads.Add(lead);
            } else if(lead is not null) lead.AgentId=r.AgentId;
            if(lead is not null) db.SalesAudits.Add(new() {ActorId=actor,LeadId=lead.Id,Action="CustomerSellerChanged",Reason=r.Reason.Trim(),CreatedAtUtc=time.GetUtcNow()});
            var e=administration.Event(actor,id,before.OrganizationId,r.RequestId,hash,"SellerChanged",r.Reason,new {PreviousAgentId=before.AgentId,AgentId=r.AgentId},before.Kind);
            return new AdminCustomerMutationDto(e.Id,false);
        },ct);
        await tx.CommitAsync(ct);return result;
    }
}
