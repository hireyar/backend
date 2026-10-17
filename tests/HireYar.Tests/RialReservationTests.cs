using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Api.Billing;
using HireYar.Application.Billing;
using HireYar.Application.Common;
using HireYar.Application.Interviews;
using HireYar.Application.InterviewTemplates;
using HireYar.Application.JobPositions;
using HireYar.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class RialReservationTests : IAsyncLifetime
{
    private readonly ApiFixture f = new();
    public Task InitializeAsync() => f.InitializeAsync();
    public Task DisposeAsync() => ((IAsyncLifetime)f).DisposeAsync();

    private async Task<(Account Owner, Guid Job, Guid Template, Guid Org)> Setup(decimal balance = 1000000m)
    {
        var owner = await f.RegisterOwner(); await f.FundInterviewsAsync(owner);
        Guid org = default;
        await f.Db(async db => org = (await db.Users.SingleAsync(x => x.Id == owner.Id)).OrganizationId);
        await using (var scope = f.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<WalletService>().AdjustAsync(org, new(Guid.NewGuid(), balance - 100m, "Synthetic fixed rial credit"), default);
        f.DisplayRates.RialPerUsdt = 1000000m;
        var job = await Read<JobDto>(await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/positions", new WizardPositionRequest("Synthetic", "Synthetic role")));
        var template = await Read<TemplateDto>(await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/templates",
            new WizardTemplateRequest(job.Id, "Synthetic", "general", Enumerable.Range(1,5).Select(x => "Synthetic question " + x).ToList(), "Welcome", "Goodbye", 5)));
        return (owner, job.Id, template.Id, org);
    }
    private async Task<HttpResponseMessage> Invite(Account owner, Guid job, Guid template)
    {
        using var client = f.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", owner.Client.AccessToken);
        using var form = new MultipartFormDataContent();
        foreach (var field in new Dictionary<string,string> { ["jobPositionId"]=job.ToString(), ["templateId"]=template.ToString(),
            ["firstName"]="Synthetic", ["lastName"]="Candidate", ["email"]="synthetic@example.test", ["phone"]="09123456789", ["deadlineDays"]="3", ["resumeText"]="Synthetic work history" })
            form.Add(new StringContent(field.Value), field.Key);
        form.Add(new ByteArrayContent("%PDF-1.7\nSynthetic"u8.ToArray()), "resume", "resume.pdf");
        return await client.PostAsync("/api/panel-wizard/invitations", form);
    }
    private async Task<RialWalletDto> Wallet(Account owner) => await Read<RialWalletDto>(await owner.Client.Send(HttpMethod.Get,"/api/billing/ai-credit"));
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(BillingJson.Options))!;
    }

    [PostgresFact]
    public async Task Invitation_reserves_fixed_rial_and_settlement_keeps_private_usd_audit_without_second_fx_lookup()
    {
        var (owner,job,template,org) = await Setup();
        var invitation = (await Read<PanelInvitationResult>(await Invite(owner,job,template))).Invitation;
        var held = await Wallet(owner);
        Assert.Equal(1000000m,held.Balance); Assert.Equal(250000m,held.ReservedCredit); Assert.Equal(750000m,held.AvailableCredit);
        f.DisplayRates.RialPerUsdt = 7000000m; f.DisplayRates.Unavailable = true;
        using var candidate = f.CreateClient(); candidate.DefaultRequestHeaders.Authorization = new("Bearer",invitation.Token);
        var route = $"/api/candidate/interviews/{invitation.InterviewId}";
        var startedResponse = await candidate.PostAsync(route+"/start",null);
        var body = await startedResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("rialPerUsdt",body); Assert.DoesNotContain("ratePerMinute",body); Assert.DoesNotContain("reservedCredit",body);
        var current = await Read<CandidateInterviewDto>(startedResponse);
        if(current.Stage=="Welcome") current = await Read<CandidateInterviewDto>(await candidate.PostAsJsonAsync(route+"/stage/advance",new AdvanceInterviewStageRequest("Welcome")));
        Assert.Equal(HttpStatusCode.Unauthorized,(await candidate.GetAsync($"/api/interview-pricing/quote?templateId={template}")).StatusCode);
        f.Clock.Offset = TimeSpan.FromSeconds(20);
        using var answer = new MultipartFormDataContent(); answer.Add(new StringContent(current.Question!.Id.ToString()),"questionId");
        var audio = new ByteArrayContent(Convert.FromHexString("1A45DFA3000000000000000000000000")); audio.Headers.ContentType = new("audio/webm"); answer.Add(audio,"audio","answer.webm");
        Assert.Equal(HttpStatusCode.OK,(await candidate.PostAsync(route+"/answers",answer)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await candidate.PostAsync(route+"/complete",null)).StatusCode);
        decimal cost = 0;
        await f.Db(async db => {
            var bill = await db.InterviewBillings.SingleAsync(x=>x.InterviewSessionId==invitation.InterviewId);
            Assert.Equal(1000000m,bill.RialPerUsdt); Assert.Equal(50000m,bill.RatePerMinuteRial);
            Assert.True(bill.BillableSeconds>0); cost=BillingMath.Cost(bill.BillableSeconds,50000m);
            Assert.Equal(cost,bill.BilledAmountRial);
            Assert.Equal(BillingMath.Cost(bill.BillableSeconds,.05m),bill.BilledAmountUsd);
            var usage = await db.AiCreditTransactions.SingleAsync(x=>x.OrganizationId==org && x.Type==CreditTransactionType.InterviewUsage);
            Assert.Equal(-cost,usage.Amount); Assert.Equal(-bill.BilledAmountUsd,usage.AmountUsd);
            Assert.Equal(1000000m-cost,(await db.OrganizationAiWallets.SingleAsync(x=>x.OrganizationId==org)).Balance);
            Assert.False(db.Database.HasPendingModelChanges());
        });
        var after=await Wallet(owner); Assert.Equal(0m,after.ReservedCredit); Assert.Equal(1000000m-cost,after.AvailableCredit);
        foreach(var url in new[]{"/api/billing/ai-usage","/api/billing/ai-credit/transactions","/api/billing/financial-history"}) {
            var response=await owner.Client.Send(HttpMethod.Get,url); Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            var output=await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("amountUsd",output); Assert.DoesNotContain("billedAmountUsd",output);
        }
    }

    [PostgresFact]
    public async Task Concurrent_invitations_cannot_overspend_and_revocation_releases_hold_idempotently()
    {
        var (owner,job,template,org)=await Setup(300000m);
        var responses=await Task.WhenAll(Invite(owner,job,template),Invite(owner,job,template));
        var success=Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.OK);
        var rejected=Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.Conflict);
        Assert.Contains("insufficient_ai_credit",await rejected.Content.ReadAsStringAsync());
        var invitation=(await Read<PanelInvitationResult>(success)).Invitation;
        Assert.Equal(50000m,(await Wallet(owner)).AvailableCredit);
        await f.Db(async db=> {Assert.Single(await db.InterviewSessions.Where(x=>x.OrganizationId==org).ToListAsync()); Assert.Single(await db.AiCreditReservations.Where(x=>x.OrganizationId==org).ToListAsync());});
        for(var i=0;i<2;i++) Assert.Equal(HttpStatusCode.NoContent,(await owner.Client.Send(HttpMethod.Post,$"/api/interviews/{invitation.InterviewId}/revoke")).StatusCode);
        Assert.Equal(300000m,(await Wallet(owner)).AvailableCredit);
    }

    [PostgresFact]
    public async Task Expired_unstarted_invitation_releases_all_and_resend_uses_new_snapshot_atomically()
    {
        var (owner,job,template,org)=await Setup();
        var invitation=(await Read<PanelInvitationResult>(await Invite(owner,job,template))).Invitation;
        f.DisplayRates.RialPerUsdt=2000000m;
        var replacement=await Read<PanelInvitationResult>(await owner.Client.Send(HttpMethod.Post,$"/api/interviews/{invitation.InterviewId}/resend",new ResendInterviewRequest()));
        await f.Db(async db=> {
            var old=await db.AiCreditReservations.SingleAsync(x=>x.InterviewSessionId==invitation.InterviewId); Assert.NotEqual(ReservationStatus.Active,old.Status);
            var fresh=await db.InterviewBillings.SingleAsync(x=>x.InterviewSessionId==replacement.Invitation.InterviewId); Assert.Equal(2000000m,fresh.RialPerUsdt);
        });
        Assert.Equal(500000m,(await Wallet(owner)).ReservedCredit);
        f.Clock.Offset=TimeSpan.FromDays(4); f.DisplayRates.Unavailable=true;
        var wallet=await Wallet(owner); Assert.Equal(1000000m,wallet.AvailableCredit); Assert.Equal(0,wallet.ReservedCredit);
        await f.Db(async db=>Assert.Equal(1000000m,(await db.OrganizationAiWallets.SingleAsync(x=>x.OrganizationId==org)).Balance));
    }

    [PostgresFact]
    public async Task Failed_resend_rolls_back_old_settlement_and_keeps_original_hold()
    {
        var (owner,job,template,org)=await Setup(300000m);
        var invitation=(await Read<PanelInvitationResult>(await Invite(owner,job,template))).Invitation;
        f.DisplayRates.RialPerUsdt=2000000m;
        var rejected=await owner.Client.Send(HttpMethod.Post,$"/api/interviews/{invitation.InterviewId}/resend",new ResendInterviewRequest());
        Assert.Equal(HttpStatusCode.Conflict,rejected.StatusCode);
        Assert.Contains("insufficient_ai_credit",await rejected.Content.ReadAsStringAsync());
        await f.Db(async db=> {
            Assert.Single(await db.InterviewSessions.Where(x=>x.OrganizationId==org).ToListAsync());
            var reservation=await db.AiCreditReservations.SingleAsync(x=>x.OrganizationId==org);
            Assert.Equal(ReservationStatus.Active,reservation.Status); Assert.Equal(250000m,reservation.ReservedAmountRial);
            Assert.False(await db.AiCreditTransactions.AnyAsync(x=>x.OrganizationId==org && x.Type==CreditTransactionType.InterviewUsage));
        });
        Assert.Equal(50000m,(await Wallet(owner)).AvailableCredit);
    }

    [PostgresFact]
    public async Task Employer_quote_has_minute_countdown_no_source_and_failure_does_not_create_invitation()
    {
        var (owner,job,template,org)=await Setup();
        var response=await owner.Client.Send(HttpMethod.Get,$"/api/interview-pricing/quote?templateId={template}");
        var quote=await Read<InvitationPriceDto>(response);
        Assert.Equal(60,quote.Rate.RefreshAfterSeconds); Assert.Equal(TimeSpan.FromMinutes(1),quote.Rate.ExpiresAtUtc-quote.Rate.FetchedAtUtc);
        Assert.Equal(250000m,quote.MaximumReservedRial); Assert.True(quote.HasSufficientCredit);
        var json=await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("Wallex",json); Assert.DoesNotContain("source",json);
        var stranger=await f.RegisterOwner(); Assert.Equal(HttpStatusCode.NotFound,(await stranger.Client.Send(HttpMethod.Get,$"/api/interview-pricing/quote?templateId={template}")).StatusCode);
        f.DisplayRates.Unavailable=true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await Invite(owner,job,template)).StatusCode);
        await f.Db(async db=> {Assert.Empty(await db.InterviewSessions.Where(x=>x.OrganizationId==org).ToListAsync()); Assert.Empty(await db.AiCreditReservations.Where(x=>x.OrganizationId==org).ToListAsync());});
        Assert.Equal(1000000m,(await Wallet(owner)).AvailableCredit);
    }
}
