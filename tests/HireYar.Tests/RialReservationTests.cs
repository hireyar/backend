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

    private async Task<(Account Owner, Guid Job, Guid Template, Guid Org)> Setup(decimal balance = 6000000m)
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
    private async Task<HttpResponseMessage> Invite(Account owner, Guid job, Guid template, string interviewType = "general")
    {
        using var client = f.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", owner.Client.AccessToken);
        using var form = new MultipartFormDataContent();
        foreach (var field in new Dictionary<string,string> { ["jobPositionId"]=job.ToString(), ["templateId"]=template.ToString(), ["interviewType"]=interviewType,
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
        Assert.Equal(6000000m,held.Balance); Assert.Equal(1500000m,held.ReservedCredit); Assert.Equal(4500000m,held.AvailableCredit);
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
            Assert.Equal(1000000m,bill.RialPerUsdt); Assert.Equal(300000m,bill.RatePerMinuteRial);
            Assert.True(bill.BillableSeconds>0); cost=BillingMath.Cost(bill.BillableSeconds,300000m);
            Assert.Equal(cost,bill.BilledAmountRial);
            Assert.Equal(BillingMath.Cost(bill.BillableSeconds,.3m),bill.BilledAmountUsd);
            var usage = await db.AiCreditTransactions.SingleAsync(x=>x.OrganizationId==org && x.Type==CreditTransactionType.InterviewUsage);
            Assert.Equal(-cost,usage.Amount); Assert.Equal(-bill.BilledAmountUsd,usage.AmountUsd);
            Assert.Equal(6000000m-cost,(await db.OrganizationAiWallets.SingleAsync(x=>x.OrganizationId==org)).Balance);
            Assert.False(db.Database.HasPendingModelChanges());
        });
        var after=await Wallet(owner); Assert.Equal(0m,after.ReservedCredit); Assert.Equal(6000000m-cost,after.AvailableCredit);
        foreach(var url in new[]{"/api/billing/ai-usage","/api/billing/ai-credit/transactions","/api/billing/financial-history"}) {
            var response=await owner.Client.Send(HttpMethod.Get,url); Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            var output=await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("amountUsd",output); Assert.DoesNotContain("billedAmountUsd",output);
        }
    }

    [PostgresFact]
    public async Task Concurrent_invitations_cannot_overspend_and_revocation_releases_hold_idempotently()
    {
        var (owner,job,template,org)=await Setup(1800000m);
        var responses=await Task.WhenAll(Invite(owner,job,template),Invite(owner,job,template));
        var success=Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.OK);
        var rejected=Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.Conflict);
        Assert.Contains("insufficient_ai_credit",await rejected.Content.ReadAsStringAsync());
        var invitation=(await Read<PanelInvitationResult>(success)).Invitation;
        Assert.Equal(300000m,(await Wallet(owner)).AvailableCredit);
        await f.Db(async db=> {Assert.Single(await db.InterviewSessions.Where(x=>x.OrganizationId==org).ToListAsync()); Assert.Single(await db.AiCreditReservations.Where(x=>x.OrganizationId==org).ToListAsync());});
        for(var i=0;i<2;i++) Assert.Equal(HttpStatusCode.NoContent,(await owner.Client.Send(HttpMethod.Post,$"/api/interviews/{invitation.InterviewId}/revoke")).StatusCode);
        Assert.Equal(1800000m,(await Wallet(owner)).AvailableCredit);
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
        Assert.Equal(3000000m,(await Wallet(owner)).ReservedCredit);
        f.Clock.Offset=TimeSpan.FromDays(4); f.DisplayRates.Unavailable=true;
        var wallet=await Wallet(owner); Assert.Equal(6000000m,wallet.AvailableCredit); Assert.Equal(0,wallet.ReservedCredit);
        await f.Db(async db=>Assert.Equal(6000000m,(await db.OrganizationAiWallets.SingleAsync(x=>x.OrganizationId==org)).Balance));
    }

    [PostgresFact]
    public async Task Failed_resend_rolls_back_old_settlement_and_keeps_original_hold()
    {
        var (owner,job,template,org)=await Setup(1800000m);
        var invitation=(await Read<PanelInvitationResult>(await Invite(owner,job,template))).Invitation;
        f.DisplayRates.RialPerUsdt=2000000m;
        var rejected=await owner.Client.Send(HttpMethod.Post,$"/api/interviews/{invitation.InterviewId}/resend",new ResendInterviewRequest());
        Assert.Equal(HttpStatusCode.Conflict,rejected.StatusCode);
        Assert.Contains("insufficient_ai_credit",await rejected.Content.ReadAsStringAsync());
        await f.Db(async db=> {
            Assert.Single(await db.InterviewSessions.Where(x=>x.OrganizationId==org).ToListAsync());
            var reservation=await db.AiCreditReservations.SingleAsync(x=>x.OrganizationId==org);
            Assert.Equal(ReservationStatus.Active,reservation.Status); Assert.Equal(1500000m,reservation.ReservedAmountRial);
            Assert.False(await db.AiCreditTransactions.AnyAsync(x=>x.OrganizationId==org && x.Type==CreditTransactionType.InterviewUsage));
        });
        Assert.Equal(300000m,(await Wallet(owner)).AvailableCredit);
    }

    [PostgresFact]
    public async Task Employer_quote_has_five_minute_countdown_no_source_and_invalid_provider_does_not_create_invitation()
    {
        var (owner,job,template,org)=await Setup();
        var response=await owner.Client.Send(HttpMethod.Get,$"/api/interview-pricing/quote?templateId={template}");
        var quote=await Read<InvitationPriceDto>(response);
        Assert.Equal(300,quote.Rate.RefreshAfterSeconds); Assert.Equal(TimeSpan.FromMinutes(5),quote.Rate.ExpiresAtUtc-quote.Rate.FetchedAtUtc);
        Assert.Equal(1500000m,quote.MaximumReservedRial); Assert.True(quote.HasSufficientCredit);
        var json=await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("Wallex",json); Assert.DoesNotContain("source",json);
        var stranger=await f.RegisterOwner(); Assert.Equal(HttpStatusCode.NotFound,(await stranger.Client.Send(HttpMethod.Get,$"/api/interview-pricing/quote?templateId={template}")).StatusCode);
        f.DisplayRates.Unavailable=true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await Invite(owner,job,template)).StatusCode);
        await f.Db(async db=> {Assert.Empty(await db.InterviewSessions.Where(x=>x.OrganizationId==org).ToListAsync()); Assert.Empty(await db.AiCreditReservations.Where(x=>x.OrganizationId==org).ToListAsync());});
        Assert.Equal(6000000m,(await Wallet(owner)).AvailableCredit);
    }
    [PostgresFact]
    public async Task Packages_control_access_but_type_tariff_is_fixed_despite_catalog_price_changes()
    {
        var (owner,job,template,org)=await Setup(); Guid basic=default; Guid advanced=default;
        await f.Db(async db=> {
            basic=await db.PricingPackages.Where(x=>x.Code=="basic").Select(x=>x.Id).SingleAsync();
            advanced=await db.PricingPackages.Where(x=>x.Code=="advanced").Select(x=>x.Id).SingleAsync();
            await db.PricingPackages.Where(x=>x.Id==advanced).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.AiPricePerMinuteUsd,.2m));
        });
        var url=$"/api/interview-pricing/quote?templateId={template}";
        var current=await Read<InvitationPriceDto>(await owner.Client.Send(HttpMethod.Get,url));
        var selectedBasic=await Read<InvitationPriceDto>(await owner.Client.Send(HttpMethod.Get,url+$"&packageId={basic}"));
        var selectedAdvanced=await Read<InvitationPriceDto>(await owner.Client.Send(HttpMethod.Get,url+$"&packageId={advanced}"));
        Assert.Equal(300000m,current.RatePerMinuteRial);Assert.Equal(1500000m,current.MaximumReservedRial);
        Assert.Equal(300000m,selectedBasic.RatePerMinuteRial);Assert.Equal(1500000m,selectedBasic.MaximumReservedRial);
        Assert.Equal(300000m,selectedAdvanced.RatePerMinuteRial);Assert.Equal(1500000m,selectedAdvanced.MaximumReservedRial);
        Assert.True(selectedAdvanced.HasSufficientCredit);Assert.Equal(300,selectedBasic.Rate.RefreshAfterSeconds);
        await f.Db(async db=> {Assert.Empty(await db.InterviewSessions.Where(x=>x.OrganizationId==org).ToListAsync());Assert.Empty(await db.AiCreditReservations.Where(x=>x.OrganizationId==org).ToListAsync());});
        var invitation=(await Read<PanelInvitationResult>(await Invite(owner,job,template))).Invitation;
        await f.Db(async db=> {
            var bill=await db.InterviewBillings.SingleAsync(x=>x.InterviewSessionId==invitation.InterviewId);
            Assert.Equal(.3m,bill.RatePerMinuteUsd);Assert.Equal(300000m,bill.RatePerMinuteRial);
            Assert.Equal(1500000m,(await db.AiCreditReservations.SingleAsync(x=>x.InterviewSessionId==invitation.InterviewId)).ReservedAmountRial);
        });
    }

    [PostgresFact]
    public async Task Optional_package_rejects_hidden_inactive_unsaleable_unknown_and_foreign_template()
    {
        var (owner,_,template,_)=await Setup();Guid basic=default;
        await f.Db(async db=>basic=await db.PricingPackages.Where(x=>x.Code=="basic").Select(x=>x.Id).SingleAsync());
        var url=$"/api/interview-pricing/quote?templateId={template}";
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.Client.Send(HttpMethod.Get,url+"&packageId=invalid")).StatusCode);
        foreach(var missing in new[]{Guid.Empty,Guid.NewGuid()})Assert.Equal(HttpStatusCode.NotFound,(await owner.Client.Send(HttpMethod.Get,url+$"&packageId={missing}")).StatusCode);
        await f.Db(db=>db.PricingPackages.Where(x=>x.Id==basic).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsPublic,false)));
        Assert.Equal(HttpStatusCode.NotFound,(await owner.Client.Send(HttpMethod.Get,url+$"&packageId={basic}")).StatusCode);
        await f.Db(db=>db.PricingPackages.Where(x=>x.Id==basic).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsPublic,true).SetProperty(x=>x.IsActive,false)));
        Assert.Equal(HttpStatusCode.NotFound,(await owner.Client.Send(HttpMethod.Get,url+$"&packageId={basic}")).StatusCode);
        await f.Db(db=>db.PricingPackages.Where(x=>x.Id==basic).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,true)));
        await f.Db(db=>db.PackagePeriods.Where(x=>x.PackageId==basic).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,false)));
        Assert.Equal(HttpStatusCode.NotFound,(await owner.Client.Send(HttpMethod.Get,url+$"&packageId={basic}")).StatusCode);
        var stranger=await f.RegisterOwner();Assert.Equal(HttpStatusCode.NotFound,(await stranger.Client.Send(HttpMethod.Get,url+$"&packageId={basic}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Browser().Send(HttpMethod.Get,url+$"&packageId={basic}")).StatusCode);
    }

    [PostgresFact]
    public async Task Selected_package_checks_its_own_capabilities_and_can_be_previewed_without_active_subscription()
    {
        var (owner,_,template,org)=await Setup();Guid basic=default;
        await f.Db(async db=> {
            basic=await db.PricingPackages.Where(x=>x.Code=="basic").Select(x=>x.Id).SingleAsync();
        });
        // Purchased financial history is immutable; simulate natural expiry with the service clock.
        f.Clock.Offset=TimeSpan.FromDays(40);
        var url=$"/api/interview-pricing/quote?templateId={template}";
        Assert.Equal(HttpStatusCode.Forbidden,(await owner.Client.Send(HttpMethod.Get,url)).StatusCode);
        var selected=await Read<InvitationPriceDto>(await owner.Client.Send(HttpMethod.Get,url+$"&packageId={basic}"));
        Assert.Equal(300000m,selected.RatePerMinuteRial);
        await f.Db(async db=> {
            var feature=await db.FeatureDefinitions.Where(x=>x.Code=="ai_interview").Select(x=>x.Id).SingleAsync();
            await db.PackageFeatures.Where(x=>x.PackageId==basic&&x.FeatureId==feature).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsEnabled,false));
        });
        var denied=await owner.Client.Send(HttpMethod.Get,url+$"&packageId={basic}");
        Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);Assert.Contains("package_feature_required",await denied.Content.ReadAsStringAsync());
        await f.Db(async db=> {Assert.Empty(await db.InterviewSessions.Where(x=>x.OrganizationId==org).ToListAsync());Assert.Empty(await db.AiCreditReservations.Where(x=>x.OrganizationId==org).ToListAsync());});
    }

    [PostgresFact]
    public async Task Explicit_interview_type_overrides_template_type_for_preview_and_validates_selected_package_access()
    {
        var (owner,_,template,_)=await Setup();Guid basic=default;Guid advanced=default;
        await f.Db(async db=> {
            basic=await db.PricingPackages.Where(x=>x.Code=="basic").Select(x=>x.Id).SingleAsync();
            advanced=await db.PricingPackages.Where(x=>x.Code=="advanced").Select(x=>x.Id).SingleAsync();
            var row=await db.InterviewTemplates.SingleAsync(x=>x.Id==template);row.InterviewType="specialized";await db.SaveChangesAsync();
        });
        var url=$"/api/interview-pricing/quote?templateId={template}";
        var general=await Read<InvitationPriceDto>(await owner.Client.Send(HttpMethod.Get,url+$"&packageId={basic}&interviewType=general"));
        Assert.Equal("general",general.InterviewType);Assert.Equal(.3m,general.RatePerMinuteUsd);Assert.Equal(300000m,general.RatePerMinuteRial);Assert.Equal(1500000m,general.MaximumReservedRial);
        var specialized=await Read<InvitationPriceDto>(await owner.Client.Send(HttpMethod.Get,url+$"&packageId={advanced}&interviewType=specialized"));
        Assert.Equal("specialized",specialized.InterviewType);Assert.Equal(.8m,specialized.RatePerMinuteUsd);Assert.Equal(800000m,specialized.RatePerMinuteRial);Assert.Equal(4000000m,specialized.MaximumReservedRial);
        Assert.Equal(HttpStatusCode.Forbidden,(await owner.Client.Send(HttpMethod.Get,url+$"&packageId={basic}&interviewType=specialized")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.Client.Send(HttpMethod.Get,url+"&interviewType=invalid")).StatusCode);
        var legacy=await Read<InvitationPriceDto>(await owner.Client.Send(HttpMethod.Get,url));Assert.Equal(.8m,legacy.RatePerMinuteUsd);
    }

    [PostgresFact]
    public async Task Actual_invitation_matches_selected_type_quote_for_general_and_specialized_on_same_template()
    {
        var (owner,job,template,org)=await Setup();
        foreach(var type in new[]{"general","specialized"})
        {
            var quote=await Read<InvitationPriceDto>(await owner.Client.Send(HttpMethod.Get,$"/api/interview-pricing/quote?templateId={template}&interviewType={type}"));
            var invitation=(await Read<PanelInvitationResult>(await Invite(owner,job,template,type))).Invitation;
            await f.Db(async db=> {
                var bill=await db.InterviewBillings.SingleAsync(x=>x.InterviewSessionId==invitation.InterviewId);
                var session=await db.InterviewSessions.SingleAsync(x=>x.Id==invitation.InterviewId);
                Assert.Equal(type=="general"?"hr_interview":"technical_interview",session.RequiredCapabilityCode);
                Assert.Equal(type=="general"?.3m:.8m,bill.RatePerMinuteUsd);Assert.Equal(quote.RatePerMinuteUsd,bill.RatePerMinuteUsd);Assert.Equal(quote.RatePerMinuteRial,bill.RatePerMinuteRial);
                Assert.Equal(quote.MaximumReservedRial,(await db.AiCreditReservations.SingleAsync(x=>x.InterviewSessionId==invitation.InterviewId)).ReservedAmountRial);
            });
        }
        Assert.Equal(5500000m,(await Wallet(owner)).ReservedCredit);
    }

    [PostgresFact]
    public async Task Database_tariff_changes_affect_catalog_quote_and_new_invites_but_keep_existing_bill()
    {
        var (owner,job,template,org)=await Setup(12000000m);
        var first=(await Read<PanelInvitationResult>(await Invite(owner,job,template))).Invitation;
        await f.Db(db=>db.InterviewTariffs.ExecuteUpdateAsync(s=>s.SetProperty(x=>x.RatePerMinuteUsd,x=>x.InterviewType=="general"?.4m:.9m)));
        await f.Db(async db=> {
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlRawAsync("UPDATE \"InterviewTariffs\" SET \"RatePerMinuteUsd\" = 0 WHERE \"InterviewType\" = 'general'"));
            await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlRawAsync("INSERT INTO \"InterviewTariffs\" (\"InterviewType\", \"RatePerMinuteUsd\") VALUES ('unknown', 1)"));
        });
        var catalog=await Read<PricingResponse>(await owner.Client.Send(HttpMethod.Get,"/api/pricing/packages"));
        Assert.All(catalog.Packages,p=> {
            Assert.Equal(.4m,p.GeneralAiPricePerMinuteUsd);Assert.Equal(.9m,p.SpecializedAiPricePerMinuteUsd);
            Assert.Equal(400000m,p.GeneralAiPricePerMinuteRial);Assert.Equal(900000m,p.SpecializedAiPricePerMinuteRial);
        });
        foreach(var type in new[]{"general","specialized"})
        {
            var quote=await Read<InvitationPriceDto>(await owner.Client.Send(HttpMethod.Get,$"/api/interview-pricing/quote?templateId={template}&interviewType={type}"));
            Assert.Equal(type=="general"?.4m:.9m,quote.RatePerMinuteUsd);
            var invite=(await Read<PanelInvitationResult>(await Invite(owner,job,template,type))).Invitation;
            await f.Db(async db=> {
                var bill=await db.InterviewBillings.SingleAsync(x=>x.InterviewSessionId==invite.InterviewId);
                Assert.Equal(quote.RatePerMinuteUsd,bill.RatePerMinuteUsd);Assert.Equal(quote.RatePerMinuteRial,bill.RatePerMinuteRial);
                Assert.Equal(quote.MaximumReservedRial,(await db.AiCreditReservations.SingleAsync(x=>x.InterviewSessionId==invite.InterviewId)).ReservedAmountRial);
                var old=await db.InterviewBillings.SingleAsync(x=>x.InterviewSessionId==first.InterviewId);
                Assert.Equal(.3m,old.RatePerMinuteUsd);Assert.Equal(300000m,old.RatePerMinuteRial);
            });
        }
    }

    [PostgresFact]
    public async Task Missing_tariff_does_not_fall_back_to_hardcoded_or_package_rate_or_create_reservation()
    {
        var (owner,job,template,org)=await Setup();
        await f.Db(db=>db.InterviewTariffs.Where(x=>x.InterviewType=="general").ExecuteDeleteAsync());
        var quote=await owner.Client.Send(HttpMethod.Get,$"/api/interview-pricing/quote?templateId={template}&interviewType=general");
        Assert.Equal(HttpStatusCode.ServiceUnavailable,quote.StatusCode);Assert.Contains("interview_tariff_unavailable",await quote.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await Invite(owner,job,template)).StatusCode);
        await f.Db(async db=> {
            Assert.Empty(await db.InterviewBillings.Where(x=>x.OrganizationId==org).ToListAsync());
            Assert.Empty(await db.AiCreditReservations.Where(x=>x.OrganizationId==org).ToListAsync());
            Assert.Empty(await db.InterviewSessions.Where(x=>x.OrganizationId==org).ToListAsync());
        });
    }

}
