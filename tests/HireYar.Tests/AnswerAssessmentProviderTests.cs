using System.Net;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Infrastructure.Interviews;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HireYar.Tests;

public sealed class AnswerAssessmentProviderTests
{
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> action):HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>action(request,ct); }
    private static AnswerAssessmentInput Input()=>new("Synthetic role","specialized","Synthetic question","SQL",3,AnswerAssessmentProtocol.Excerpts("untrusted synthetic answer"));
    [Fact]
    public async Task New_evaluator_uses_its_independent_model_prompt_and_only_one_answers_excerpts()
    {
        using var http=new HttpClient(new Handler(async(request,ct)=> {
            Assert.Equal("synthetic-key",request.Headers.Authorization!.Parameter);
            Assert.EndsWith("/chat/completions",request.RequestUri!.AbsolutePath);
            using var doc=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));var root=doc.RootElement;
            Assert.Equal("synthetic-assessment",root.GetProperty("model").GetString());
            Assert.Equal(AnswerAssessmentProtocol.Prompt,root.GetProperty("messages")[0].GetProperty("content").GetString());
            var context=JsonSerializer.Deserialize<AnswerAssessmentInput>(root.GetProperty("messages")[1].GetProperty("content").GetString()!,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.Single(context.Excerpts);Assert.Equal(Input().Question,context.Question);
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=FakeAnswerAssessmentProvider.Valid().Json}}}}))};
        }));
        var client=new LiaraClient(http,Options.Create(new LiaraOptions{BaseUrl="https://synthetic.example.test/v1",ApiKey="synthetic-key",LlmModel="synthetic-live",AnswerAssessmentModel="synthetic-assessment"}),NullLogger<LiaraClient>.Instance);
        var result=await client.AssessAnswerAsync(Input(),default);Assert.Equal("Liara",result.Provider);Assert.Equal("synthetic-assessment",result.Model);
        Assert.Equal(80,AnswerAssessmentProtocol.Parse(result.Json,Guid.NewGuid(),Input().Excerpts).Score);
    }
    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"choices\":[]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":null}}]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":7}}]}")]
    public async Task Malformed_envelope_has_a_controlled_safe_failure(string envelope)
    {
        using var http=new HttpClient(new Handler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(envelope)})));
        var client=new NetArzClient(http,Options.Create(new NetArzOptions{ApiKey="synthetic-key"}),NullLogger<NetArzClient>.Instance);
        Assert.Equal("assessment_invalid_envelope",(await Assert.ThrowsAsync<InterviewProviderException>(()=>client.AssessAnswerAsync(Input(),default))).Code);
    }
}

public sealed class ReportNarrativeProviderTests
{
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> action):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>action(request,ct);}
    [Fact]
    public async Task Narrative_uses_the_independent_report_model_and_server_source_catalog()
    {
        var input=new ReportNarrativeInput("Synthetic role","Synthetic criteria",[new(1,"answer",Guid.NewGuid(),0,"Synthetic source")],[]);
        using var http=new HttpClient(new Handler(async(request,ct)=> {
            using var doc=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("synthetic-report-model",doc.RootElement.GetProperty("model").GetString());
            Assert.Equal(ReportNarrativeProtocol.Prompt,doc.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
            var context=JsonSerializer.Deserialize<ReportNarrativeInput>(doc.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.Equal(input.Sources[0],Assert.Single(context.Sources));
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=FakeReportNarrativeProvider.Valid(input).Json}}}}))};
        }));
        var client=new LiaraClient(http,Options.Create(new LiaraOptions{BaseUrl="https://synthetic.example.test/v1",ApiKey="synthetic-key",AnswerAssessmentModel="synthetic-report-model"}),NullLogger<LiaraClient>.Instance);
        var response=await client.GenerateNarrativeAsync(input,default);Assert.Equal("synthetic-report-model",response.Model);
        Assert.NotEmpty(ReportNarrativeProtocol.Validate(response.Json,input));
    }
}
