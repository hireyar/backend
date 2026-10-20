using HireYar.Application.Administration;
using HireYar.Application.Common;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Infrastructure.Administration;
public sealed partial class AdminCustomers
{
    private sealed class HistoryRow
    {
        public Guid Id {get;set;} public string Source {get;set;}=""; public string Action {get;set;}=""; public DateTimeOffset OccurredAtUtc {get;set;}
        public Guid? ActorId {get;set;} public string Description {get;set;}=""; public long? AmountRial {get;set;} public int? Quantity {get;set;} public string? DetailsJson {get;set;}
    }
    public async Task<Page<AdminCustomerHistoryDto>> HistoryAsync(Guid id,PageQuery page,CancellationToken ct)
    {
        var customer=await RowAsync(id,ct);var org=customer.OrganizationId;var lead=customer.LeadId;
        var audit=db.CustomerAdministrationEvents.AsNoTracking().Where(x=>x.CustomerId==id || lead!=null && x.CustomerId==lead && x.CustomerKind=="Lead")
            .Select(x=>new HistoryRow {Id=x.Id,Source="Administration",Action=x.Action,OccurredAtUtc=x.CreatedAtUtc,ActorId=x.ActorId,Description=x.Reason,AmountRial=null,Quantity=x.CreditQuantity>0 ? x.CreditQuantity : null,DetailsJson=x.DetailsJson});
        var orders=db.SubscriptionOrders.AsNoTracking().Where(x=>org!=null && x.OrganizationId==org)
            .Select(x=>new HistoryRow {Id=x.Id,Source="Order",Action=x.Product+":"+x.Status,OccurredAtUtc=x.ConfirmedAtUtc ?? x.CreatedAtUtc,
                ActorId=x.ConfirmedByUserId,Description=x.ProductName,AmountRial=x.AmountRial,Quantity=x.Product=="interviews" ? x.Quantity : null,DetailsJson=null});
        var calls=db.SalesAudits.AsNoTracking().Where(x=>lead!=null && x.LeadId==lead)
            .Select(x=>new HistoryRow {Id=x.Id,Source="Sales",Action=x.Action,OccurredAtUtc=x.CreatedAtUtc,ActorId=x.ActorId,Description=x.Reason,AmountRial=null,Quantity=null,DetailsJson=null});
        var signup=db.Users.AsNoTracking().Where(x=>customer.Kind=="User" && x.Id==id)
            .Select(x=>new HistoryRow {Id=x.Id,Source="Registration",Action="Registered",OccurredAtUtc=x.CreatedAtUtc,ActorId=null,Description="ثبت حساب سازمانی",AmountRial=null,Quantity=null,DetailsJson=null});
        var trial=db.SignupTrials.AsNoTracking().Where(x=>org!=null && x.OrganizationId==org)
            .Select(x=>new HistoryRow {Id=x.OrganizationId,Source="SignupTrial",Action="GiftGranted",OccurredAtUtc=x.StartedAtUtc,ActorId=null,Description="هدیه ثبت‌نام ۴۸ساعته",AmountRial=null,Quantity=x.InterviewQuantity,DetailsJson=null});
        var gift=db.ExistingOrganizationGrants.AsNoTracking().Where(x=>org!=null && x.OrganizationId==org)
            .Select(x=>new HistoryRow {Id=x.OrganizationId,Source="ExistingOrganizationGrant",Action="GiftGranted",OccurredAtUtc=x.StartedAtUtc,ActorId=null,Description="هدیه سازمان‌های موجود",AmountRial=null,Quantity=x.InterviewQuantity,DetailsJson=null});
        var q=audit.Concat(orders).Concat(calls).Concat(signup).Concat(trial).Concat(gift);
        var total=await q.CountAsync(ct);var rows=await q.OrderByDescending(x=>x.OccurredAtUtc).ThenBy(x=>x.Source).ThenBy(x=>x.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new(rows.Select(x=>new AdminCustomerHistoryDto(x.Id,x.Source,x.Action,x.OccurredAtUtc,x.ActorId,x.Description,x.AmountRial,x.Quantity,x.DetailsJson)).ToArray(),total,page.Page,page.PageSize);
    }
}
