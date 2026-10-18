using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using HireYar.Infrastructure.Billing;
using Microsoft.Extensions.Logging.Abstractions;

namespace HireYar.Tests;

public sealed class WallexDisplayRateTests
{
    [Theory]
    [InlineData("\"65000.5\"", "650005")]
    [InlineData("65000.5", "650005")]
    public void ConvertsWallexTomanToRial(string price, string expected)
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
    public async Task Five_minute_cache_keeps_original_fetch_time_during_repeated_failure_and_recovers()
    {
        var handler = new Handler(); var clock = new Clock();
        var provider = new WallexAiDisplayRateProvider(new Factory(handler), clock);
        var first = await provider.GetAsync(default);
        Assert.Equal("IRR",first.Currency); Assert.Equal(650005m, first.RialPerUsdt);
        Assert.Equal(300,first.RefreshAfterSeconds); Assert.Equal(TimeSpan.FromMinutes(5),first.ExpiresAtUtc-first.FetchedAtUtc);
        clock.Advance(TimeSpan.FromSeconds(299)); Assert.Same(first,await provider.GetAsync(default)); Assert.Equal(1,handler.Calls);
        clock.Advance(TimeSpan.FromSeconds(1)); handler.Failure="status";
        var stale = await provider.GetAsync(default);
        Assert.Equal(first.RialPerUsdt,stale.RialPerUsdt); Assert.Equal(first.FetchedAtUtc,stale.FetchedAtUtc); Assert.True(stale.IsStale);
        Assert.Equal(clock.Now.AddMinutes(5),stale.ExpiresAtUtc);
        var cachedReads = await Task.WhenAll(Enumerable.Range(0,20).Select(_=>provider.GetAsync(default)));
        Assert.All(cachedReads,x=>Assert.Same(stale,x)); Assert.Equal(2,handler.Calls);
        clock.Advance(TimeSpan.FromDays(1)); var older=await provider.GetAsync(default);
        Assert.Equal(first.FetchedAtUtc,older.FetchedAtUtc); Assert.True(older.IsStale); Assert.Equal(3,handler.Calls);
        clock.Advance(TimeSpan.FromMinutes(5)); handler.Failure=null; handler.Price="70000";
        var recovered=await provider.GetAsync(default); Assert.False(recovered.IsStale); Assert.Equal(700000m,recovered.RialPerUsdt);
        Assert.Equal(clock.Now,recovered.FetchedAtUtc); Assert.Equal(4,handler.Calls);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("http")]
    [InlineData("timeout")]
    [InlineData("json")]
    [InlineData("missing")]
    [InlineData("units")]
    [InlineData("zero")]
    [InlineData("io")]
    public async Task Provider_failures_preserve_last_successful_amount(string failure)
    {
        var handler=new Handler();var clock=new Clock();var provider=new WallexAiDisplayRateProvider(new Factory(handler),clock);
        var first=await provider.GetAsync(default);clock.Advance(TimeSpan.FromMinutes(5));handler.Failure=failure;
        var result=await provider.GetAsync(default);
        Assert.True(result.IsStale);Assert.Equal(first.RialPerUsdt,result.RialPerUsdt);Assert.Equal(first.FetchedAtUtc,result.FetchedAtUtc);
        Assert.Equal(300,result.RefreshAfterSeconds);Assert.Equal(clock.Now.AddMinutes(5),result.ExpiresAtUtc);
    }

    [Fact]
    public async Task Concurrent_expired_reads_share_one_refresh_and_cancellation_does_not_poison_cache()
    {
        var handler=new Handler();var clock=new Clock();var provider=new WallexAiDisplayRateProvider(new Factory(handler),clock);
        await provider.GetAsync(default);clock.Advance(TimeSpan.FromMinutes(5));handler.Block=true;
        var first=provider.GetAsync(default);await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var cancelled=new CancellationTokenSource();var waiting=provider.GetAsync(cancelled.Token);cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>waiting);
        var others=Enumerable.Range(0,20).Select(_=>provider.GetAsync(default)).ToArray();
        handler.Release.TrySetResult();var refreshed=await first;
        Assert.All(await Task.WhenAll(others),x=>Assert.Same(refreshed,x));Assert.Equal(2,handler.Calls);
    }

    [Fact]
    public async Task First_quote_retries_on_server_until_success_instead_of_returning_service_error()
    {
        var handler=new Handler{Failure="status"};var clock=new Clock();var provider=new WallexAiDisplayRateProvider(new Factory(handler),clock);
        var pending=provider.GetAsync(default);
        Assert.Equal(TimeSpan.FromSeconds(5),await clock.Delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(pending.IsCompleted);Assert.Equal(1,handler.Calls);
        handler.Failure=null;clock.Advance(TimeSpan.FromSeconds(5));
        var result=await pending.WaitAsync(TimeSpan.FromSeconds(5));Assert.False(result.IsStale);Assert.Equal(650005m,result.RialPerUsdt);Assert.Equal(2,handler.Calls);
    }

    [Fact]
    public async Task Cancellation_interrupts_first_quote_retry_without_busy_loop()
    {
        var handler=new Handler{Failure="status"};var clock=new Clock();var provider=new WallexAiDisplayRateProvider(new Factory(handler),clock);
        using var cancelled=new CancellationTokenSource();var pending=provider.GetAsync(cancelled.Token);
        await clock.Delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);Assert.Equal(1,handler.Calls);
    }

    [Fact]
    public async Task Background_refresh_runs_without_frontend_requests_and_survives_provider_failure()
    {
        var handler=new Handler();var clock=new Clock();var provider=new WallexAiDisplayRateProvider(new Factory(handler),clock);
        using var worker=new AiDisplayRateRefreshWorker(provider,clock,NullLogger<AiDisplayRateRefreshWorker>.Instance);
        await worker.StartAsync(default);
        Assert.Equal(TimeSpan.FromMinutes(5),await clock.Delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));Assert.Equal(1,handler.Calls);
        handler.Failure="status";clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.FromMinutes(5),await clock.Delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));Assert.Equal(2,handler.Calls);
        Assert.True((await provider.GetAsync(default)).IsStale);
        handler.Failure=null;handler.Price="70000";clock.Advance(TimeSpan.FromMinutes(5));
        await clock.Delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(700000m,(await provider.GetAsync(default)).RialPerUsdt);
        await worker.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static string Payload(string price) => "{\"success\":true,\"result\":{\"symbols\":{\"USDTTMN\":{\"baseAsset\":\"USDT\",\"quoteAsset\":\"TMN\",\"stats\":{\"lastPrice\":" + price + "}}}}}";
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now {get;private set;}=DateTimeOffset.UtcNow;
        public Channel<TimeSpan> Delays {get;}=Channel.CreateUnbounded<TimeSpan>();
        private readonly List<ManualTimer> timers=[];
        public override DateTimeOffset GetUtcNow()=>Now;
        public override ITimer CreateTimer(TimerCallback callback,object? state,TimeSpan dueTime,TimeSpan period)
        {
            var timer=new ManualTimer(this,callback,state,dueTime);
            lock(timers)timers.Add(timer);Delays.Writer.TryWrite(dueTime);return timer;
        }
        public void Advance(TimeSpan by)
        {
            Now+=by;ManualTimer[] due;lock(timers)due=timers.Where(x=>x.Active&&x.Due<=Now).ToArray();
            foreach(var timer in due)timer.Fire();
        }
        private sealed class ManualTimer(Clock clock,TimerCallback callback,object? state,TimeSpan dueTime):ITimer
        {
            public DateTimeOffset Due {get;private set;}=clock.Now+dueTime;
            public bool Active {get;private set;}=true;
            public void Fire(){Active=false;callback(state);}
            public bool Change(TimeSpan due,TimeSpan period){Due=clock.Now+due;Active=true;return true;}
            public void Dispose()=>Active=false;
            public ValueTask DisposeAsync(){Dispose();return ValueTask.CompletedTask;}
        }
    }
    private sealed class Factory(Handler handler):IHttpClientFactory
    {
        public HttpClient CreateClient(string name)=>new(handler,false){BaseAddress=new Uri("https://api.wallex.ir/")};
    }
    private sealed class Handler:HttpMessageHandler
    {
        public int Calls;public string? Failure;public string Price="65000.5";public bool Block;
        public TaskCompletionSource Entered {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            if(Block){Entered.TrySetResult();await Release.Task.WaitAsync(ct);}
            if(Failure=="http")throw new HttpRequestException();
            if(Failure=="timeout")throw new TaskCanceledException();
            if(Failure=="io")throw new IOException();
            var payload=Failure switch{"json"=>"{", "missing"=>"{}", "zero"=>Payload("0"),"units"=>Payload(Price).Replace("TMN","USD"),_=>Payload(Price)};
            return new HttpResponseMessage(Failure=="status"?HttpStatusCode.ServiceUnavailable:HttpStatusCode.OK){Content=new StringContent(payload)};
        }
    }
}
