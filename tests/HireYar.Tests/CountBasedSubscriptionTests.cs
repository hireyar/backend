using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Subscriptions;
using HireYar.Application.Interviews;
using HireYar.Application.InterviewTemplates;
using HireYar.Application.JobPositions;
using HireYar.Application.Organizations;
using HireYar.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class CountBasedSubscriptionTests : IAsyncLifetime
{
    private readonly ApiFixture f = new();
    private Account? admin;
    public Task InitializeAsync() => f.InitializeAsync();
    public Task DisposeAsync() => ((IAsyncLifetime)f).DisposeAsync();
    // Paid-order tests start with an existing synthetic account; signup gifts are tested separately.
    private Task<Account> Owner() => f.RegisterOwner(provisionMembership: false);
    private async Task<Account> Admin() => admin ??= await f.RegisterAdmin();
    private async Task<T> Read<T>(HttpResponseMessage response)
    { Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!; }
    private async Task<PurchaseQuote> Quote(Account owner, PurchaseSelection s) => await Read<PurchaseQuote>(await owner.Client.Send(HttpMethod.Post, "/api/subscriptions/catalog/quote", s));
    private async Task<SubscriptionOrderDto> Order(Account owner, PurchaseSelection s, Guid? request = null)
    {
        var q = await Quote(owner, s);
        return await Read<SubscriptionOrderDto>(await owner.Client.Send(HttpMethod.Post, "/api/subscriptions/orders",
            new CreateSubscriptionOrderRequest(request ?? Guid.NewGuid(), s, q.PlanVersion, q.BundleVersion)));
    }
    private async Task Confirm(SubscriptionOrderDto order, string? reference = null)
    {
        var a = await Admin(); await Read<SubscriptionOrderDto>(await a.Client.Send(HttpMethod.Post,
            $"/api/admin/subscriptions/orders/{order.Id}/confirm", new ConfirmSubscriptionOrderRequest(reference ?? "synthetic-" + order.Id)));
    }
    private async Task Subscribe(Account o, int users = 5, int months = 1) => await Confirm(await Order(o, new("subscription", months, users)));
    private async Task BuyBundle(Account o, string code = "dana", int? count = null) => await Confirm(await Order(o, new("interviews", BundleCode: code, Quantity: count)));
    private async Task<SubscriptionStateDto> State(Account o) => await Read<SubscriptionStateDto>(await o.Client.Send(HttpMethod.Get, "/api/subscriptions/current"));
    private async Task<(JobDto Job, TemplateDto Template)> Setup(Account owner)
    {
        var job = await Read<JobDto>(await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/positions", new WizardPositionRequest("Synthetic role", "Synthetic job description")));
        var template = await Read<TemplateDto>(await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/templates",
            new WizardTemplateRequest(job.Id, "Synthetic template", "general", Enumerable.Range(1, 5).Select(i => "Synthetic question " + i).ToList(), "Welcome", "Goodbye", 30)));
        return (job, template);
    }
    private async Task<HttpResponseMessage> Invite(Account o, Guid job, Guid template)
    {
        using var http = f.CreateClient(); http.DefaultRequestHeaders.Authorization = new("Bearer", o.Client.AccessToken);
        using var body = new MultipartFormDataContent();
        foreach (var pair in new Dictionary<string,string> { ["jobPositionId"] = job.ToString(), ["templateId"] = template.ToString(),
            ["firstName"] = "Synthetic", ["lastName"] = "Candidate", ["email"] = "candidate@example.test", ["phone"] = "09123456789",
            ["interviewType"] = "general", ["resumeText"] = "Synthetic professional history" }) body.Add(new StringContent(pair.Value), pair.Key);
        body.Add(new ByteArrayContent("%PDF-1.7\nSynthetic test resume"u8.ToArray()), "resume", "resume.pdf");
        return await http.PostAsync("/api/panel-wizard/invitations", body);
    }
    private async Task<InterviewInvitation> Issue(Account o, Guid job, Guid template) => (await Read<PanelInvitationResult>(await Invite(o, job, template))).Invitation;
    private HttpClient Candidate(InterviewInvitation i) { var c = f.CreateClient(); c.DefaultRequestHeaders.Authorization = new("Bearer", i.Token); return c; }

    [PostgresFact]
    public async Task Html_prices_discounts_custom_steps_and_team_ceiling_are_authoritative_rial()
    {
        var o = await Owner();
        var catalog = await Read<PlanDto>(await f.CreateClient().GetAsync("/api/subscriptions/catalog"));
        Assert.Equal("IRR", catalog.Currency); Assert.Equal(90_000_000, catalog.MonthlyPriceRial); Assert.Equal(4_000_000, catalog.BaseInterviewPriceRial);
        Assert.Equal(4, catalog.Bundles.Count); Assert.Equal(30, catalog.MaximumInterviewMinutes); Assert.Equal(60, catalog.RetentionDays);
        foreach (var (code, quantity, amount, discount) in new[] { ("dana",10,40_000_000L,0m), ("bina",20,75_000_000L,6.25m), ("zharfa",50,190_000_000L,5m), ("custom",50,200_000_000L,0m), ("custom",300,1_200_000_000L,0m) })
        {
            var q = await Quote(o, new("interviews", BundleCode: code, Quantity: quantity));
            Assert.Equal(amount, q.AmountRial); Assert.Equal(discount, q.DiscountPercent); Assert.Equal(4_000_000L * quantity - amount, q.DiscountAmountRial);
        }
        foreach (var quantity in new[] {49, 51, 301}) Assert.Equal(HttpStatusCode.BadRequest,
            (await o.Client.Send(HttpMethod.Post, "/api/subscriptions/catalog/quote", new PurchaseSelection("interviews", BundleCode:"custom", Quantity:quantity))).StatusCode);
        Assert.Equal(90_000_000, (await Quote(o, new("subscription"))).AmountRial);
        Assert.Equal(92_000_000, (await Quote(o, new("subscription", TeamUsers:6))).AmountRial);
        Assert.Equal(140_000_000, (await Quote(o, new("subscription", TeamUsers:30))).AmountRial);
        Assert.Equal((await Quote(o,new("subscription", TeamUsers:30))).AmountRial,(await Quote(o,new("subscription", TeamUsers:100))).AmountRial);
        Assert.Equal(1_080_000_000, (await Quote(o, new("subscription",12))).AmountRial);
        Assert.Equal(HttpStatusCode.BadRequest,(await o.Client.Send(HttpMethod.Post,"/api/subscriptions/catalog/quote",new PurchaseSelection("subscription",2))).StatusCode);
    }

    [PostgresFact]
    public async Task Only_admin_confirmation_activates_purchases_and_replay_never_duplicates_entitlements()
    {
        var o = await Owner(); var before = await State(o); Assert.False(before.HasActiveSubscription); Assert.Equal(0,before.RemainingInterviews);
        Assert.Equal(HttpStatusCode.Forbidden,(await o.Client.Send(HttpMethod.Post,"/api/panel-wizard/positions",new WizardPositionRequest("Denied","No subscription"))).StatusCode);
        var selection = new PurchaseSelection("subscription",3); var q = await Quote(o,selection); var key = Guid.NewGuid();
        var request = new CreateSubscriptionOrderRequest(key,selection,q.PlanVersion);
        var order = await Read<SubscriptionOrderDto>(await o.Client.Send(HttpMethod.Post,"/api/subscriptions/orders",request));
        Assert.Equal(order.Id,(await Read<SubscriptionOrderDto>(await o.Client.Send(HttpMethod.Post,"/api/subscriptions/orders",request))).Id);
        Assert.False((await State(o)).HasActiveSubscription);
        Assert.Equal(HttpStatusCode.Unauthorized,(await o.Client.Send(HttpMethod.Post,$"/api/admin/subscriptions/orders/{order.Id}/confirm",new ConfirmSubscriptionOrderRequest("forged"))).StatusCode);
        await Confirm(order); await Confirm(order);
        var state = await State(o); Assert.True(state.HasActiveSubscription); Assert.InRange(state.RemainingDays,89,93);
        Assert.Equal(0,state.RemainingInterviews); await BuyBundle(o,"bina"); Assert.Equal(20,(await State(o)).AvailableInterviews);
        await f.Db(async db => {Assert.Single(await db.OrganizationMemberships.Where(x=>x.OrganizationId==order.OrganizationId).ToListAsync());Assert.Single(await db.InterviewQuotaGrants.Where(x=>x.OrganizationId==order.OrganizationId).ToListAsync());});
        var other = await Owner(); Assert.Equal(HttpStatusCode.NotFound,(await other.Client.Send(HttpMethod.Get,$"/api/subscriptions/orders/{order.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await o.Client.Send(HttpMethod.Post,"/api/subscriptions/orders",request with {Selection=new("subscription",6)})).StatusCode);
    }

    [PostgresFact]
    public async Task Admin_price_changes_use_row_versions_and_never_reprice_a_pending_order()
    {
        var o = await Owner(); var order = await Order(o,new("subscription")); var oldQuote = await Quote(o,new("subscription")); var a = await Admin();
        var plan = await Read<PlanDto>(await a.Client.Send(HttpMethod.Get,"/api/admin/subscriptions/plan"));
        var update = new PlanUpdateRequest(plan.Name,100_000_000,5_000_000,plan.ExtraUserMonthlyPriceRial,5,30,60,true,plan.RowVersion);
        var edited = await Read<PlanDto>(await a.Client.Send(HttpMethod.Put,"/api/admin/subscriptions/plan",update));
        Assert.NotEqual(Convert.ToBase64String(plan.RowVersion),Convert.ToBase64String(edited.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict,(await a.Client.Send(HttpMethod.Put,"/api/admin/subscriptions/plan",update)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await o.Client.Send(HttpMethod.Post,"/api/subscriptions/orders",new CreateSubscriptionOrderRequest(Guid.NewGuid(),new("subscription"),oldQuote.PlanVersion))).StatusCode);
        await Confirm(order); Assert.Equal(90_000_000,(await Read<SubscriptionOrderDto>(await o.Client.Send(HttpMethod.Get,$"/api/subscriptions/orders/{order.Id}"))).AmountRial);
        Assert.Equal(100_000_000,(await Quote(o,new("subscription"))).AmountRial);
        var bundle = edited.Bundles.Single(b=>b.Code=="bina");
        await Read<PlanDto>(await a.Client.Send(HttpMethod.Put,"/api/admin/subscriptions/bundles/bina",new BundleUpdateRequest(bundle.Name,20,20,1,10,2,true,bundle.RowVersion)));
        Assert.Equal(90_000_000,(await Quote(o,new("interviews",BundleCode:"bina"))).AmountRial);
    }

    [PostgresFact]
    public async Task Parallel_invitation_and_start_are_atomic_and_reserved_cancel_releases_without_refunding_started_interviews()
    {
        var o = await Owner(); await Subscribe(o); await BuyBundle(o); var (job,template)=await Setup(o); var invitations=new List<InterviewInvitation>();
        for(var n=0;n<9;n++)invitations.Add(await Issue(o,job.Id,template.Id));
        var race=await Task.WhenAll(Invite(o,job.Id,template.Id),Invite(o,job.Id,template.Id));
        Assert.Single(race,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(race,r=>r.StatusCode==HttpStatusCode.Conflict);
        var state=await State(o);Assert.Equal(10,state.RemainingInterviews);Assert.Equal(10,state.ReservedInterviews);Assert.Equal(0,state.AvailableInterviews);
        using var c1=Candidate(invitations[0]);using var c2=Candidate(invitations[0]);
        var starts=await Task.WhenAll(c1.PostAsync($"/api/candidate/interviews/{invitations[0].InterviewId}/start",null),c2.PostAsync($"/api/candidate/interviews/{invitations[0].InterviewId}/start",null));
        Assert.Contains(starts,r=>r.StatusCode==HttpStatusCode.OK);Assert.All(starts,r=>Assert.Contains(r.StatusCode,new[]{HttpStatusCode.OK,HttpStatusCode.Conflict}));
        Assert.Equal(HttpStatusCode.OK,(await c1.PostAsync($"/api/candidate/interviews/{invitations[0].InterviewId}/start",null)).StatusCode);state=await State(o);Assert.Equal(1,state.ConsumedInterviews);Assert.Equal(9,state.RemainingInterviews);
        Assert.Equal(HttpStatusCode.NoContent,(await o.Client.Send(HttpMethod.Post,$"/api/interviews/{invitations[1].InterviewId}/cancel")).StatusCode);
        Assert.Equal(1,(await State(o)).AvailableInterviews);
        Assert.Equal(HttpStatusCode.NoContent,(await o.Client.Send(HttpMethod.Post,$"/api/interviews/{invitations[0].InterviewId}/cancel")).StatusCode);
        Assert.Equal(1,(await State(o)).ConsumedInterviews);Assert.Equal(9,(await State(o)).RemainingInterviews);
        await f.Db(async db=>Assert.Equal(1800,(await db.InterviewSessions.SingleAsync(s=>s.Id==invitations[0].InterviewId)).MaxDurationSeconds));
    }

    [PostgresFact]
    public async Task Resend_reuses_a_reserved_slot_and_expired_invitations_release_available_quota_without_a_get_write()
    {
        var o=await Owner();await Subscribe(o);await BuyBundle(o);var(job,t)=await Setup(o);var invite=await Issue(o,job.Id,t.Id);
        var replacement=await Read<PanelInvitationResult>(await o.Client.Send(HttpMethod.Post,$"/api/interviews/{invite.InterviewId}/resend",new ResendInterviewRequest()));
        Assert.Equal(1,(await State(o)).ReservedInterviews);Assert.Equal(9,(await State(o)).AvailableInterviews);
        await f.Db(async db=>Assert.Equal("Released",(await db.InterviewQuotaReservations.SingleAsync(r=>r.InterviewSessionId==invite.InterviewId)).State));
        f.Clock.Offset=TimeSpan.FromDays(4);
        Assert.Equal(0,(await State(o)).ReservedInterviews);Assert.Equal(10,(await State(o)).AvailableInterviews);
        using var candidate=Candidate(replacement.Invitation);Assert.Equal(HttpStatusCode.Gone,(await candidate.PostAsync($"/api/candidate/interviews/{replacement.Invitation.InterviewId}/start",null)).StatusCode);
    }

    [PostgresFact]
    public async Task Expiry_preserves_unused_quota_renewal_restores_access_and_early_renewal_appends_calendar_months()
    {
        var o=await Owner();await Subscribe(o);await BuyBundle(o);var state=await State(o);var end=state.ExpiresAtUtc!.Value;
        await Subscribe(o);var renewed=await State(o);Assert.Equal(end.AddMonths(1),renewed.ExpiresAtUtc);
        f.Clock.Offset=TimeSpan.FromDays(70);state=await State(o);Assert.False(state.HasActiveSubscription);Assert.Equal(0,state.RemainingDays);Assert.Equal(10,state.RemainingInterviews);
        Assert.Equal(HttpStatusCode.Forbidden,(await o.Client.Send(HttpMethod.Post,"/api/subscriptions/orders",new CreateSubscriptionOrderRequest(Guid.NewGuid(),new("interviews",BundleCode:"dana"),(await Quote(o,new("interviews",BundleCode:"dana"))).PlanVersion,(await Quote(o,new("interviews",BundleCode:"dana"))).BundleVersion))).StatusCode);
        await Subscribe(o);Assert.True((await State(o)).HasActiveSubscription);Assert.Equal(10,(await State(o)).AvailableInterviews);
    }

    [PostgresFact]
    public async Task Database_rejects_cross_organization_grants_and_changes_to_fulfilled_history()
    {
        var o=await Owner();await Subscribe(o);await BuyBundle(o);var other=await Owner();var foreignOrder=await Order(o,new("interviews",BundleCode:"dana"));var foreign=(await other.Client.Send(HttpMethod.Get,"/api/auth/me"));
        var me=(await foreign.Content.ReadFromJsonAsync<HireYar.Application.Auth.CurrentUserDto>(TestJson.Options))!;
        await f.Db(async db=>{
            var order=await db.SubscriptionOrders.SingleAsync(x=>x.Id==foreignOrder.Id);
            db.InterviewQuotaGrants.Add(new(){OrganizationId=me.OrganizationId,OrderId=order.Id,Quantity=10,CreatedAtUtc=f.Clock.GetUtcNow()});
            var failure=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());Assert.Equal(PostgresErrorCodes.ForeignKeyViolation,Assert.IsType<PostgresException>(failure.InnerException).SqlState);db.ChangeTracker.Clear();
            var immutable=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"SubscriptionOrders\" SET \"AmountRial\"=1 WHERE \"Id\"={order.Id}"));Assert.Equal(PostgresErrorCodes.RaiseException,immutable.SqlState);
        });
    }
    [PostgresFact]
    public async Task Team_capacity_counts_owner_and_pending_invites_and_the_price_does_not_grow_above_thirty()
    {
        var o=await Owner();await Subscribe(o);
        for(var i=0;i<4;i++)Assert.Equal(HttpStatusCode.Created,(await o.Client.Send(HttpMethod.Post,"/api/team/invitations",
            new InviteTeamMemberRequest("09"+Random.Shared.NextInt64(100_000_000,1_000_000_000),"Synthetic","Member"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await o.Client.Send(HttpMethod.Post,"/api/team/invitations",new InviteTeamMemberRequest("09"+Random.Shared.NextInt64(100_000_000,1_000_000_000),"Over","Capacity"))).StatusCode);
        var unlimited=await Owner();await Subscribe(unlimited,31);
        Assert.True((await State(unlimited)).UnlimitedTeam);Assert.Null((await State(unlimited)).TeamCapacity);
        for(var i=0;i<6;i++)Assert.Equal(HttpStatusCode.Created,(await unlimited.Client.Send(HttpMethod.Post,"/api/team/invitations",new InviteTeamMemberRequest("09"+Random.Shared.NextInt64(100_000_000,1_000_000_000),"Synthetic","Unlimited"))).StatusCode);
    }

    [PostgresFact]
    public async Task Sixty_day_media_expiry_keeps_text_report_and_dashboard_exposes_server_counters()
    {
        var o=await Owner();await Subscribe(o);await BuyBundle(o);var(job,t)=await Setup(o);var invite=await Issue(o,job.Id,t.Id);
        using var candidate=Candidate(invite);Assert.Equal(HttpStatusCode.OK,(await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/start",null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await candidate.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/complete",null)).StatusCode);
        var dashboard=await Read<System.Text.Json.JsonElement>(await o.Client.Send(HttpMethod.Get,"/api/dashboard"));
        Assert.Equal(9,dashboard.GetProperty("subscription").GetProperty("remainingInterviews").GetInt64());
        Assert.Equal(1,dashboard.GetProperty("subscription").GetProperty("consumedInterviews").GetInt32());
        f.Clock.Offset=TimeSpan.FromDays(61);
        var report=await Read<System.Text.Json.JsonElement>(await o.Client.Send(HttpMethod.Get,$"/api/interviews/{invite.InterviewId}/report"));
        Assert.Equal("BaseReady",report.GetProperty("state").GetString());Assert.Equal("Expired",report.GetProperty("access").GetProperty("mediaState").GetString());
        Assert.Equal(0,f.AssessmentAi.Calls);Assert.Equal(HttpStatusCode.OK,(await o.Client.Send(HttpMethod.Get,$"/api/interviews/{invite.InterviewId}/review")).StatusCode);
        await f.Db(async db=> {
            var session=await db.InterviewSessions.SingleAsync(x=>x.Id==invite.InterviewId);
            await using var scope=f.Services.CreateAsyncScope();
            var error=await Assert.ThrowsAsync<HireYar.Application.Common.AppException>(()=>scope.ServiceProvider.GetRequiredService<InterviewQuotaService>().RequireMediaAsync(session,default));Assert.Equal(404,error.Status);
        });
    }

    [PostgresFact]
    public async Task Expired_or_cancelled_orders_and_reused_receipts_cannot_grant_access()
    {
        var o=await Owner();var a=await Admin();var expired=await Order(o,new("subscription"));f.Clock.Offset=TimeSpan.FromHours(25);
        Assert.Equal(HttpStatusCode.Conflict,(await a.Client.Send(HttpMethod.Post,$"/api/admin/subscriptions/orders/{expired.Id}/confirm",new ConfirmSubscriptionOrderRequest("expired-receipt"))).StatusCode);
        f.Clock.Offset=TimeSpan.Zero;
        var cancelled=await Order(o,new("subscription"));Assert.Equal(HttpStatusCode.NoContent,(await o.Client.Send(HttpMethod.Post,$"/api/subscriptions/orders/{cancelled.Id}/cancel")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await a.Client.Send(HttpMethod.Post,$"/api/admin/subscriptions/orders/{cancelled.Id}/confirm",new ConfirmSubscriptionOrderRequest("cancelled-receipt"))).StatusCode);
        var first=await Order(o,new("subscription"));await Confirm(first,"shared-reference");var second=await Order(o,new("subscription"));
        Assert.Equal(HttpStatusCode.Conflict,(await a.Client.Send(HttpMethod.Post,$"/api/admin/subscriptions/orders/{second.Id}/confirm",new ConfirmSubscriptionOrderRequest("shared-reference"))).StatusCode);
    }

    [PostgresFact]
    public async Task Calendar_month_uses_utc_even_when_database_session_is_in_a_different_month()
    {
        var o=await Owner();var a=await Admin();
        f.Clock.Offset=new DateTimeOffset(2027,1,31,23,30,0,TimeSpan.Zero)-DateTimeOffset.UtcNow;
        var order=await Order(o,new("subscription"));
        await using var scope=f.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<HireYar.Infrastructure.Persistence.AppDbContext>();
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("SET TIME ZONE 'Asia/Tehran'");
        try
        {
            await scope.ServiceProvider.GetRequiredService<SubscriptionOrderService>().ConfirmAsync(order.Id,a.Id,"utc-month-boundary",default);
            var m=await db.OrganizationMemberships.AsNoTracking().SingleAsync(x=>x.OrderId==order.Id);
            Assert.Equal(31,m.StartedAtUtc.Day);Assert.Equal(1,m.StartedAtUtc.Month);
            Assert.Equal(m.StartedAtUtc.AddMonths(1),m.ExpiresAtUtc);Assert.Equal(28,m.ExpiresAtUtc.Day);Assert.Equal(2,m.ExpiresAtUtc.Month);
        }
        finally {await db.Database.CloseConnectionAsync();}
    }

    [PostgresFact]
    public async Task Signup_grants_exactly_48_hours_and_two_interviews_once_without_payment()
    {
        var o = await f.RegisterOwner(explicitSignup: true, provisionMembership: false);
        var state = await State(o);
        Assert.True(state.HasActiveSubscription); Assert.True(state.IsTrial);
        Assert.Equal(2, state.RemainingDays); Assert.Equal(2, state.AvailableInterviews);
        Assert.Equal(TimeSpan.FromHours(48), state.ExpiresAtUtc - state.StartedAtUtc);
        Assert.Equal(5, state.TeamCapacity); Assert.False(state.UnlimitedTeam);
        await f.SignInAsync(o.Client, o.Mobile);
        Assert.Equal(2, (await State(o)).AvailableInterviews);
        await f.Db(async db => {
            Assert.Single(await db.SignupTrials.ToListAsync());
            Assert.Empty(await db.SubscriptionOrders.ToListAsync());
            Assert.Empty(await db.OrganizationMemberships.ToListAsync());
            Assert.Empty(await db.InterviewQuotaGrants.ToListAsync());
        });
        var (job, template) = await Setup(o);
        var first = await Issue(o, job.Id, template.Id);
        var second = await Issue(o, job.Id, template.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Invite(o, job.Id, template.Id)).StatusCode);
        Assert.Equal(2, (await State(o)).ReservedInterviews);
        using var candidate = Candidate(first);
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{first.InterviewId}/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{first.InterviewId}/start", null)).StatusCode);
        Assert.Equal(1, (await State(o)).ConsumedInterviews);
        Assert.Equal(HttpStatusCode.NoContent, (await o.Client.Send(HttpMethod.Post, $"/api/interviews/{second.InterviewId}/cancel")).StatusCode);
        Assert.Equal(1, (await State(o)).AvailableInterviews);
        await f.Db(async db => {
            var trial = await db.SignupTrials.AsNoTracking().SingleAsync();
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"SignupTrials\" SET \"ExpiresAtUtc\" = \"ExpiresAtUtc\" + interval '1 day' WHERE \"OrganizationId\" = {trial.OrganizationId}"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"SignupTrials\" SELECT * FROM \"SignupTrials\" WHERE \"OrganizationId\" = {trial.OrganizationId}"));
        });
    }

    [PostgresFact]
    public async Task Trial_expiry_blocks_new_activity_preserves_gift_and_paid_renewal_restores_access()
    {
        var o = await f.RegisterOwner(explicitSignup: true, provisionMembership: false);
        var original = await State(o);
        await Admin(); // Issue admin credentials before advancing the business clock.
        // Business clock advances; retain the existing JWT, whose lifetime validation uses wall time.
        f.Clock.Offset = original.ExpiresAtUtc!.Value - DateTimeOffset.UtcNow;
        var expired = await State(o);
        Assert.False(expired.HasActiveSubscription); Assert.False(expired.IsTrial);
        Assert.Equal(0, expired.RemainingDays); Assert.Equal(2, expired.RemainingInterviews);
        Assert.Equal(HttpStatusCode.Forbidden, (await o.Client.Send(HttpMethod.Post, "/api/panel-wizard/positions",
            new WizardPositionRequest("Blocked", "Expired trial"))).StatusCode);
        Assert.False((await State(o)).HasActiveSubscription);
        await Subscribe(o);
        Assert.False((await State(o)).IsTrial); Assert.True((await State(o)).HasActiveSubscription);
        Assert.Equal(2, (await State(o)).AvailableInterviews);
        await f.Db(async db => Assert.Single(await db.SignupTrials.ToListAsync()));
    }

    [PostgresFact]
    public async Task Paid_subscription_bought_during_trial_starts_after_the_free_period()
    {
        var o = await f.RegisterOwner(explicitSignup: true, provisionMembership: false);
        var trial = await State(o);
        var order = await Order(o, new("subscription")); await Confirm(order);
        var state = await State(o);
        Assert.True(state.IsTrial); Assert.Equal(trial.StartedAtUtc, state.StartedAtUtc);
        Assert.Equal(trial.ExpiresAtUtc!.Value.AddMonths(1), state.ExpiresAtUtc);
        await f.Db(async db => Assert.Equal(trial.ExpiresAtUtc,
            (await db.OrganizationMemberships.SingleAsync(m => m.OrderId == order.Id)).StartedAtUtc));
        f.Clock.Offset = trial.ExpiresAtUtc.Value - DateTimeOffset.UtcNow;
        Assert.True((await State(o)).HasActiveSubscription); Assert.False((await State(o)).IsTrial);
        Assert.Equal(2, (await State(o)).AvailableInterviews);
    }

}
