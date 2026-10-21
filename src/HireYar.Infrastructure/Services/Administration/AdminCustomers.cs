using HireYar.Application.Administration;
using HireYar.Application.Common;
using HireYar.Application.Subscriptions;
using HireYar.Domain.Organizations;
using HireYar.Infrastructure.Auth;
using HireYar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Infrastructure.Administration;

public sealed partial class AdminCustomers(AppDbContext db, UserManager<ApplicationUser> users,
    SubscriptionService subscriptions, CustomerSubscriptionAdministration administration, TimeProvider time) : IAdminCustomers
{
    private sealed class CustomerRow
    {
        public Guid Id {get;set;} public string Kind {get;set;}="User"; public Guid? OrganizationId {get;set;} public Guid? LeadId {get;set;}
        public string FullName {get;set;}=""; public string OrganizationName {get;set;}=""; public string? Mobile {get;set;} public string? Email {get;set;}
        public bool IsActive {get;set;} public bool IsSystemAdmin {get;set;} public DateTimeOffset CreatedAtUtc {get;set;} public DateTimeOffset? RegisteredAtUtc {get;set;}
        public Guid? AgentId {get;set;} public string? AgentName {get;set;} public string? UserStamp {get;set;} public DateTimeOffset? OrganizationUpdatedAtUtc {get;set;}
        public DateTimeOffset? LastContactAtUtc {get;set;}
    }
    private sealed class CustomerReadRow
    {
        public CustomerRow Customer {get;set;}=null!; public bool HasPaid {get;set;} public bool Active {get;set;} public bool Trial {get;set;}
        public bool HasHistory {get;set;} public bool Stopped {get;set;} public bool HasReplacement {get;set;}
        public DateTimeOffset? PaidEnd {get;set;} public DateTimeOffset? PaidStart {get;set;}
        public DateTimeOffset? TrialEnd {get;set;} public DateTimeOffset? TrialStart {get;set;}
        public DateTimeOffset? GiftEnd {get;set;} public DateTimeOffset? GiftStart {get;set;}
        public DateTimeOffset? OverrideEnd {get;set;} public DateTimeOffset? OverrideStart {get;set;}
        public long TotalInterviews {get;set;} public int Consumed {get;set;} public int Reserved {get;set;} public string? BundleName {get;set;}
    }
    private IQueryable<CustomerRow> BaseRows()
    {
        var registered=from u in db.Users.AsNoTracking() join o in db.Organizations.AsNoTracking() on u.OrganizationId equals o.Id
            join l in db.SalesLeads.AsNoTracking() on (Guid?)o.Id equals l.OrganizationId into leads from l in leads.DefaultIfEmpty()
            join a in db.SalesAgents.AsNoTracking() on l.AgentId equals (Guid?)a.Id into agents from a in agents.DefaultIfEmpty()
            where u.Role==OrganizationRole.Owner
            select new CustomerRow {Id=u.Id,Kind="User",OrganizationId=o.Id,LeadId=l==null ? null : l.Id,FullName=u.FullName,OrganizationName=o.Name,
                Mobile=u.PhoneNumber,Email=u.Email,IsActive=u.IsActive,IsSystemAdmin=u.IsSystemAdmin,CreatedAtUtc=u.CreatedAtUtc,RegisteredAtUtc=u.CreatedAtUtc,
                AgentId=l==null ? null : l.AgentId,AgentName=a==null ? null : a.FullName,UserStamp=u.ConcurrencyStamp,OrganizationUpdatedAtUtc=o.UpdatedAtUtc,
                LastContactAtUtc=l==null ? null : l.LastContactAtUtc};
        var waiting=from l in db.SalesLeads.AsNoTracking()
            join a in db.SalesAgents.AsNoTracking() on l.AgentId equals (Guid?)a.Id into agents from a in agents.DefaultIfEmpty()
            where l.OrganizationId==null
            select new CustomerRow {Id=l.Id,Kind="Lead",OrganizationId=null,LeadId=l.Id,FullName=l.CustomerName,OrganizationName=l.ContactOrganizationName,
                Mobile=l.Mobile,Email=l.ContactEmail,IsActive=true,IsSystemAdmin=false,CreatedAtUtc=l.CreatedAtUtc,RegisteredAtUtc=null,
                AgentId=l.AgentId,AgentName=a==null ? null : a.FullName,UserStamp=null,OrganizationUpdatedAtUtc=null,LastContactAtUtc=l.LastContactAtUtc};
        return registered.Concat(waiting);
    }
    private IQueryable<CustomerReadRow> ReadRows(AdminCustomerQuery filter)
    {
        var now=time.GetUtcNow(); var rows=BaseRows();
        if(!string.IsNullOrWhiteSpace(filter.Search)) {
            var search=NormalizeDigits(filter.Search.Trim()).ToLowerInvariant();
            rows=rows.Where(x=>x.FullName.ToLower().Contains(search) || x.OrganizationName.ToLower().Contains(search) ||
                x.Mobile!=null && x.Mobile.Contains(search) || x.Email!=null && x.Email.ToLower().Contains(search));
        }
        if(filter.AgentId is not null) rows=rows.Where(x=>x.AgentId==filter.AgentId);
        if(filter.HasSeller is not null) rows=rows.Where(x=>(x.AgentId!=null)==filter.HasSeller);
        var paid=subscriptions.EffectiveMemberships().Where(m=>!db.CustomerSubscriptionOverrides.Any(c=>c.OrganizationId==m.OrganizationId && c.ExcludedOrderIds.Contains(m.OrderId)));
        return from r in rows
            join c in db.CustomerSubscriptionOverrides.AsNoTracking() on r.OrganizationId equals (Guid?)c.OrganizationId into replacements from c in replacements.DefaultIfEmpty()
            select new CustomerReadRow {Customer=r,
                HasPaid=db.SubscriptionOrders.Any(x=>x.OrganizationId==r.OrganizationId && x.Product=="subscription" && x.Status=="Confirmed"),
                HasHistory=c!=null || db.OrganizationMemberships.Any(x=>x.OrganizationId==r.OrganizationId) || db.SignupTrials.Any(x=>x.OrganizationId==r.OrganizationId) || db.ExistingOrganizationGrants.Any(x=>x.OrganizationId==r.OrganizationId),
                HasReplacement=c!=null,Stopped=c!=null && c.IsStopped && !paid.Any(m=>m.OrganizationId==r.OrganizationId && m.StartedAtUtc<=now && m.ExpiresAtUtc>now),
                Active=paid.Any(m=>m.OrganizationId==r.OrganizationId && m.StartedAtUtc<=now && m.ExpiresAtUtc>now) ||
                    c!=null && !c.IsStopped && c.StartedAtUtc<=now && c.ExpiresAtUtc>now ||
                    c==null && (db.SignupTrials.Any(t=>t.OrganizationId==r.OrganizationId && t.StartedAtUtc<=now && t.ExpiresAtUtc>now) || db.ExistingOrganizationGrants.Any(g=>g.OrganizationId==r.OrganizationId && g.StartedAtUtc<=now && g.ExpiresAtUtc>now)),
                Trial=c==null && !paid.Any(m=>m.OrganizationId==r.OrganizationId && m.StartedAtUtc<=now && m.ExpiresAtUtc>now) &&
                    !db.ExistingOrganizationGrants.Any(g=>g.OrganizationId==r.OrganizationId && g.StartedAtUtc<=now && g.ExpiresAtUtc>now) &&
                    db.SignupTrials.Any(t=>t.OrganizationId==r.OrganizationId && t.StartedAtUtc<=now && t.ExpiresAtUtc>now),
                PaidEnd=paid.Where(m=>m.OrganizationId==r.OrganizationId).Select(m=>(DateTimeOffset?)m.ExpiresAtUtc).Max(),
                PaidStart=paid.Where(m=>m.OrganizationId==r.OrganizationId && m.StartedAtUtc<=now && m.ExpiresAtUtc>now).OrderByDescending(m=>m.StartedAtUtc).Select(m=>(DateTimeOffset?)m.StartedAtUtc).FirstOrDefault(),
                TrialEnd=c==null ? db.SignupTrials.Where(t=>t.OrganizationId==r.OrganizationId).Select(t=>(DateTimeOffset?)t.ExpiresAtUtc).SingleOrDefault() : null,
                TrialStart=c==null ? db.SignupTrials.Where(t=>t.OrganizationId==r.OrganizationId).Select(t=>(DateTimeOffset?)t.StartedAtUtc).SingleOrDefault() : null,
                GiftEnd=c==null ? db.ExistingOrganizationGrants.Where(g=>g.OrganizationId==r.OrganizationId).Select(g=>(DateTimeOffset?)g.ExpiresAtUtc).SingleOrDefault() : null,
                GiftStart=c==null ? db.ExistingOrganizationGrants.Where(g=>g.OrganizationId==r.OrganizationId).Select(g=>(DateTimeOffset?)g.StartedAtUtc).SingleOrDefault() : null,
                OverrideEnd=c!=null && !c.IsStopped ? c.ExpiresAtUtc : null,OverrideStart=c!=null && !c.IsStopped ? c.StartedAtUtc : null,
                TotalInterviews=db.InterviewQuotaGrants.Where(x=>x.OrganizationId==r.OrganizationId).Sum(x=>(long)x.Quantity)+
                    db.SignupTrials.Where(x=>x.OrganizationId==r.OrganizationId).Sum(x=>(long)x.InterviewQuantity)+
                    db.ExistingOrganizationGrants.Where(x=>x.OrganizationId==r.OrganizationId).Sum(x=>(long)x.InterviewQuantity)+
                    db.CustomerAdministrationEvents.Where(x=>x.OrganizationId==r.OrganizationId).Sum(x=>(long)x.CreditQuantity),
                Consumed=db.InterviewQuotaReservations.Count(x=>x.OrganizationId==r.OrganizationId && x.State=="Consumed"),
                Reserved=(from quota in db.InterviewQuotaReservations join session in db.InterviewSessions on quota.InterviewSessionId equals session.Id
                    where quota.OrganizationId==r.OrganizationId && session.OrganizationId==r.OrganizationId && quota.State=="Reserved" && session.Status==HireYar.Domain.Interviews.InterviewStatus.Created && session.ExpiresAtUtc>now && session.AccessRevokedAtUtc==null && session.DeletedAtUtc==null select quota.Id).Count(),
                BundleName=db.SubscriptionOrders.Where(x=>x.OrganizationId==r.OrganizationId && x.Product=="interviews" && x.Status=="Confirmed").OrderByDescending(x=>x.ConfirmedAtUtc).ThenBy(x=>x.Id).Select(x=>x.ProductName).FirstOrDefault()
            };
    }
    private static string NormalizeDigits(string value) => string.Concat(value.Select(c=>c is >= '۰' and <= '۹' ? (char)('0'+c-'۰') : c is >= '٠' and <= '٩' ? (char)('0'+c-'٠') : c));
    private static string ProfileVersion(CustomerRow r)=>CustomerSubscriptionAdministration.Fingerprint(new {r.Id,r.Kind,r.FullName,r.OrganizationName,r.Mobile,r.Email,r.AgentId,r.UserStamp,r.OrganizationUpdatedAtUtc,r.LastContactAtUtc});
    private AdminCustomerDto Map(CustomerReadRow r)
    {
        var c=r.Customer;var now=time.GetUtcNow();
        var end=new[]{r.PaidEnd,r.TrialEnd,r.GiftEnd,r.OverrideEnd}.Max();
        var start=r.PaidStart ?? (r.OverrideStart<=now && r.OverrideEnd>now ? r.OverrideStart : null) ??
            (r.GiftStart<=now && r.GiftEnd>now ? r.GiftStart : null) ?? (r.TrialStart<=now && r.TrialEnd>now ? r.TrialStart : null);
        var status=c.OrganizationId==null ? "None" : r.Active ? r.Trial ? "Trial" : "Active" : r.Stopped ? "Stopped" : end>now ? "Scheduled" : r.HasHistory ? "Expired" : "None";
        return new(c.Id,c.Kind,c.OrganizationId,c.LeadId,c.FullName,c.OrganizationName,c.Mobile,c.Email,c.IsActive,c.CreatedAtUtc,c.RegisteredAtUtc,
            c.OrganizationId==null ? "Waiting" : r.HasPaid ? "Won" : "Registered",c.AgentId,c.AgentName,status,start,end,
            !r.Active || end==null ? 0 : (int)Math.Ceiling((end.Value-now).TotalDays),r.TotalInterviews-r.Consumed,r.TotalInterviews-r.Consumed-r.Reserved,r.Reserved,r.Consumed,r.BundleName,ProfileVersion(c));
    }
    private static IQueryable<CustomerReadRow> FilterStatus(IQueryable<CustomerReadRow> q,string? status,DateTimeOffset now) => status switch {
        "Won"=>q.Where(x=>x.HasPaid),"Registered"=>q.Where(x=>x.Customer.OrganizationId!=null && !x.HasPaid),
        "Waiting"=>q.Where(x=>x.Customer.OrganizationId==null),
        "Expired"=>q.Where(x=>!x.Active && !x.Stopped && x.HasHistory && !(x.PaidEnd>now || x.TrialEnd>now || x.GiftEnd>now || x.OverrideEnd>now)),
        "Stopped"=>q.Where(x=>x.Stopped),
        "Expiring"=>q.Where(x=>x.Active && !(x.PaidEnd>now.AddDays(7) || x.TrialEnd>now.AddDays(7) || x.GiftEnd>now.AddDays(7) || x.OverrideEnd>now.AddDays(7))),_=>q
    };
    public async Task<AdminCustomerPage> ListAsync(AdminCustomerQuery filter,CancellationToken ct)
    {
        var q=ReadRows(filter);var now=time.GetUtcNow();
        var stats=new AdminCustomerStats(await q.CountAsync(ct),await FilterStatus(q,"Won",now).CountAsync(ct),await FilterStatus(q,"Registered",now).CountAsync(ct),
            await FilterStatus(q,"Waiting",now).CountAsync(ct),await FilterStatus(q,"Expired",now).CountAsync(ct),await q.CountAsync(x=>x.Customer.AgentId==null,ct),await FilterStatus(q,"Expiring",now).CountAsync(ct));
        var filtered=FilterStatus(q,filter.Status,now);var total=await filtered.CountAsync(ct);
        var rows=await filtered.OrderByDescending(x=>x.Customer.CreatedAtUtc).ThenBy(x=>x.Customer.Id).Skip(filter.Skip).Take(filter.PageSize).ToListAsync(ct);
        return new(new(rows.Select(Map).ToArray(),total,filter.Page,filter.PageSize),stats);
    }
    public async Task<AdminCustomerDetail> GetAsync(Guid id,CancellationToken ct)
    {
        var r=await ReadRows(new()).SingleOrDefaultAsync(x=>x.Customer.Id==id,ct) ?? throw AppException.NotFound();
        var org=r.Customer.OrganizationId;
        return new(Map(r),org==null ? null : await subscriptions.StateAsync(org.Value,ct),org==null ? null : await administration.VersionAsync(org.Value,ct),org!=null,!r.Customer.IsSystemAdmin);
    }
    public async Task<Guid> OrganizationAsync(Guid id,CancellationToken ct)
    {
        var org=await db.Users.AsNoTracking().Where(x=>x.Id==id && x.Role==OrganizationRole.Owner).Select(x=>(Guid?)x.OrganizationId).SingleOrDefaultAsync(ct);
        if(org is not null) return org.Value;
        if(await db.SalesLeads.AnyAsync(x=>x.Id==id && x.OrganizationId==null,ct)) throw new AppException(409,"customer_not_registered","A verified registered customer is required.");
        throw AppException.NotFound();
    }
    public async Task<byte[]> ExportAsync(AdminCustomerQuery query,CancellationToken ct)
    {
        var q=FilterStatus(ReadRows(query),query.Status,time.GetUtcNow());
        var rows=await q.OrderByDescending(x=>x.Customer.CreatedAtUtc).ThenBy(x=>x.Customer.Id).Take(10001).ToListAsync(ct);
        if(rows.Count>10000) throw new AppException(413,"export_too_large","Limit the export using search or filters (maximum 10000 rows).");
        return CustomerWorkbook.Create(rows.Select(Map));
    }
}
