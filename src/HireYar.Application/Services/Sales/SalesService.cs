using System.Security.Cryptography;
using System.Text;
using HireYar.Application.Common;
using HireYar.Domain.Sales;
using HireYar.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Sales;

public sealed class SalesService(IApplicationDbContext db, TimeProvider time)
{
    public static void Text(string? text,int max,bool required=true) {
        if (text is null || text.Length>max || text.Contains('\0') || required && string.IsNullOrWhiteSpace(text)) throw AppException.Invalid("invalid_sales_text");
    }
    public static void Rate(decimal rate) { if(rate<0 || rate>100 || decimal.Round(rate,2)!=rate) throw AppException.Invalid("invalid_commission_rate"); }
    public static SalesLeadDto MapLead(SalesLead l,bool won,DateTimeOffset now) => new(l.Id,l.AgentId,l.CustomerName,l.Mobile,l.Note,
        won ? "Won" : l.RegisteredAtUtc is not null ? "Registered" : l.LastContactAtUtc<=now.AddDays(-30) ? "Dead" : "Waiting",
        l.CreatedAtUtc,l.LastContactAtUtc,l.FollowUpAtUtc,l.RegisteredAtUtc);
    public async Task<Page<SalesLeadDto>> LeadsAsync(Guid? agent,SalesLeadFilter filter,PageQuery page,CancellationToken ct) {
        var now=time.GetUtcNow(); var cutoff=now.AddDays(-30);
        var q=db.SalesLeads.AsNoTracking().Where(x=>agent==null || x.AgentId==agent);
        var orders=db.SubscriptionOrders.Where(o=>o.Status=="Confirmed" && o.Product=="subscription");
        if(filter.Status=="Won") q=q.Where(x=>orders.Any(o=>o.OrganizationId==x.OrganizationId));
        if(filter.Status=="Registered") q=q.Where(x=>x.RegisteredAtUtc!=null && !orders.Any(o=>o.OrganizationId==x.OrganizationId));
        if(filter.Status=="Waiting") q=q.Where(x=>x.RegisteredAtUtc==null && x.LastContactAtUtc>cutoff);
        if(filter.Status=="Dead") q=q.Where(x=>x.RegisteredAtUtc==null && x.LastContactAtUtc<=cutoff);
        var count=await q.CountAsync(ct);
        var rows=await q.OrderByDescending(x=>x.CreatedAtUtc).ThenBy(x=>x.Id).Skip(page.Skip).Take(page.PageSize)
            .Select(x=>new { Lead=x, Won=orders.Any(o=>o.OrganizationId==x.OrganizationId) }).ToListAsync(ct);
        return new(rows.Select(x=>MapLead(x.Lead,x.Won,now)).ToArray(),count,page.Page,page.PageSize);
    }
    public Task FollowUpAsync(Guid agent,Guid id,SalesFollowUpRequest r,CancellationToken ct) => db.WithSalesLockAsync(async () => {
        Text(r.Note,2000,false);
        var l=await db.SalesLeads.SingleOrDefaultAsync(x=>x.Id==id && x.AgentId==agent,ct) ?? throw AppException.NotFound();
        l.Note=r.Note.Trim(); l.LastContactAtUtc=time.GetUtcNow(); l.FollowUpAtUtc=r.FollowUpAtUtc?.ToUniversalTime();
        db.SalesAudits.Add(new() { ActorId=agent,LeadId=id,Action="FollowUp",CreatedAtUtc=time.GetUtcNow() }); return true;
    },ct);
    public Task<int> TransferAsync(Guid actor,Guid source,SalesTransferRequest r,CancellationToken ct) => db.WithSalesLockAsync(async () => {
        Text(r.Reason,1000);
        if(source==r.TargetAgentId) throw AppException.Invalid("same_sales_agent");
        if(!await db.SalesAgents.AnyAsync(x=>x.Id==source,ct) || !await db.SalesAgents.AnyAsync(x=>x.Id==r.TargetAgentId && x.IsActive,ct)) throw AppException.NotFound();
        var rows=await db.SalesLeads.Where(x=>x.AgentId==source && (r.IncludeCustomers || x.OrganizationId==null)).ToListAsync(ct);
        foreach(var l in rows) {
            l.AgentId=r.TargetAgentId;
            db.SalesAudits.Add(new() { ActorId=actor,LeadId=l.Id,Action="Transferred:"+source+":"+r.TargetAgentId,Reason=r.Reason.Trim(),CreatedAtUtc=time.GetUtcNow() });
        }
        return rows.Count;
    },ct);
    // Called inside the existing order-confirmation transaction; no backdating or historical rewards.
    public Task AccrueAsync(SubscriptionOrder order,CancellationToken ct) => db.WithSalesLockAsync(async () => {
        if(order.Product!="subscription") return false;
        var lead=await db.SalesLeads.SingleOrDefaultAsync(x=>x.OrganizationId==order.OrganizationId,ct);
        if(lead is null || await db.SalesCommissions.AnyAsync(x=>x.OrderId==order.Id,ct)) return false;
        var agent=await db.SalesAgents.SingleAsync(x=>x.Id==lead.AgentId,ct);
        var renewal=await db.SubscriptionOrders.AnyAsync(x=>x.OrganizationId==order.OrganizationId && x.Id!=order.Id && x.Product=="subscription" && x.Status=="Confirmed",ct);
        var percent=renewal ? agent.RenewalPercent : agent.FirstSalePercent;
        var now=order.ConfirmedAtUtc!.Value;
        db.SalesCommissions.Add(new() { AgentId=agent.Id,LeadId=lead.Id,OrganizationId=order.OrganizationId,OrderId=order.Id,
            Kind=renewal ? "Renewal" : "FirstSale",InvoiceAmountRial=order.AmountRial,Percent=percent,
            AmountRial=checked((long)decimal.Floor(order.AmountRial*(percent/100m))),CreatedAtUtc=now,AvailableAtUtc=now.AddDays(7) });
        return true;
    },ct);
    private static void ValidateFilter(SalesCommissionFilter f) {
        if(f.RegisteredFromUtc?.Offset!=null && f.RegisteredFromUtc.Value.Offset!=TimeSpan.Zero || f.RegisteredToUtc?.Offset!=null && f.RegisteredToUtc.Value.Offset!=TimeSpan.Zero ||
            f.RegisteredFromUtc>=f.RegisteredToUtc) throw AppException.Invalid("invalid_registration_range");
    }
    public async Task<SalesCommissionPage> CommissionsAsync(Guid? agent,SalesCommissionFilter f,PageQuery page,CancellationToken ct) {
        ValidateFilter(f); var now=time.GetUtcNow();
        var q=from c in db.SalesCommissions.AsNoTracking()
              join l in db.SalesLeads.AsNoTracking() on c.LeadId equals l.Id
              join p in db.SalesPayouts.AsNoTracking() on c.PayoutId equals p.Id into payouts
              from p in payouts.DefaultIfEmpty()
              where (agent==null || c.AgentId==agent) && (f.LeadId==null || c.LeadId==f.LeadId) && (f.PayoutId==null || c.PayoutId==f.PayoutId) && (f.RegisteredFromUtc==null || l.RegisteredAtUtc>=f.RegisteredFromUtc) && (f.RegisteredToUtc==null || l.RegisteredAtUtc<f.RegisteredToUtc)
              select new { C=c,L=l, Status=p!=null ? p.Status : c.AvailableAtUtc>now ? "Holding" : "Available" };
        if(f.Status is not null) q=q.Where(x=>x.Status==f.Status);
        var count=await q.CountAsync(ct);
        var total=await q.SumAsync(x=>x.C.AmountRial,ct);
        var available=await q.Where(x=>x.Status=="Available" && x.C.AmountRial>0).SumAsync(x=>x.C.AmountRial,ct);
        var requested=await q.Where(x=>x.Status=="Requested").SumAsync(x=>x.C.AmountRial,ct);
        var paid=await q.Where(x=>x.Status=="Paid").SumAsync(x=>x.C.AmountRial,ct);
        var rows=await q.OrderByDescending(x=>x.C.CreatedAtUtc).ThenBy(x=>x.C.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new(new(rows.Select(x=>new SalesCommissionDto(x.C.Id,x.C.AgentId,x.C.LeadId,x.C.OrderId,x.L.CustomerName,x.L.RegisteredAtUtc!.Value,
            x.C.Kind,x.C.InvoiceAmountRial,x.C.Percent,x.C.AmountRial,x.C.CreatedAtUtc,x.C.AvailableAtUtc,x.Status,x.C.PayoutId,x.Status=="Available" && x.C.AmountRial>0)).ToArray(),count,page.Page,page.PageSize),"IRR",total,available,requested,paid);
    }
    private async Task<SalesPayoutDto> MapPayout(SalesPayout p,CancellationToken ct) => new(p.Id,p.AgentId,
        await db.SalesAgents.Where(x=>x.Id==p.AgentId).Select(x=>x.FullName).SingleAsync(ct),p.AmountRial,p.Status,p.CreatedAtUtc,p.ReviewedAtUtc,p.ReviewNote);
    public Task<SalesPayoutDto> RequestPayoutAsync(Guid agent,SalesPayoutRequest r,CancellationToken ct) => db.WithSalesLockAsync(async () => {
        if(r.RequestId==Guid.Empty || r.CommissionIds is null || r.CommissionIds.Length is <1 or >100 || r.CommissionIds.Distinct().Count()!=r.CommissionIds.Length) throw AppException.Invalid();
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(',',r.CommissionIds.Order()))));
        var old=await db.SalesPayouts.SingleOrDefaultAsync(x=>x.AgentId==agent && x.RequestId==r.RequestId,ct);
        if(old is not null) {
            if(old.Fingerprint!=fingerprint) throw new AppException(409,"idempotency_conflict","Reuse request only for the same commissions.");
            return await MapPayout(old,ct);
        }
        var rows=await db.SalesCommissions.Where(x=>x.AgentId==agent && r.CommissionIds.Contains(x.Id)).ToListAsync(ct);
        if(rows.Count!=r.CommissionIds.Length) throw AppException.NotFound();
        var now=time.GetUtcNow();
        if(rows.Any(x=>x.PayoutId!=null || x.AvailableAtUtc>now || x.AmountRial<=0)) throw new AppException(409,"commission_unavailable","Commission is holding or already requested.");
        var payout=new SalesPayout { AgentId=agent,RequestId=r.RequestId,Fingerprint=fingerprint,AmountRial=rows.Sum(x=>x.AmountRial),CreatedAtUtc=now };
        db.SalesPayouts.Add(payout); foreach(var row in rows) row.PayoutId=payout.Id;
        db.SalesAudits.Add(new() { ActorId=agent,Action="PayoutRequested:"+payout.Id,CreatedAtUtc=now });
        return await MapPayout(payout,ct);
    },ct);
    public async Task<Page<SalesPayoutDto>> PayoutsAsync(Guid? agent,PageQuery page,CancellationToken ct) {
        var q=db.SalesPayouts.AsNoTracking().Where(x=>agent==null || x.AgentId==agent); var count=await q.CountAsync(ct);
        var rows=await q.OrderByDescending(x=>x.CreatedAtUtc).ThenBy(x=>x.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        var result=new List<SalesPayoutDto>(); foreach(var p in rows) result.Add(await MapPayout(p,ct));
        return new(result,count,page.Page,page.PageSize);
    }
    public Task<SalesPayoutDto> ReviewAsync(Guid actor,Guid id,SalesPayoutReviewRequest r,CancellationToken ct) => db.WithSalesLockAsync(async () => {
        Text(r.Note,1000);
        var p=await db.SalesPayouts.SingleOrDefaultAsync(x=>x.Id==id,ct) ?? throw AppException.NotFound();
        var status=r.Paid ? "Paid" : "Returned";
        if(p.Status!="Requested") {
            if(p.Status==status && p.ReviewNote==r.Note.Trim()) return await MapPayout(p,ct);
            throw new AppException(409,"payout_already_reviewed","Request already reviewed.");
        }
        p.Status=status; p.ReviewNote=r.Note.Trim(); p.ReviewedAtUtc=time.GetUtcNow(); p.ReviewedById=actor;
        db.SalesAudits.Add(new() { ActorId=actor,Action="Payout"+status+":"+p.Id,Reason=r.Note.Trim(),CreatedAtUtc=time.GetUtcNow() });
        return await MapPayout(p,ct);
    },ct);
    public async Task<Page<SalesAuditDto>> AuditsAsync(PageQuery page,CancellationToken ct) {
        var q=db.SalesAudits.AsNoTracking(); var count=await q.CountAsync(ct);
        return new(await q.OrderByDescending(x=>x.CreatedAtUtc).ThenBy(x=>x.Id).Skip(page.Skip).Take(page.PageSize)
            .Select(x=>new SalesAuditDto(x.Id,x.ActorId,x.LeadId,x.Action,x.Reason,x.CreatedAtUtc)).ToListAsync(ct),count,page.Page,page.PageSize);
    }
    public async Task<SalesStatsDto> StatsAsync(Guid? agent,CancellationToken ct) {
        var now=time.GetUtcNow(); var cutoff=now.AddDays(-30); var month=new DateTimeOffset(now.Year,now.Month,1,0,0,0,TimeSpan.Zero);
        var q=db.SalesLeads.AsNoTracking().Where(x=>agent==null || x.AgentId==agent);
        var won=q.Where(x=>db.SubscriptionOrders.Any(o=>o.OrganizationId==x.OrganizationId && o.Product=="subscription" && o.Status=="Confirmed"));
        return new(await q.CountAsync(x=>x.CreatedAtUtc>=cutoff,ct),await q.CountAsync(x=>x.RegisteredAtUtc==null && x.LastContactAtUtc>cutoff,ct),
            await q.CountAsync(x=>x.RegisteredAtUtc!=null && !db.SubscriptionOrders.Any(o=>o.OrganizationId==x.OrganizationId && o.Product=="subscription" && o.Status=="Confirmed"),ct),
            await won.CountAsync(ct),await q.CountAsync(x=>x.RegisteredAtUtc==null && x.LastContactAtUtc<=cutoff,ct),
            await db.SalesCommissions.Where(x=>(agent==null || x.AgentId==agent) && x.CreatedAtUtc>=month).SumAsync(x=>x.AmountRial,ct),
            agent==null ? await db.SalesAgents.CountAsync(x=>x.IsActive,ct) : 0,await db.SalesPayouts.CountAsync(x=>(agent==null || x.AgentId==agent) && x.Status=="Requested",ct));
    }
}

