using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using HireYar.Application.Administration;
using HireYar.Application.Auth;
using HireYar.Application.Common;
using HireYar.Application.Sales;
using HireYar.Application.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class AdminCustomerTests : IAsyncLifetime
{
    private readonly ApiFixture f=new(); private Account? admin;
    private const string Route="/api/admin/customers";
    public Task InitializeAsync()=>f.InitializeAsync();
    public Task DisposeAsync()=>((IAsyncLifetime)f).DisposeAsync();
    private async Task<Account> Admin()=>admin ??= await f.RegisterAdmin();
    private static async Task<T> Read<T>(HttpResponseMessage response) {
        Assert.True(response.StatusCode==HttpStatusCode.OK,$"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }
    private async Task<AdminCustomerDetail> Detail(Guid id)=>await Read<AdminCustomerDetail>(await (await Admin()).Client.Send(HttpMethod.Get,Route+"/"+id));
    private async Task<SalesCredentialsDto> Agent() {
        var mobile="09"+Random.Shared.NextInt64(100_000_000,1_000_000_000);
        return await Read<SalesCredentialsDto>(await (await Admin()).Client.Send(HttpMethod.Post,"/api/admin/sales/agents",new SalesAgentRequest("فروشنده آزمایشی",mobile)));
    }
    private async Task<Browser> SalesBrowser(SalesCredentialsDto agent) {
        var b=f.Browser();b.SalesEntryKey=ApiFixture.SalesEntry;b.AccessToken=(await Read<SalesSessionDto>(await b.Send(HttpMethod.Post,"/api/sales/auth/login",new SalesLoginRequest(agent.Agent.Mobile,agent.Password)))).AccessToken;return b;
    }
    private async Task<SubscriptionOrderDto> Buy(Account owner) {
        var q=await Read<PurchaseQuote>(await owner.Client.Send(HttpMethod.Post,"/api/subscriptions/catalog/quote",new PurchaseSelection("subscription")));
        var a=await Admin();var o=await Read<SubscriptionOrderDto>(await a.Client.Send(HttpMethod.Post,Route+$"/{owner.Id}/orders",new CreateSubscriptionOrderRequest(Guid.NewGuid(),new("subscription"),q.PlanVersion)));
        return await Read<SubscriptionOrderDto>(await a.Client.Send(HttpMethod.Post,$"/api/admin/subscriptions/orders/{o.Id}/confirm",new ConfirmSubscriptionOrderRequest("synthetic-"+o.Id)));
    }
    [PostgresFact]
    public async Task Customer_list_search_filters_stats_and_detail_reflect_real_sources_and_permissions() {
        var a=await Admin();var owner=await f.RegisterOwner(explicitSignup:true,provisionMembership:false);var agent=await Agent();var seller=await SalesBrowser(agent);
        var mobile="09"+Random.Shared.NextInt64(100_000_000,1_000_000_000);
        var lead=await Read<SalesLeadDto>(await seller.Send(HttpMethod.Post,"/api/sales/leads",new SalesLeadRequest(mobile,"لید آزمایشی")));
        foreach(var browser in new[]{f.Browser(),owner.Client,seller}) Assert.Equal(HttpStatusCode.Unauthorized,(await browser.Send(HttpMethod.Get,Route)).StatusCode);
        var detail=await Detail(owner.Id);Assert.Equal("Registered",detail.Customer.RegistrationStatus);Assert.Equal("Trial",detail.Customer.SubscriptionStatus);Assert.Equal(2,detail.Customer.RemainingInterviews);
        var found=await Read<AdminCustomerPage>(await a.Client.Send(HttpMethod.Get,Route+"?search="+owner.Mobile+"&pageSize=1"));Assert.Equal(owner.Id,Assert.Single(found.Rows.Items).Id);Assert.Equal(1,found.Stats.Total);
        var fa=string.Concat(owner.Mobile.Select(c=>(char)('۰'+c-'0')));
        Assert.Equal(owner.Id,Assert.Single((await Read<AdminCustomerPage>(await a.Client.Send(HttpMethod.Get,Route+"?search="+Uri.EscapeDataString(fa)))).Rows.Items).Id);
        Assert.Equal(lead.Id,Assert.Single((await Read<AdminCustomerPage>(await a.Client.Send(HttpMethod.Get,Route+"?status=Waiting"))).Rows.Items).Id);
        Assert.False((await Detail(lead.Id)).CanManageSubscription);
        await Buy(owner);detail=await Detail(owner.Id);Assert.Equal("Won",detail.Customer.RegistrationStatus);Assert.Equal("Trial",detail.Customer.SubscriptionStatus);
        Assert.Equal(HttpStatusCode.BadRequest,(await a.Client.Send(HttpMethod.Get,Route+"?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await a.Client.Send(HttpMethod.Get,Route+"?status=Invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await a.Client.Send(HttpMethod.Get,Route+"/"+Guid.NewGuid())).StatusCode);
        Assert.Contains("no-store",(await a.Client.Send(HttpMethod.Get,Route)).Headers.CacheControl!.ToString());
    }
    [PostgresFact]
    public async Task Profile_updates_are_atomic_versioned_idempotent_and_revoke_identity_sessions() {
        var a=await Admin();var owner=await f.RegisterOwner();var other=await f.RegisterOwner();var d=await Detail(owner.Id);var oldRefresh=owner.Client.RefreshToken;
        var mobile="09"+Random.Shared.NextInt64(100_000_000,1_000_000_000);
        var r=new AdminCustomerProfileRequest(Guid.NewGuid(),d.Customer.ProfileVersion,"نام جدید","سازمان جدید",mobile,"synthetic-new@example.test","اصلاح اطلاعات آزمایشی");
        var saved=await Read<AdminCustomerMutationDto>(await a.Client.Send(HttpMethod.Put,Route+$"/{owner.Id}/profile",r));
        Assert.Equal(saved.EventId,(await Read<AdminCustomerMutationDto>(await a.Client.Send(HttpMethod.Put,Route+$"/{owner.Id}/profile",r))).EventId);
        var updated=await Detail(owner.Id);Assert.Equal(mobile,updated.Customer.Mobile);Assert.Equal("سازمان جدید",updated.Customer.OrganizationName);
        Assert.Equal(HttpStatusCode.Unauthorized,(await owner.Client.Send(HttpMethod.Get,"/api/auth/me")).StatusCode);
        owner.Client.AccessToken=null;owner.Client.SetRefresh(oldRefresh);Assert.Equal(HttpStatusCode.Unauthorized,(await owner.Client.Send(HttpMethod.Post,"/api/auth/refresh")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await a.Client.Send(HttpMethod.Put,Route+$"/{owner.Id}/profile",r with {RequestId=Guid.NewGuid()})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await a.Client.Send(HttpMethod.Put,Route+$"/{owner.Id}/profile",r with {RequestId=Guid.NewGuid(),ExpectedVersion=updated.Customer.ProfileVersion,Mobile=other.Mobile})).StatusCode);
        Assert.Equal(mobile,(await Detail(owner.Id)).Customer.Mobile);
        var self=await Detail(a.Id);Assert.False(self.CanEditIdentity);
        Assert.Equal(HttpStatusCode.Forbidden,(await a.Client.Send(HttpMethod.Put,Route+$"/{a.Id}/profile",r with {RequestId=Guid.NewGuid(),ExpectedVersion=self.Customer.ProfileVersion})).StatusCode);
        var history=await Read<Page<AdminCustomerHistoryDto>>(await a.Client.Send(HttpMethod.Get,Route+$"/{owner.Id}/history"));Assert.Single(history.Items,x=>x.Action=="ProfileUpdated");
    }
    [PostgresFact]
    public async Task Credit_and_manual_coverage_are_idempotent_preserve_paid_history_and_paid_renewal_creates_only_real_commission() {
        var a=await Admin();var owner=await f.RegisterOwner(explicitSignup:true,provisionMembership:false);var agent=await Agent();var d=await Detail(owner.Id);
        await Read<AdminCustomerMutationDto>(await a.Client.Send(HttpMethod.Put,Route+$"/{owner.Id}/seller",new AdminCustomerSellerRequest(Guid.NewGuid(),d.Customer.ProfileVersion,agent.Agent.Id,"انتساب آزمایشی")));
        var r=new AdminCustomerCreditRequest(Guid.NewGuid(),5,"bina","جبران مشکل فنی");
        var grants=await Task.WhenAll(a.Client.Send(HttpMethod.Post,Route+$"/{owner.Id}/interview-credit",r),a.Client.Send(HttpMethod.Post,Route+$"/{owner.Id}/interview-credit",r));
        Assert.Equal((await Read<AdminCustomerMutationDto>(grants[0])).EventId,(await Read<AdminCustomerMutationDto>(grants[1])).EventId);
        d=await Detail(owner.Id);Assert.Equal(7,d.Customer.RemainingInterviews);Assert.Equal(7,d.Subscription!.RemainingInterviews);
        var days=new AdminSubscriptionDaysRequest(Guid.NewGuid(),d.SubscriptionVersion!,7,"هدیه زمان");
        await Read<AdminCustomerMutationDto>(await a.Client.Send(HttpMethod.Post,Route+$"/{owner.Id}/subscription/days",days));
        var extended=await Detail(owner.Id);Assert.Equal(d.Subscription.ExpiresAtUtc!.Value.AddDays(7),extended.Subscription!.ExpiresAtUtc);
        Assert.Equal(HttpStatusCode.Conflict,(await a.Client.Send(HttpMethod.Post,Route+$"/{owner.Id}/subscription/days",days with {RequestId=Guid.NewGuid()})).StatusCode);
        var seller=await SalesBrowser(agent);Assert.Equal(0,(await Read<SalesCommissionPage>(await seller.Send(HttpMethod.Get,"/api/sales/commissions"))).Rows.Total);
        var paid=await Buy(owner);var first=Assert.Single((await Read<SalesCommissionPage>(await seller.Send(HttpMethod.Get,"/api/sales/commissions"))).Rows.Items);Assert.Equal("FirstSale",first.Kind);
        var beforeStop=await Detail(owner.Id);
        await Read<AdminCustomerMutationDto>(await a.Client.Send(HttpMethod.Post,Route+$"/{owner.Id}/subscription/stop",new AdminSubscriptionStopRequest(Guid.NewGuid(),beforeStop.SubscriptionVersion!,"توقف آزمایشی")));
        var stopped=await Detail(owner.Id);Assert.Equal("Stopped",stopped.Customer.SubscriptionStatus);Assert.False(stopped.Subscription!.HasActiveSubscription);Assert.Equal(7,stopped.Customer.RemainingInterviews);
        await Buy(owner);Assert.True((await Detail(owner.Id)).Subscription!.HasActiveSubscription);
        var commissions=await Read<SalesCommissionPage>(await seller.Send(HttpMethod.Get,"/api/sales/commissions"));Assert.Equal(2,commissions.Rows.Total);Assert.Single(commissions.Rows.Items,x=>x.Kind=="Renewal");
        await f.Db(async db=>{Assert.Equal(2,await db.OrganizationMemberships.CountAsync(x=>x.OrganizationId==paid.OrganizationId));Assert.Equal(1,await db.CustomerAdministrationEvents.CountAsync(x=>x.CustomerId==owner.Id && x.CreditQuantity==5));});
    }
    [PostgresFact]
    public async Task Seller_removal_preserves_accrued_commissions_and_unassigned_lead_still_registers_via_otp() {
        var a=await Admin();var agent=await Agent();var seller=await SalesBrowser(agent);var owner=await f.RegisterOwner(provisionMembership:false);
        await seller.Send(HttpMethod.Post,"/api/sales/leads",new SalesLeadRequest(owner.Mobile,"مشتری آزمایشی"));await Buy(owner);
        var d=await Detail(owner.Id);await Read<AdminCustomerMutationDto>(await a.Client.Send(HttpMethod.Put,Route+$"/{owner.Id}/seller",new AdminCustomerSellerRequest(Guid.NewGuid(),d.Customer.ProfileVersion,null,"حذف انتساب")));
        await Buy(owner);Assert.Equal(1,(await Read<SalesCommissionPage>(await seller.Send(HttpMethod.Get,"/api/sales/commissions"))).Rows.Total);Assert.Null((await Detail(owner.Id)).Customer.AgentId);
        var mobile="09"+Random.Shared.NextInt64(100_000_000,1_000_000_000);
        var lead=await Read<SalesLeadDto>(await seller.Send(HttpMethod.Post,"/api/sales/leads",new SalesLeadRequest(mobile,"لید")));d=await Detail(lead.Id);
        await Read<AdminCustomerMutationDto>(await a.Client.Send(HttpMethod.Put,Route+$"/{lead.Id}/seller",new AdminCustomerSellerRequest(Guid.NewGuid(),d.Customer.ProfileVersion,null,"بدون فروشنده")));
        var b=f.Browser();await b.Send(HttpMethod.Post,"/api/auth/otp/request",new RequestOtpRequest(mobile));var session=await Read<AccessResponse>(await b.Send(HttpMethod.Post,"/api/auth/register",ApiFixture.Registration(mobile,f.PhoneOtp.Codes[mobile])));b.AccessToken=session.AccessToken;
        var me=await Read<CurrentUserDto>(await b.Send(HttpMethod.Get,"/api/auth/me"));Assert.Null((await Detail(me.Id)).Customer.AgentId);Assert.Equal("Registered",(await Detail(me.Id)).Customer.RegistrationStatus);
        Assert.Contains((await Read<Page<AdminCustomerHistoryDto>>(await a.Client.Send(HttpMethod.Get,Route+$"/{me.Id}/history"))).Items,x=>x.Action=="SellerChanged");
    }
    [PostgresFact]
    public async Task Export_is_real_xlsx_filtered_complete_and_formula_safe_and_create_route_is_absent() {
        var a=await Admin();var owner=await f.RegisterOwner(provisionMembership:false);var other=await f.RegisterOwner(provisionMembership:false);
        var d=await Detail(owner.Id);
        await Read<AdminCustomerMutationDto>(await a.Client.Send(HttpMethod.Put,Route+$"/{owner.Id}/profile",new AdminCustomerProfileRequest(Guid.NewGuid(),d.Customer.ProfileVersion,"=SUM(1,2)\u0001🙂","خروجی آزمایشی",owner.Mobile,null,"آزمون خروجی")));
        var response=await a.Client.Send(HttpMethod.Get,Route+"/export?search="+owner.Mobile+"&pageSize=1");Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",response.Content.Headers.ContentType!.MediaType);
        using var zip=new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
        using var stream=zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();var sheet=XDocument.Load(stream);XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Assert.Equal(2,sheet.Descendants(ns+"row").Count());Assert.Empty(sheet.Descendants(ns+"f"));
        Assert.Contains(sheet.Descendants(ns+"t"),x=>x.Value=="=SUM(1,2)🙂");Assert.Contains(sheet.Descendants(ns+"t"),x=>x.Value==owner.Mobile);
        Assert.DoesNotContain(sheet.Descendants(ns+"t"),x=>x.Value==other.Mobile);
        var all=await a.Client.Send(HttpMethod.Get,Route+"/export?pageSize=1");using var allZip=new ZipArchive(new MemoryStream(await all.Content.ReadAsByteArrayAsync()));using var allStream=allZip.GetEntry("xl/worksheets/sheet1.xml")!.Open();Assert.True(XDocument.Load(allStream).Descendants(ns+"row").Count()>2);
        var doc=await Read<JsonElement>(await f.Browser().Send(HttpMethod.Get,"/openapi/v1.json"));var paths=doc.GetProperty("paths");Assert.False(paths.GetProperty(Route).TryGetProperty("post",out _));
        Assert.Contains("SuperAdmin",paths.GetProperty(Route).GetProperty("get").GetProperty("security").ToString());
        Assert.Equal(HttpStatusCode.Unauthorized,(await owner.Client.Send(HttpMethod.Get,Route+"/export")).StatusCode);
    }
    [PostgresFact]
    public async Task Scheduled_set_days_stop_and_validation_apply_to_actual_access_without_minting_paid_orders() {
        var a=await Admin();var owner=await f.RegisterOwner(provisionMembership:false);var d=await Detail(owner.Id);
        var start=new DateTimeOffset(f.Clock.GetUtcNow().AddDays(2).Ticks/10*10,TimeSpan.Zero);
        var set=new AdminSubscriptionSetRequest(Guid.NewGuid(),d.SubscriptionVersion!,start,3,0,"اشتراک دستی زمان‌بندی‌شده");
        await Read<AdminCustomerMutationDto>(await a.Client.Send(HttpMethod.Post,Route+$"/{owner.Id}/subscription/set",set));
        d=await Detail(owner.Id);Assert.Equal("Scheduled",d.Customer.SubscriptionStatus);Assert.False(d.Subscription!.HasActiveSubscription);
        var request=new AdminSubscriptionDaysRequest(Guid.NewGuid(),d.SubscriptionVersion!,7,"افزودن هفت روز");
        var race=await Task.WhenAll(a.Client.Send(HttpMethod.Post,Route+$"/{owner.Id}/subscription/days",request),a.Client.Send(HttpMethod.Post,Route+$"/{owner.Id}/subscription/days",request with {RequestId=Guid.NewGuid()}));
        Assert.Single(race,x=>x.StatusCode==HttpStatusCode.OK);Assert.Single(race,x=>x.StatusCode==HttpStatusCode.Conflict);
        await f.Db(async db=>{var o=await db.CustomerSubscriptionOverrides.SingleAsync(x=>x.OrganizationId==d.Customer.OrganizationId);Assert.Equal(start,o.StartedAtUtc);Assert.Equal(start.AddMonths(3).AddDays(7),o.ExpiresAtUtc);Assert.Empty(await db.SubscriptionOrders.Where(x=>x.OrganizationId==o.OrganizationId).ToListAsync());});
        f.Clock.Offset=TimeSpan.FromDays(3);d=await Detail(owner.Id);Assert.True(d.Subscription!.HasActiveSubscription);
        Assert.Equal(HttpStatusCode.BadRequest,(await a.Client.Send(HttpMethod.Post,Route+$"/{owner.Id}/subscription/set",set with {RequestId=Guid.NewGuid(),ExpectedVersion=d.SubscriptionVersion!,Months=2})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await a.Client.Send(HttpMethod.Post,Route+$"/{owner.Id}/interview-credit",new AdminCustomerCreditRequest(Guid.NewGuid(),0,"dana","آزمایش"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await a.Client.Send(HttpMethod.Post,Route+$"/{Guid.NewGuid()}/interview-credit",new AdminCustomerCreditRequest(Guid.NewGuid(),1,"dana","آزمایش"))).StatusCode);
        await Read<AdminCustomerMutationDto>(await a.Client.Send(HttpMethod.Post,Route+$"/{owner.Id}/subscription/stop",new AdminSubscriptionStopRequest(Guid.NewGuid(),d.SubscriptionVersion!,"توقف دسترسی")));
        Assert.Equal(HttpStatusCode.Forbidden,(await owner.Client.Send(HttpMethod.Post,"/api/panel-wizard/positions",new HireYar.Application.Interviews.WizardPositionRequest("Synthetic role","Synthetic description"))).StatusCode);
    }
    [PostgresFact]
    public async Task Migration_preserves_legacy_sales_and_memberships_is_idempotent_and_protects_scope_and_history() {
        var a=await Admin();var agent=await Agent();var owner=await f.RegisterOwner();
        const string previous="20261020121951_AddSalesPanel";
        await f.Db(async db=> {
            await db.GetService<IMigrator>().MigrateAsync(previous);
            var lead=Guid.NewGuid();var now=f.Clock.GetUtcNow();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"SalesLeads\" (\"Id\",\"AgentId\",\"Mobile\",\"CustomerName\",\"Note\",\"CreatedAtUtc\",\"LastContactAtUtc\") VALUES ({lead},{agent.Agent.Id},{"09"+Random.Shared.NextInt64(100_000_000,1_000_000_000)},{"Synthetic old lead"},{""},{now},{now})");
            var script=db.GetService<IMigrator>().GenerateScript(previous,null,MigrationsSqlGenerationOptions.Idempotent);
            await db.Database.OpenConnectionAsync();await using var command=db.Database.GetDbConnection().CreateCommand();command.CommandText=script;await command.ExecuteNonQueryAsync();await command.ExecuteNonQueryAsync();
            Assert.True(await db.SalesLeads.AnyAsync(x=>x.Id==lead && x.AgentId==agent.Agent.Id));Assert.Empty(await db.CustomerAdministrationEvents.ToListAsync());Assert.Empty(await db.CustomerSubscriptionOverrides.ToListAsync());Assert.False(db.Database.HasPendingModelChanges());
        });
        var d=await Detail(owner.Id);Assert.True(d.Subscription!.HasActiveSubscription);
        await Read<AdminCustomerMutationDto>(await a.Client.Send(HttpMethod.Post,Route+$"/{owner.Id}/interview-credit",new AdminCustomerCreditRequest(Guid.NewGuid(),3,"dana","اعتبار تست migration")));
        await f.Db(async db=> {
            var e=await db.CustomerAdministrationEvents.AsNoTracking().SingleAsync(x=>x.CustomerId==owner.Id);
            var error=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"CustomerAdministrationEvents\" WHERE \"Id\"={e.Id}"));Assert.Equal("23514",error.SqlState);
            error=await Assert.ThrowsAsync<PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync(previous));Assert.Contains("customer administration history requires backup",error.MessageText);
            var otherOrg=await db.Users.Where(x=>x.Id==a.Id).Select(x=>x.OrganizationId).SingleAsync();
            db.CustomerAdministrationEvents.Add(new(){ActorId=a.Id,CustomerId=owner.Id,OrganizationId=otherOrg,RequestId=Guid.NewGuid(),Fingerprint=new string('a',64),Action="InterviewCreditGranted",CreditQuantity=1,BundleCode="dana",Reason="Synthetic cross tenant",CreatedAtUtc=f.Clock.GetUtcNow()});
            var fail=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());Assert.Contains("invalid customer administration scope",fail.ToString());
        });
    }
    [PostgresFact]
    public async Task Administrative_credit_is_used_by_real_invitation_and_start_without_affecting_other_tenants() {
        var a=await Admin();var owner=await f.RegisterOwner(explicitSignup:true,provisionMembership:false);var other=await f.RegisterOwner(explicitSignup:true,provisionMembership:false);
        await Read<AdminCustomerMutationDto>(await a.Client.Send(HttpMethod.Post,Route+$"/{owner.Id}/interview-credit",new AdminCustomerCreditRequest(Guid.NewGuid(),1,"dana","مصرف آزمایشی واقعی")));
        var job=await Read<HireYar.Application.JobPositions.JobDto>(await owner.Client.Send(HttpMethod.Post,"/api/panel-wizard/positions",new HireYar.Application.Interviews.WizardPositionRequest("Synthetic role","Synthetic description")));
        var template=await Read<HireYar.Application.InterviewTemplates.TemplateDto>(await owner.Client.Send(HttpMethod.Post,"/api/panel-wizard/templates",new HireYar.Application.Interviews.WizardTemplateRequest(job.Id,"Synthetic template","general",Enumerable.Range(1,5).Select(i=>"Synthetic question "+i).ToList(),"Welcome","Goodbye",30)));
        using var http=f.CreateClient();http.DefaultRequestHeaders.Authorization=new("Bearer",owner.Client.AccessToken);
        using var body=new MultipartFormDataContent();
        foreach(var pair in new Dictionary<string,string> { ["jobPositionId"]=job.Id.ToString(),["templateId"]=template.Id.ToString(),["firstName"]="Synthetic",["lastName"]="Candidate",["email"]="candidate@example.test",["phone"]="09123456789",["interviewType"]="general",["resumeText"]="Synthetic professional history" }) body.Add(new StringContent(pair.Value),pair.Key);
        body.Add(new ByteArrayContent("%PDF-1.7\nSynthetic resume"u8.ToArray()),"resume","resume.pdf");
        var issued=await Read<HireYar.Application.Interviews.PanelInvitationResult>(await http.PostAsync("/api/panel-wizard/invitations",body));
        var state=await Detail(owner.Id);Assert.Equal(3,state.Subscription!.RemainingInterviews);Assert.Equal(1,state.Subscription.ReservedInterviews);Assert.Equal(2,state.Customer.AvailableInterviews);
        using var candidate=f.CreateClient();candidate.DefaultRequestHeaders.Authorization=new("Bearer",issued.Invitation.Token);
        Assert.Equal(HttpStatusCode.OK,(await candidate.PostAsync($"/api/candidate/interviews/{issued.Invitation.InterviewId}/start",null)).StatusCode);
        state=await Detail(owner.Id);Assert.Equal(2,state.Customer.RemainingInterviews);Assert.Equal(1,state.Customer.ConsumedInterviews);Assert.Equal(0,state.Customer.ReservedInterviews);
        Assert.Equal(2,(await Detail(other.Id)).Customer.RemainingInterviews);
    }
}
