using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Auth;
using HireYar.Application.Common;
using HireYar.Application.Sales;
using HireYar.Application.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Text.Json;
namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class SalesTests : IAsyncLifetime
{
    private readonly ApiFixture f=new();
    private Account? admin;
    public Task InitializeAsync()=>f.InitializeAsync();
    public Task DisposeAsync()=>((IAsyncLifetime)f).DisposeAsync();
    private async Task<Account> Admin()=>admin ??= await f.RegisterAdmin();
    private static async Task<T> Read<T>(HttpResponseMessage r) {
        Assert.True(r.StatusCode==HttpStatusCode.OK, $"{r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
        return (await r.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }
    private async Task<(Browser Client,SalesCredentialsDto Credentials)> Agent() {
        var a=await Admin();
        var mobile="09"+Random.Shared.NextInt64(100_000_000,1_000_000_000).ToString();
        var c=await Read<SalesCredentialsDto>(await a.Client.Send(HttpMethod.Post,"/api/admin/sales/agents",new SalesAgentRequest("فروشنده آزمایشی",mobile)));
        var b=f.Browser(); b.AccessToken=(await Read<SalesSessionDto>(await b.Send(HttpMethod.Post,"/api/sales/auth/login",new SalesLoginRequest(mobile,c.Password)))).AccessToken;
        return (b,c);
    }
    private async Task<SubscriptionOrderDto> Buy(Account owner,string product="subscription") {
        var selection=product=="subscription" ? new PurchaseSelection("subscription") : new PurchaseSelection("interviews",BundleCode:"dana");
        var quote=await Read<PurchaseQuote>(await owner.Client.Send(HttpMethod.Post,"/api/subscriptions/catalog/quote",selection));
        var order=await Read<SubscriptionOrderDto>(await owner.Client.Send(HttpMethod.Post,"/api/subscriptions/orders",new CreateSubscriptionOrderRequest(Guid.NewGuid(),selection,quote.PlanVersion,quote.BundleVersion)));
        var a=await Admin();
        return await Read<SubscriptionOrderDto>(await a.Client.Send(HttpMethod.Post,$"/api/admin/subscriptions/orders/{order.Id}/confirm",new ConfirmSubscriptionOrderRequest("synthetic-"+order.Id)));
    }
    private async Task<SalesCommissionPage> Commissions(Browser b,string query="")=>await Read<SalesCommissionPage>(await b.Send(HttpMethod.Get,"/api/sales/commissions"+query));
    [PostgresFact]
    public async Task Signup_attribution_purchase_hold_filtered_totals_payout_race_and_renewal_are_atomic() {
        var seller=await Agent(); var second=await Agent(); var a=await Admin();
        var mobile="09"+Random.Shared.NextInt64(100_000_000,1_000_000_000).ToString();
        var lead=await Read<SalesLeadDto>(await seller.Client.Send(HttpMethod.Post,"/api/sales/leads",new SalesLeadRequest(mobile,"مشتری مصنوعی","تماس اول")));
        Assert.Equal("Waiting",lead.Status);
        var b=f.Browser(); await b.Send(HttpMethod.Post,"/api/auth/otp/request",new RequestOtpRequest(mobile));
        var signup=await Read<AccessResponse>(await b.Send(HttpMethod.Post,"/api/auth/register",ApiFixture.Registration(mobile,f.PhoneOtp.Codes[mobile]))); b.AccessToken=signup.AccessToken;
        var customer=new Account(b,mobile,Guid.Empty);
        var registered=await Read<Page<SalesLeadDto>>(await seller.Client.Send(HttpMethod.Get,"/api/sales/leads?status=Registered"));
        Assert.Equal(lead.Id,Assert.Single(registered.Items).Id); Assert.NotNull(registered.Items[0].RegisteredAtUtc);
        var trial=await Read<SubscriptionStateDto>(await b.Send(HttpMethod.Get,"/api/subscriptions/current")); Assert.True(trial.IsTrial); Assert.Equal(2,trial.AvailableInterviews);
        var order=await Buy(customer); await Buy(customer,"interviews");
        var rows=await Commissions(seller.Client); var first=Assert.Single(rows.Rows.Items); Assert.Equal(18_000_000,first.AmountRial); Assert.Equal("Holding",first.Status);
        Assert.Equal(0,(await Commissions(second.Client)).Rows.Total);
        Assert.Equal(HttpStatusCode.Conflict,(await seller.Client.Send(HttpMethod.Post,"/api/sales/payouts",new SalesPayoutRequest(Guid.NewGuid(),[first.Id]))).StatusCode);
        await a.Client.Send(HttpMethod.Post,$"/api/admin/subscriptions/orders/{order.Id}/confirm",new ConfirmSubscriptionOrderRequest("synthetic-"+order.Id));
        Assert.Equal(1,(await Commissions(seller.Client)).Rows.Total);
        f.Clock.Offset=TimeSpan.FromDays(8);
        rows=await Commissions(seller.Client); Assert.Equal(18_000_000,rows.AvailableAmountRial); Assert.True(rows.Rows.Items[0].CanRequest);
        var registeredAt=registered.Items[0].RegisteredAtUtc!.Value;
        var from=Uri.EscapeDataString(registeredAt.AddMinutes(-1).ToString("O")); var to=Uri.EscapeDataString(registeredAt.AddMinutes(1).ToString("O"));
        Assert.Equal(18_000_000,(await Commissions(seller.Client,$"?registeredFromUtc={from}&registeredToUtc={to}&pageSize=1")).TotalAmountRial);
        Assert.Equal(0,(await Commissions(seller.Client,$"?registeredFromUtc={Uri.EscapeDataString(registeredAt.AddDays(1).ToString("O"))}")).Rows.Total);
        var requests=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>seller.Client.Send(HttpMethod.Post,"/api/sales/payouts",new SalesPayoutRequest(Guid.NewGuid(),[first.Id]))));
        var paidRequest=await Read<SalesPayoutDto>(Assert.Single(requests,x=>x.StatusCode==HttpStatusCode.OK)); Assert.Single(requests,x=>x.StatusCode==HttpStatusCode.Conflict);
        Assert.Equal("Requested",(await Commissions(seller.Client)).Rows.Items[0].Status);
        Assert.Equal(first.Id,Assert.Single((await Commissions(seller.Client,"?payoutId="+paidRequest.Id)).Rows.Items).Id);
        await Read<SalesPayoutDto>(await a.Client.Send(HttpMethod.Post,$"/api/admin/sales/payouts/{paidRequest.Id}/review",new SalesPayoutReviewRequest(true,"رسید آزمایشی")));
        Assert.Equal("Paid",(await Commissions(seller.Client)).Rows.Items[0].Status);
        var renewalOrder=await Buy(customer); rows=await Commissions(seller.Client); Assert.Equal(2,rows.Rows.Total); Assert.Equal(22_500_000,rows.TotalAmountRial);
        var renewal=Assert.Single(rows.Rows.Items,x=>x.Kind=="Renewal"); Assert.Equal(4_500_000,renewal.AmountRial);
        f.Clock.Offset=TimeSpan.FromDays(16);
        var renewalRequest=new SalesPayoutRequest(Guid.NewGuid(),[renewal.Id]);
        var repeated=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>seller.Client.Send(HttpMethod.Post,"/api/sales/payouts",renewalRequest)));
        var p1=await Read<SalesPayoutDto>(repeated[0]); var p2=await Read<SalesPayoutDto>(repeated[1]); Assert.Equal(p1.Id,p2.Id);
        await Read<SalesPayoutDto>(await a.Client.Send(HttpMethod.Post,$"/api/admin/sales/payouts/{p1.Id}/review",new SalesPayoutReviewRequest(false,"نیاز به بررسی")));
        Assert.Equal(HttpStatusCode.Conflict,(await seller.Client.Send(HttpMethod.Post,"/api/sales/payouts",new SalesPayoutRequest(Guid.NewGuid(),[renewal.Id]))).StatusCode);
        Assert.Equal(2,(await Read<Page<SalesPayoutDto>>(await seller.Client.Send(HttpMethod.Get,"/api/sales/payouts"))).Total);
    }
    [PostgresFact]
    public async Task Concurrent_claims_scope_dead_restore_existing_customer_and_transfer_preserve_history() {
        var s1=await Agent(); var s2=await Agent(); var a=await Admin();
        var customer=await f.RegisterOwner(provisionMembership:false);
        var race=await Task.WhenAll(s1.Client.Send(HttpMethod.Post,"/api/sales/leads",new SalesLeadRequest(customer.Mobile,"مشتری")),s2.Client.Send(HttpMethod.Post,"/api/sales/leads",new SalesLeadRequest(customer.Mobile,"مشتری")));
        var claimed=await Read<SalesLeadDto>(Assert.Single(race,x=>x.StatusCode==HttpStatusCode.OK)); Assert.Single(race,x=>x.StatusCode==HttpStatusCode.Conflict); Assert.Equal("Registered",claimed.Status);
        var winner=claimed.AgentId==s1.Credentials.Agent.Id ? s1 : s2; var loser=claimed.AgentId==s1.Credentials.Agent.Id ? s2 : s1;
        Assert.Equal(HttpStatusCode.NotFound,(await loser.Client.Send(HttpMethod.Put,$"/api/sales/leads/{claimed.Id}/follow-up",new SalesFollowUpRequest("غیرمجاز",null))).StatusCode);
        await Buy(customer);
        var first=Assert.Single((await Commissions(winner.Client)).Rows.Items);
        await Read<int>(await a.Client.Send(HttpMethod.Post,$"/api/admin/sales/agents/{winner.Credentials.Agent.Id}/transfer",new SalesTransferRequest(loser.Credentials.Agent.Id,true,"انتقال آزمایشی")));
        Assert.Equal(0,(await Read<Page<SalesLeadDto>>(await winner.Client.Send(HttpMethod.Get,"/api/sales/leads"))).Total);
        Assert.Equal(1,(await Read<Page<SalesLeadDto>>(await loser.Client.Send(HttpMethod.Get,"/api/sales/leads"))).Total);
        Assert.Equal(first.Id,Assert.Single((await Commissions(winner.Client)).Rows.Items).Id); Assert.Equal(0,(await Commissions(loser.Client)).Rows.Total);
        await Buy(customer); Assert.Equal("Renewal",Assert.Single((await Commissions(loser.Client)).Rows.Items).Kind);
        var waiting=await Read<SalesLeadDto>(await loser.Client.Send(HttpMethod.Post,"/api/sales/leads",new SalesLeadRequest("09"+Random.Shared.NextInt64(100_000_000,1_000_000_000),"لید باز")));
        f.Clock.Offset=TimeSpan.FromDays(31);
        Assert.Equal(waiting.Id,Assert.Single((await Read<Page<SalesLeadDto>>(await loser.Client.Send(HttpMethod.Get,"/api/sales/leads?status=Dead"))).Items).Id);
        Assert.Equal(HttpStatusCode.NoContent,(await loser.Client.Send(HttpMethod.Put,$"/api/sales/leads/{waiting.Id}/follow-up",new SalesFollowUpRequest("تماس دوباره",f.Clock.GetUtcNow().AddDays(1)))).StatusCode);
        Assert.Equal("Waiting",Assert.Single((await Read<Page<SalesLeadDto>>(await loser.Client.Send(HttpMethod.Get,"/api/sales/leads?status=Waiting"))).Items).Status);
        Assert.True((await Read<Page<SalesAuditDto>>(await a.Client.Send(HttpMethod.Get,"/api/admin/sales/audits"))).Total>=6);
    }
    [PostgresFact]
    public async Task Independent_password_login_isolation_disable_reset_logout_and_lockout() {
        var seller=await Agent(); var a=await Admin(); var owner=await f.RegisterOwner(); var s=seller.Credentials.Agent;
        foreach(var denied in new[]{f.Browser(),a.Client,owner.Client}) Assert.Equal(HttpStatusCode.Unauthorized,(await denied.Send(HttpMethod.Get,"/api/sales/leads")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await seller.Client.Send(HttpMethod.Get,"/api/admin/sales/agents")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await seller.Client.Send(HttpMethod.Get,"/api/auth/me")).StatusCode);
        var reset=await Read<SalesCredentialsDto>(await a.Client.Send(HttpMethod.Post,$"/api/admin/sales/agents/{s.Id}/reset-password"));
        Assert.Equal(HttpStatusCode.Unauthorized,(await seller.Client.Send(HttpMethod.Get,"/api/sales/auth/me")).StatusCode);
        var browser=f.Browser(); var login=new SalesLoginRequest(s.Mobile,reset.Password);
        browser.AccessToken=(await Read<SalesSessionDto>(await browser.Send(HttpMethod.Post,"/api/sales/auth/login",login))).AccessToken;
        var failed=await Task.WhenAll(Enumerable.Range(0,5).Select(_=>browser.Send(HttpMethod.Post,"/api/sales/auth/login",login with {Password="Wrong-Synthetic-Password-42!"})));
        Assert.All(failed,x=>Assert.Equal(HttpStatusCode.Unauthorized,x.StatusCode));
        Assert.Equal(HttpStatusCode.Unauthorized,(await browser.Send(HttpMethod.Post,"/api/sales/auth/login",login)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await browser.Send(HttpMethod.Get,"/api/sales/auth/me")).StatusCode);
        reset=await Read<SalesCredentialsDto>(await a.Client.Send(HttpMethod.Post,$"/api/admin/sales/agents/{s.Id}/reset-password")); login=login with {Password=reset.Password};
        browser.AccessToken=(await Read<SalesSessionDto>(await browser.Send(HttpMethod.Post,"/api/sales/auth/login",login))).AccessToken;
        await Read<SalesAgentDto>(await a.Client.Send(HttpMethod.Put,$"/api/admin/sales/agents/{s.Id}",new SalesAgentRequest(s.FullName,s.Mobile,IsActive:false)));
        Assert.Equal(HttpStatusCode.Unauthorized,(await browser.Send(HttpMethod.Get,"/api/sales/auth/me")).StatusCode);
        await Read<SalesAgentDto>(await a.Client.Send(HttpMethod.Put,$"/api/admin/sales/agents/{s.Id}",new SalesAgentRequest(s.FullName,s.Mobile)));
        browser.AccessToken=(await Read<SalesSessionDto>(await browser.Send(HttpMethod.Post,"/api/sales/auth/login",login))).AccessToken;
        Assert.Equal(HttpStatusCode.NoContent,(await browser.Send(HttpMethod.Post,"/api/sales/auth/logout")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await browser.Send(HttpMethod.Get,"/api/sales/auth/me")).StatusCode);
    }
    [PostgresFact]
    public async Task Database_blocks_cross_tenant_source_and_financial_history_mutations() {
        var seller=await Agent(); var owner=await f.RegisterOwner(provisionMembership:false); var other=await f.RegisterOwner(provisionMembership:false);
        await seller.Client.Send(HttpMethod.Post,"/api/sales/leads",new SalesLeadRequest(owner.Mobile,"آزمایش")); await Buy(owner); await Buy(other);
        var commission=Assert.Single((await Commissions(seller.Client)).Rows.Items);
        await f.Db(async db=> {
            var error=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"SalesCommissions\" SET \"AmountRial\"=1 WHERE \"Id\"={commission.Id}")); Assert.Equal("23514",error.SqlState);
            error=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"SalesCommissions\" WHERE \"Id\"={commission.Id}")); Assert.Equal("23514",error.SqlState);
            var source=await db.SubscriptionOrders.AsNoTracking().SingleAsync(x=>x.Id==commission.OrderId);
            var otherOrder=await db.SubscriptionOrders.AsNoTracking().SingleAsync(x=>x.Product=="subscription" && x.Status=="Confirmed" && x.Id!=source.Id && x.OrganizationId!=source.OrganizationId && x.ProductName!="Synthetic baseline");
            db.SalesCommissions.Add(new() { AgentId=seller.Credentials.Agent.Id,LeadId=commission.LeadId,OrganizationId=otherOrder.OrganizationId,OrderId=otherOrder.Id,
                InvoiceAmountRial=otherOrder.AmountRial,AmountRial=otherOrder.AmountRial/5,Percent=20,CreatedAtUtc=otherOrder.ConfirmedAtUtc!.Value,AvailableAtUtc=otherOrder.ConfirmedAtUtc.Value.AddDays(7) });
            var ex=await Assert.ThrowsAnyAsync<Exception>(()=>db.SaveChangesAsync()); Assert.Contains("invalid sales commission source",ex.ToString());
        });
    }
    [PostgresFact]
    public async Task Migration_is_additive_idempotent_and_refuses_loss_of_sales_history() {
        const string previous="20261020093013_AddInterviewUsageHistory";
        await f.Db(async db=>await db.GetService<IMigrator>().MigrateAsync(previous));
        var existing=await f.RegisterOwner(provisionMembership:false);
        await f.Db(async db=> {
            var script=db.GetService<IMigrator>().GenerateScript(previous,null,MigrationsSqlGenerationOptions.Idempotent);
            await db.Database.OpenConnectionAsync();
            await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = script;
            await command.ExecuteNonQueryAsync(); await command.ExecuteNonQueryAsync();
            Assert.Empty(await db.SalesAgents.ToListAsync()); Assert.Empty(await db.SalesLeads.ToListAsync()); Assert.Empty(await db.SalesCommissions.ToListAsync());
            Assert.False(db.Database.HasPendingModelChanges());
        });
        var agent=await Agent();
        var lead=await Read<SalesLeadDto>(await agent.Client.Send(HttpMethod.Post,"/api/sales/leads",new SalesLeadRequest(existing.Mobile,"مشتری قبلی")));
        Assert.Equal("Registered",lead.Status); await Buy(existing);
        await f.Db(async db=> {
            var ex=await Assert.ThrowsAsync<PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync(previous));
            Assert.Contains("sales history requires backup",ex.MessageText);
        });
        Assert.Single((await Commissions(agent.Client)).Rows.Items);
    }
    [PostgresFact]
    public async Task Validation_scope_overrides_snapshot_rates_and_openapi_contract_are_enforced() {
        var agent=await Agent(); var a=await Admin(); var owner=await f.RegisterOwner(provisionMembership:false);
        Assert.Equal(HttpStatusCode.BadRequest,(await a.Client.Send(HttpMethod.Post,"/api/admin/sales/agents",new SalesAgentRequest(" ","09123456789"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await a.Client.Send(HttpMethod.Post,"/api/admin/sales/agents",new SalesAgentRequest("آزمایش","09123456789",20.001m))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await agent.Client.Send(HttpMethod.Post,"/api/sales/leads",new SalesLeadRequest("invalid","مشتری"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await agent.Client.Send(HttpMethod.Post,"/api/sales/leads",new SalesLeadRequest(owner.Mobile,"نام\0"))).StatusCode);
        await agent.Client.Send(HttpMethod.Post,"/api/sales/leads",new SalesLeadRequest(owner.Mobile,"مشتری")); await Buy(owner);
        var first=Assert.Single((await Commissions(agent.Client)).Rows.Items); Assert.Equal(20,first.Percent);
        await Read<SalesAgentDto>(await a.Client.Send(HttpMethod.Put,$"/api/admin/sales/agents/{agent.Credentials.Agent.Id}",new SalesAgentRequest("فروشنده",agent.Credentials.Agent.Mobile,30,8)));
        agent.Client.AccessToken=(await Read<SalesSessionDto>(await agent.Client.Send(HttpMethod.Post,"/api/sales/auth/login",new SalesLoginRequest(agent.Credentials.Agent.Mobile,agent.Credentials.Password)))).AccessToken;
        await Buy(owner); var rows=(await Commissions(agent.Client)).Rows.Items; Assert.Equal(20,rows.Single(x=>x.Id==first.Id).Percent); Assert.Equal(8,rows.Single(x=>x.Kind=="Renewal").Percent);
        Assert.Equal(2,(await Commissions(agent.Client,"?agentId="+Guid.NewGuid())).Rows.Total);
        Assert.Equal(HttpStatusCode.BadRequest,(await agent.Client.Send(HttpMethod.Get,"/api/sales/commissions?status=Invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await agent.Client.Send(HttpMethod.Get,"/api/sales/commissions?registeredFromUtc=2026-10-21T00:00:00Z&registeredToUtc=2026-10-20T00:00:00Z")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await agent.Client.Send(HttpMethod.Post,"/api/sales/payouts",new SalesPayoutRequest(Guid.Empty,[first.Id]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await agent.Client.Send(HttpMethod.Post,"/api/sales/payouts",new SalesPayoutRequest(Guid.NewGuid(),[first.Id,first.Id]))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await agent.Client.Send(HttpMethod.Post,"/api/sales/payouts",new SalesPayoutRequest(Guid.NewGuid(),[Guid.NewGuid()]))).StatusCode);
        var document=await Read<JsonElement>(await f.Browser().Send(HttpMethod.Get,"/openapi/v1.json"));
        var salesPath=document.GetProperty("paths").GetProperty("/api/sales/commissions").GetProperty("get");
        Assert.Contains("Sales",salesPath.GetProperty("security").ToString());
        Assert.Contains("SuperAdmin",document.GetProperty("paths").GetProperty("/api/admin/sales/agents").GetProperty("get").GetProperty("security").ToString());
        Assert.Contains("no-store",(await agent.Client.Send(HttpMethod.Get,"/api/sales/commissions")).Headers.CacheControl!.ToString());
    }
}

