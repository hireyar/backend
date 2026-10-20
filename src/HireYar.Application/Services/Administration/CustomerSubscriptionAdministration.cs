using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HireYar.Application.Common;
using HireYar.Application.Sales;
using HireYar.Application.Subscriptions;
using HireYar.Domain.Administration;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Administration;

public sealed class CustomerSubscriptionAdministration(IApplicationDbContext db, SubscriptionService subscriptions, IOrganizationTeamUsage team, TimeProvider time)
{
    public static string Fingerprint(object data) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data))));
    public static void Validate(Guid requestId, string reason) { if(requestId==Guid.Empty) throw AppException.Invalid("request_id_required"); SalesService.Text(reason,1000); }
    public async Task<string> VersionAsync(Guid org, CancellationToken ct) {
        var state=await subscriptions.StateAsync(org,ct);
        var revision=await db.CustomerSubscriptionOverrides.Where(x=>x.OrganizationId==org).Select(x=>(Guid?)x.EventId).SingleOrDefaultAsync(ct);
        return Fingerprint(new { state.StartedAtUtc,state.ExpiresAtUtc,state.HasActiveSubscription,revision });
    }
    public async Task<AdminCustomerMutationDto?> ReplayAsync(Guid customer, Guid request, string fingerprint, CancellationToken ct) {
        var old=await db.CustomerAdministrationEvents.AsNoTracking().SingleOrDefaultAsync(x=>x.CustomerId==customer && x.RequestId==request,ct);
        if(old is null) return null;
        if(old.Fingerprint!=fingerprint) throw new AppException(409,"idempotency_conflict","Reuse request id only for the same operation.");
        return new(old.Id,true);
    }
    public CustomerAdministrationEvent Event(Guid actor,Guid customer,Guid? org,Guid request,string fingerprint,string action,string reason,object details,string kind="User") {
        var e=new CustomerAdministrationEvent {ActorId=actor,CustomerId=customer,CustomerKind=kind,OrganizationId=org,RequestId=request,Fingerprint=fingerprint,
            Action=action,Reason=reason.Trim(),DetailsJson=JsonSerializer.Serialize(details),CreatedAtUtc=time.GetUtcNow()};
        db.CustomerAdministrationEvents.Add(e); return e;
    }
    public Task<AdminCustomerMutationDto> CreditAsync(Guid actor,Guid customer,Guid org,AdminCustomerCreditRequest r,CancellationToken ct) => db.WithOrganizationLockAsync(org,async()=> {
        Validate(r.RequestId,r.Reason); if(r.Quantity is <1 or >100000) throw AppException.Invalid();
        var hash=Fingerprint(new {Action="InterviewCreditGranted",Request=r});
        var old=await ReplayAsync(customer,r.RequestId,hash,ct); if(old is not null) return old;
        var bundle=await db.InterviewBundles.AsNoTracking().SingleOrDefaultAsync(x=>x.Code==r.BundleCode && x.IsActive,ct) ?? throw AppException.Invalid("invalid_bundle");
        var e=Event(actor,customer,org,r.RequestId,hash,"InterviewCreditGranted",r.Reason,new {bundle.Code,bundle.Name,r.Quantity});
        e.CreditQuantity=r.Quantity; e.BundleCode=bundle.Code; return new AdminCustomerMutationDto(e.Id,false);
    },ct);
    public Task<AdminCustomerMutationDto> SetAsync(Guid actor,Guid customer,Guid org,AdminSubscriptionSetRequest r,CancellationToken ct) => ChangeAsync(actor,customer,org,r.RequestId,r.ExpectedVersion,r.Reason,"SubscriptionSet",r,async()=> {
        if(r.Months is not (1 or 3 or 6 or 12) || r.DaysDelta is < -3650 or >3650 || r.StartedAtUtc.Offset!=TimeSpan.Zero) throw AppException.Invalid("invalid_subscription_period");
        var now=time.GetUtcNow(); if(r.StartedAtUtc<now.AddYears(-10) || r.StartedAtUtc>now.AddYears(1)) throw AppException.Invalid("invalid_subscription_start");
        return await Task.FromResult((r.StartedAtUtc,r.StartedAtUtc.AddMonths(r.Months).AddDays(r.DaysDelta),false));
    },ct);
    public Task<AdminCustomerMutationDto> DaysAsync(Guid actor,Guid customer,Guid org,AdminSubscriptionDaysRequest r,CancellationToken ct) => ChangeAsync(actor,customer,org,r.RequestId,r.ExpectedVersion,r.Reason,"SubscriptionDaysAdjusted",r,async()=> {
        if(r.Days is 0 or < -3650 or >3650) throw AppException.Invalid("invalid_day_adjustment");
        var state=await subscriptions.StateAsync(org,ct); var now=time.GetUtcNow();
        if(!state.HasActiveSubscription && r.Days<0) throw AppException.Invalid("no_active_subscription");
        var replacement=await db.CustomerSubscriptionOverrides.AsNoTracking().SingleOrDefaultAsync(x=>x.OrganizationId==org,ct);
        var excluded=replacement?.ExcludedOrderIds ?? [];
        var futureStart=await db.OrganizationMemberships.Where(x=>x.OrganizationId==org && !excluded.Contains(x.OrderId) && x.StartedAtUtc>now)
            .Select(x=>(DateTimeOffset?)x.StartedAtUtc).MinAsync(ct);
        var start=state.StartedAtUtc ?? new[] {futureStart,replacement is {IsStopped:false} && replacement.ExpiresAtUtc>now ? (DateTimeOffset?)replacement.StartedAtUtc : null}.Min() ?? now;
        return (start,(state.ExpiresAtUtc ?? now).AddDays(r.Days),false);
    },ct);
    public Task<AdminCustomerMutationDto> StopAsync(Guid actor,Guid customer,Guid org,AdminSubscriptionStopRequest r,CancellationToken ct) => ChangeAsync(actor,customer,org,r.RequestId,r.ExpectedVersion,r.Reason,"SubscriptionStopped",r,()=>Task.FromResult((time.GetUtcNow(),time.GetUtcNow(),true)),ct);
    private Task<AdminCustomerMutationDto> ChangeAsync(Guid actor,Guid customer,Guid org,Guid request,string expected,string reason,string action,object body,
        Func<Task<(DateTimeOffset Start,DateTimeOffset End,bool Stopped)>> interval,CancellationToken ct) => db.WithOrganizationLockAsync(org,async()=> {
        Validate(request,reason); var hash=Fingerprint(new {Action=action,Request=body});
        var old=await ReplayAsync(customer,request,hash,ct); if(old is not null) return old;
        if(await VersionAsync(org,ct)!=expected) throw new AppException(409,"customer_subscription_changed","Refresh customer subscription before editing.");
        var (start,end,stopped)=await interval();
        if(!stopped && (end<=start || end>time.GetUtcNow().AddYears(11))) throw AppException.Invalid("invalid_subscription_end");
        var before=await subscriptions.StateAsync(org,ct);
        var current=await subscriptions.CurrentAsync(org,ct); var plan=await db.SubscriptionPlans.AsNoTracking().SingleAsync(ct);
        var replacement=await db.CustomerSubscriptionOverrides.SingleOrDefaultAsync(x=>x.OrganizationId==org,ct);
        var e=Event(actor,customer,org,request,hash,action,reason,new {Before=before,StartedAtUtc=start,ExpiresAtUtc=end,IsStopped=stopped});
        if(replacement is null) {replacement=new() {OrganizationId=org};db.CustomerSubscriptionOverrides.Add(replacement);}
        replacement.EventId=e.Id; replacement.StartedAtUtc=start; replacement.ExpiresAtUtc=end; replacement.IsStopped=stopped;
        replacement.ExcludedOrderIds=await db.OrganizationMemberships.Where(x=>x.OrganizationId==org).Select(x=>x.OrderId).ToArrayAsync(ct);
        replacement.TeamUsers=current?.TeamUsers ?? Math.Max(plan.IncludedUsers,await team.OccupiedAsync(org,ct));
        replacement.UnlimitedTeam=current?.UnlimitedTeam ?? false; replacement.RetentionDays=current?.RetentionDays ?? plan.RetentionDays;
        return new AdminCustomerMutationDto(e.Id,false);
    },ct);
}
