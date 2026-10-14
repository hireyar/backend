using System.Text.Json;
using System.Text.Json.Nodes;
using HireYar.Application.Interviews;

namespace HireYar.Tests;

public sealed class InterviewReportSourceTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static InterviewReportContext Context(string type = "specialized", string? transcript = null) => new("Backend", "Job description", null, null, "fa-IR", "Manual", [
        new(Guid.NewGuid(), Guid.NewGuid(), "Question", "Clarification only", true, "Clarified question"),
        new(Guid.NewGuid(), Guid.NewGuid(), "Question", transcript ?? "من تصمیم‌های تیم را دقیق ثبت می‌کنم.\nبا همکاران دربارهٔ راه‌حل گفت‌وگو می‌کنم.", false, null),
        new(Guid.NewGuid(), Guid.NewGuid(), "Question 2", "Second final answer", false, null)
    ], [], [], [], type);

    private static string Reply(InterviewReportContext context) => JsonSerializer.Serialize(new {
        summary = "Summary", findings = new[] { new { sourceRef="A1.1", observation="Observed answer", kind="strength" } },
        limitations = new[] { "Human review required" }, nextRoundFocus = Array.Empty<string>(),
        questionEvaluations = context.Answers.Where(a=>!a.IsInteraction).Select((a,i)=> new {
            answerRef = "A"+(i+1), sourceRef = "A"+(i+1)+".1", score=3, confidence=0.8m, feedback="Feedback", highlights=Array.Empty<string>(), gaps=Array.Empty<string>()
        }),
        metrics = InterviewReportMetricCatalog.Codes(context.InterviewType).Select(code=> new {
            code, score=3, confidence=0.8m, explanation="Explanation", evidence=new[] { new { sourceRef="A1.1" } }
        })
    }, Json);

    [Theory]
    [InlineData("general", 7)]
    [InlineData("specialized", 3)]
    public void References_resolve_to_exact_final_answer_evidence_in_existing_public_schema(string type, int metricCount)
    {
        var context = Context(type); var sources = new InterviewReportSources(context);
        Assert.Equal(2, sources.Entries.Count);
        Assert.DoesNotContain(sources.Entries, s=>s.AnswerId==context.Answers[0].AnswerId);
        var resolved = sources.Resolve(Reply(context));
        var content = InterviewReportValidator.Validate(resolved, context, true, true);
        Assert.Equal(metricCount, content.Metrics!.Count); Assert.Equal(2, content.QuestionEvaluations!.Count);
        Assert.Equal(context.Answers[1].Transcript, content.Metrics[0].Evidence[0].Quote);
        Assert.Equal(context.Answers[1].AnswerId, content.Metrics[0].Evidence[0].AnswerId);
        Assert.Equal(context.Answers[2].AnswerId, content.QuestionEvaluations[1].AnswerId);
        Assert.DoesNotContain("sourceRef", resolved); Assert.DoesNotContain("answerRef", resolved);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("wrong_answer")]
    [InlineData("invented_quote")]
    [InlineData("invented_uuid")]
    [InlineData("interaction_uuid")]
    public void Unknown_sources_and_injected_ids_quotes_and_cross_answer_evaluations_are_rejected(string invalid)
    {
        var context=Context(); var sources=new InterviewReportSources(context);
        var reply=JsonNode.Parse(Reply(context))!;
        var evaluation=reply["questionEvaluations"]![0]!.AsObject();
        switch(invalid) {
            case "unknown": reply["metrics"]![0]!["evidence"]![0]!["sourceRef"]="A99.1"; break;
            case "wrong_answer": evaluation["sourceRef"]="A2.1"; break;
            case "invented_quote": reply["metrics"]![0]!["evidence"]![0]!["quote"]="Fabricated quote"; break;
            case "invented_uuid": evaluation["answerId"]=Guid.NewGuid(); break;
            case "interaction_uuid": reply["metrics"]![0]!["evidence"]![0]!["sourceRef"]=context.Answers[0].AnswerId.ToString(); break;
        }
        var error=Assert.Throws<InterviewProviderException>(()=>sources.Resolve(reply.ToJsonString()));
        Assert.Equal("report_invalid_response",error.Code);
        Assert.DoesNotContain(context.Answers[1].Transcript!,error.ToString());
    }

    [Fact]
    public void Long_unicode_transcripts_are_split_into_exact_bounded_quotes_without_mutating_context()
    {
        var text=new string('a',499)+"🙂"+new string('ب',501)+"\nپایان";
        var context=Context(transcript:text); var sources=new InterviewReportSources(context);
        var parts=sources.Entries.Where(s=>s.AnswerRef=="A1").ToArray();
        Assert.Equal(3,parts.Length); Assert.Equal(text,string.Concat(parts.Select(s=>s.Quote)));
        Assert.All(parts,s=> { Assert.InRange(s.Quote.Length,1,500); Assert.Contains(s.Quote,text); Assert.False(char.IsHighSurrogate(s.Quote[^1])); Assert.False(char.IsLowSurrogate(s.Quote[0])); });
        Assert.Equal(text,context.Answers[1].Transcript);
        using var request=JsonDocument.Parse(sources.RequestJson(context));
        Assert.Equal(context.Answers.Count,request.RootElement.GetProperty("interview").GetProperty("answers").GetArrayLength());
        Assert.Equal(4,request.RootElement.GetProperty("evidenceSources").GetArrayLength());
    }

    [Fact]
    public void Reference_conversion_keeps_numeric_catalog_and_extra_field_validation_strict()
    {
        var context=Context(); var sources=new InterviewReportSources(context);
        foreach(var invalid in new[] { "score", "extra", "catalog" }) {
            var reply=JsonNode.Parse(Reply(context))!;
            if(invalid=="score") reply["metrics"]![0]!["score"]=99;
            if(invalid=="extra") reply["metrics"]![0]!["madeUp"]=true;
            if(invalid=="catalog") reply["metrics"]![0]!["code"]="invented_metric";
            var resolved=sources.Resolve(reply.ToJsonString());
            Assert.Throws<InterviewProviderException>(()=>InterviewReportValidator.Validate(resolved,context,true,true));
        }
    }

    [Fact]
    public void No_final_answers_keep_unknown_metrics_without_manufacturing_evidence()
    {
        var context=Context() with { Answers=[] }; var sources=new InterviewReportSources(context);
        var reply=JsonNode.Parse(Reply(context))!; reply["findings"]=new JsonArray();
        foreach(var metric in reply["metrics"]!.AsArray()) { metric!["score"]=null; metric["confidence"]=null; metric["evidence"]=new JsonArray(); }
        Assert.Empty(sources.Entries);
        var content=InterviewReportValidator.Validate(sources.Resolve(reply.ToJsonString()),context,true,true);
        Assert.Empty(content.QuestionEvaluations!); Assert.All(content.Metrics!,m=>Assert.Null(m.Score));
        Assert.Throws<InterviewProviderException>(()=>new InterviewReportSources(Context(transcript:" ")));
    }
    [Fact]
    public void Proven_redundant_fields_and_unambiguous_aliases_resolve_without_guessing_evidence()
    {
        var context=Context(); var sources=new InterviewReportSources(context); var reply=JsonNode.Parse(Reply(context))!;
        var citation=reply["metrics"]![0]!["evidence"]![0]!;
        citation["sourceRef"]=" a1 "; citation["answerId"]=context.Answers[1].AnswerId; citation["quote"]=context.Answers[1].Transcript;
        var result=InterviewReportValidator.Validate(sources.Resolve(reply.ToJsonString()),context,true,true);
        Assert.Equal(context.Answers[1].Transcript,result.Metrics![0].Evidence[0].Quote);
        var longContext=Context(transcript:new string('a',501));
        var ambiguous=new InterviewReportSources(longContext); var longReply=JsonNode.Parse(Reply(longContext))!;
        longReply["metrics"]![0]!["evidence"]![0]!["sourceRef"]="A1";
        Assert.Equal("source_reference_unknown",Assert.Throws<InterviewProviderException>(()=>ambiguous.Resolve(longReply.ToJsonString())).Reason);
    }
}
