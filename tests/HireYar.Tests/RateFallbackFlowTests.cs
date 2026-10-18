using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Billing;
using HireYar.Application.Interviews;
using HireYar.Application.InterviewTemplates;
using HireYar.Application.JobPositions;
using HireYar.Infrastructure.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class RateFallbackFlowTests : IAsyncLifetime
{
    private readonly ApiFixture f = new();
    private readonly Market market = new();
    public RateFallbackFlowTests() => f.DisplayRateProviderOverride = new WallexAiDisplayRateProvider(new Factory(market), f.Clock);
    public Task InitializeAsync() => f.InitializeAsync();
    public Task DisposeAsync() => ((IAsyncLifetime)f).DisposeAsync();
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(BillingJson.Options))!;
    }

    [PostgresFact]
    public async Task Outage_returns_200_for_rate_catalog_and_employer_quote_and_reserves_same_amount_until_recovery()
    {
        var owner=await f.RegisterOwner(); await f.FundInterviewsAsync(owner); Guid org=default;
        await f.Db(async db=>org=(await db.Users.SingleAsync(x=>x.Id==owner.Id)).OrganizationId);
        await using(var scope=f.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<WalletService>().AdjustAsync(org,new(Guid.NewGuid(),5999900m,"Synthetic fallback test credit"),default);
        var job=await Read<JobDto>(await owner.Client.Send(HttpMethod.Post,"/api/panel-wizard/positions",new WizardPositionRequest("Synthetic","Synthetic role")));
        var template=await Read<TemplateDto>(await owner.Client.Send(HttpMethod.Post,"/api/panel-wizard/templates",
            new WizardTemplateRequest(job.Id,"Synthetic","general",Enumerable.Range(1,5).Select(x=>"Synthetic question "+x).ToList(),"Welcome","Goodbye",5)));
        var first=await Read<AiDisplayRateDto>(await owner.Client.Send(HttpMethod.Get,"/api/pricing/ai-display-rate"));
        Assert.False(first.IsStale);Assert.Equal(1000000m,first.RialPerUsdt);Assert.Equal(300,first.RefreshAfterSeconds);
        market.Fail=true;f.Clock.Offset=TimeSpan.FromMinutes(6);
        var stale=await Read<AiDisplayRateDto>(await owner.Client.Send(HttpMethod.Get,"/api/pricing/ai-display-rate"));
        Assert.True(stale.IsStale);Assert.Equal(first.FetchedAtUtc,stale.FetchedAtUtc);Assert.Equal(first.RialPerUsdt,stale.RialPerUsdt);
        var catalog=await owner.Client.Send(HttpMethod.Get,"/api/pricing/packages");Assert.Equal(HttpStatusCode.OK,catalog.StatusCode);
        Assert.DoesNotContain("Wallex",await catalog.Content.ReadAsStringAsync());
        var quote=await Read<InvitationPriceDto>(await owner.Client.Send(HttpMethod.Get,$"/api/interview-pricing/quote?templateId={template.Id}"));
        Assert.True(quote.Rate.IsStale);Assert.Equal(stale.ExpiresAtUtc,quote.Rate.ExpiresAtUtc);Assert.Equal(300,quote.Rate.RefreshAfterSeconds);
        Assert.Equal(300000m,quote.RatePerMinuteRial);Assert.Equal(1500000m,quote.MaximumReservedRial);
        using var client=f.CreateClient();client.DefaultRequestHeaders.Authorization=new("Bearer",owner.Client.AccessToken);
        using var form=new MultipartFormDataContent();
        foreach(var field in new Dictionary<string,string>{["jobPositionId"]=job.Id.ToString(),["templateId"]=template.Id.ToString(),
            ["firstName"]="Synthetic",["lastName"]="Candidate",["email"]="synthetic@example.test",["phone"]="09123456789",["deadlineDays"]="3",["resumeText"]="Synthetic work history"})
            form.Add(new StringContent(field.Value),field.Key);
        form.Add(new ByteArrayContent("%PDF-1.7\nSynthetic"u8.ToArray()),"resume","resume.pdf");
        var invitation=(await Read<PanelInvitationResult>(await client.PostAsync("/api/panel-wizard/invitations",form))).Invitation;
        await f.Db(async db=> {
            var bill=await db.InterviewBillings.SingleAsync(x=>x.InterviewSessionId==invitation.InterviewId);
            Assert.Equal(first.RialPerUsdt,bill.RialPerUsdt);Assert.Equal(300000m,bill.RatePerMinuteRial);
            Assert.InRange(bill.RateQuotedAtUtc,first.FetchedAtUtc.AddTicks(-10),first.FetchedAtUtc.AddTicks(10));
            Assert.Equal(1500000m,(await db.AiCreditReservations.SingleAsync(x=>x.InterviewSessionId==invitation.InterviewId)).ReservedAmountRial);
        });
        Assert.Equal(2,market.Calls);
        market.Fail=false;market.Price="110000";f.Clock.Offset=TimeSpan.FromMinutes(12);
        var recovered=await Read<AiDisplayRateDto>(await owner.Client.Send(HttpMethod.Get,"/api/pricing/ai-display-rate"));
        Assert.False(recovered.IsStale);Assert.Equal(1100000m,recovered.RialPerUsdt);Assert.Equal(3,market.Calls);
        await f.Db(async db=>Assert.Equal(300000m,(await db.InterviewBillings.SingleAsync(x=>x.InterviewSessionId==invitation.InterviewId)).RatePerMinuteRial));
    }

    private sealed class Factory(Market market):IHttpClientFactory
    {
        public HttpClient CreateClient(string name)=>new(market,false){BaseAddress=new Uri("https://api.wallex.ir/")};
    }
    private sealed class Market:HttpMessageHandler
    {
        public bool Fail;public int Calls;public string Price="100000";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            var json="{\"success\":true,\"result\":{\"symbols\":{\"USDTTMN\":{\"baseAsset\":\"USDT\",\"quoteAsset\":\"TMN\",\"stats\":{\"lastPrice\":\""+Price+"\"}}}}}";
            return Task.FromResult(new HttpResponseMessage(Fail?HttpStatusCode.ServiceUnavailable:HttpStatusCode.OK){Content=new StringContent(json)});
        }
    }
}
