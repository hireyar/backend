using System.Net;
using System.Text.Json;
using HireYar.Application.Common;
using HireYar.Infrastructure.Billing;

namespace HireYar.Tests;

public sealed class WallexDisplayRateTests
{
    [Theory]
    [InlineData("\"65000.5\"", "65000.5")]
    [InlineData("65000.5", "65000.5")]
    public void ReadsTomanWithoutRialConversion(string price, string expected)
    {
        using var json = JsonDocument.Parse(Payload(price));
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), WallexAiDisplayRateProvider.ParseRate(json.RootElement));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("\"NaN\"")]
    public void RejectsInvalidPrices(string price)
    {
        using var json = JsonDocument.Parse(Payload(price));
        Assert.Throws<FormatException>(() => WallexAiDisplayRateProvider.ParseRate(json.RootElement));
    }

    [Fact]
    public async Task SharesQuoteAndDoesNotReturnExpiredRateOnFailure()
    {
        var handler = new Handler(); var clock = new Clock();
        var provider = new WallexAiDisplayRateProvider(new Factory(handler), clock);
        var first = await provider.GetAsync(default);
        Assert.Equal(5, first.RefreshAfterSeconds);
        Assert.Equal(TimeSpan.FromSeconds(5), first.ExpiresAtUtc - first.FetchedAtUtc);
        clock.Now = clock.Now.AddSeconds(4);
        Assert.Same(first, await provider.GetAsync(default));
        Assert.Equal(1, handler.Calls);
        clock.Now = first.ExpiresAtUtc; handler.Fail = true;
        var failure = await Assert.ThrowsAsync<AppException>(() => provider.GetAsync(default));
        Assert.Equal(503, failure.Status);
        await Assert.ThrowsAsync<AppException>(() => provider.GetAsync(default));
        Assert.Equal(2, handler.Calls);
        clock.Now = clock.Now.AddSeconds(6); handler.Fail = false;
        Assert.True((await provider.GetAsync(default)).FetchedAtUtc > first.FetchedAtUtc);
    }

    private static string Payload(string price) => "{\"success\":true,\"result\":{\"symbols\":{\"USDTTMN\":{\"baseAsset\":\"USDT\",\"quoteAsset\":\"TMN\",\"stats\":{\"lastPrice\":" + price + "}}}}}";
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Factory(Handler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false) { BaseAddress = new Uri("https://api.wallex.ir/") };
    }
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls; public bool Fail;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(Fail ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
                { Content = new StringContent(Payload("\"65000.5\"")) });
        }
    }
}
