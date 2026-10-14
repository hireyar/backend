using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using HireYar.Application.Interviews;
using HireYar.Infrastructure.Interviews;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HireYar.Tests;

public sealed class InterviewReportRepairTests
{
    private static InterviewReportContext Context() => new("Role","Job description",null,null,"fa-IR","Manual",
        [new(Guid.NewGuid(),Guid.NewGuid(),"Question","پاسخ اصلی برای شاهد",false,null)],[],[],[]);
    private static string Valid() => JsonSerializer.Serialize(new {
        summary="Summary",findings=Array.Empty<object>(),limitations=new[]{"Limited evidence"},nextRoundFocus=Array.Empty<string>(),
        questionEvaluations=new[]{new {answerRef="A1",sourceRef="A1.1",score=3,confidence=0.8m,feedback="Feedback",highlights=Array.Empty<string>(),gaps=Array.Empty<string>()}},
        metrics=InterviewReportMetricCatalog.Codes("specialized").Select(code=>new {code,score=3,confidence=0.8m,explanation="Explanation",evidence=new[]{new {sourceRef="A1.1"}}})
    });
    private sealed class Handler(string error, bool alwaysInvalid=false, bool blockRepair=false) : HttpMessageHandler
    {
        public int Calls;
        public TaskCompletionSource RepairEntered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++;
            using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            if(Calls==2) {
                Assert.Equal(4,body.RootElement.GetProperty("messages").GetArrayLength());
                Assert.Contains("failed server validation",body.RootElement.GetProperty("messages")[3].GetProperty("content").GetString());
                RepairEntered.TrySetResult();
                if(blockRepair) await Task.Delay(Timeout.InfiniteTimeSpan,ct);
            }
            if(error=="http") return new(HttpStatusCode.ServiceUnavailable);
            var reply=Valid();
            if(Calls==1||alwaysInvalid) {
                var node=JsonNode.Parse(reply)!;
                if(error=="reference") node["metrics"]![0]!["evidence"]![0]!["sourceRef"]="A999.1";
                if(error=="numeric") node["metrics"]![0]!["score"]=99;
                reply=error=="json"?"not-json":node.ToJsonString();
            }
            return new(HttpStatusCode.OK){Content=JsonContent.Create(new {choices=new[]{new {message=new {content=reply}}}})};
        }
    }
    private static LiaraClient Provider(HttpClient http,int timeout=90)=>new(http,Options.Create(new LiaraOptions {
        BaseUrl="https://synthetic.invalid/v1",ApiKey="synthetic",EvaluationTimeoutSeconds=timeout
    }),NullLogger<LiaraClient>.Instance);

    [Theory]
    [InlineData("reference")]
    [InlineData("numeric")]
    [InlineData("json")]
    public async Task Invalid_output_is_repaired_once_and_revalidated_completely(string error)
    {
        var context=Context();var handler=new Handler(error);using var http=new HttpClient(handler);
        var result=await Provider(http).GenerateReportAsync(context,default);
        Assert.Equal(2,handler.Calls);
        var content=InterviewReportValidator.Validate(result.Json,context,true,true);
        Assert.Equal(context.Answers[0].Transcript,content.Metrics![0].Evidence[0].Quote);
    }

    [Fact]
    public async Task Valid_first_output_needs_no_repair_and_both_adapters_share_the_behavior()
    {
        foreach(var liara in new[]{true,false}) {
            var handler=new Handler("none");using var http=new HttpClient(handler);
            IInterviewReportProvider provider=liara?Provider(http):new NetArzClient(http,Options.Create(new NetArzOptions{ApiKey="synthetic"}),NullLogger<NetArzClient>.Instance);
            var context=Context();var result=await provider.GenerateReportAsync(context,default);
            Assert.Equal(1,handler.Calls);InterviewReportValidator.Validate(result.Json,context,true,true);
        }
    }

    [Fact]
    public async Task Persistently_invalid_reference_stops_after_two_calls_without_fabricating_a_report()
    {
        var handler=new Handler("reference",alwaysInvalid:true);using var http=new HttpClient(handler);
        var error=await Assert.ThrowsAsync<InterviewProviderException>(()=>Provider(http).GenerateReportAsync(Context(),default));
        Assert.Equal(2,handler.Calls);Assert.Equal("report_invalid_response",error.Code);Assert.Equal("source_reference_unknown",error.Reason);
    }

    [Fact]
    public async Task Http_failure_is_not_retried_as_output_repair()
    {
        var handler=new Handler("http");using var http=new HttpClient(handler);
        Assert.Equal("report_http_503",(await Assert.ThrowsAsync<InterviewProviderException>(()=>Provider(http).GenerateReportAsync(Context(),default))).Code);
        Assert.Equal(1,handler.Calls);
    }

    [Fact]
    public async Task Repair_shares_original_time_budget_and_honors_caller_cancellation()
    {
        var handler=new Handler("reference",blockRepair:true);using var http=new HttpClient(handler);
        var error=await Assert.ThrowsAsync<InterviewProviderException>(()=>Provider(http,timeout:1).GenerateReportAsync(Context(),default));
        Assert.Equal("report_timeout",error.Code);Assert.Equal(2,handler.Calls);
        var stoppedHandler=new Handler("reference",blockRepair:true);using var stoppedHttp=new HttpClient(stoppedHandler);
        using var stopped=new CancellationTokenSource();var pending=Provider(stoppedHttp).GenerateReportAsync(Context(),stopped.Token);
        await stoppedHandler.RepairEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));stopped.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);Assert.Equal(2,stoppedHandler.Calls);
    }
}
