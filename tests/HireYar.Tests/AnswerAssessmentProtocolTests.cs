using HireYar.Application.Interviews;

namespace HireYar.Tests;

public sealed class AnswerAssessmentProtocolTests
{
    [Fact]
    public void Evidence_is_built_exclusively_from_this_answers_server_excerpts()
    {
        var id = Guid.NewGuid(); var input = "A real answer, including \"untrusted\" text.";
        var output = FakeAnswerAssessmentProvider.Valid().Json.TrimEnd('}') + ",\"answerId\":\"forged\",\"quote\":\"fabricated\"}";
        var result = AnswerAssessmentProtocol.Parse(output, id, AnswerAssessmentProtocol.Excerpts(input));
        var evidence = Assert.Single(result.Evidence); Assert.Equal(id,evidence.AnswerId);Assert.Equal(input,evidence.Quote);
        Assert.Equal(0,evidence.Start);Assert.Equal(input.Length,evidence.Length);Assert.Equal(80,result.Score);
    }
    [Fact]
    public void Excerpts_preserve_exact_text_offsets_and_unicode()
    {
        var text = string.Concat(Enumerable.Repeat("پاسخ آزمایشی 😀 with punctuation.\n",150));
        var excerpts = AnswerAssessmentProtocol.Excerpts(text);
        Assert.Equal(text,string.Concat(excerpts.Select(e=>e.Text)));
        for(var i=0;i<excerpts.Count;i++) {var e=excerpts[i];Assert.Equal(i+1,e.Id);Assert.Equal(e.Text,text.Substring(e.Start,e.Text.Length));Assert.False(char.IsLowSurrogate(e.Text[0]));Assert.False(char.IsHighSurrogate(e.Text[^1]));}
    }
    [Fact]
    public void Insufficient_evidence_is_explicit_and_never_a_zero_score()
    {
        var result=AnswerAssessmentProtocol.Parse("{\"verdict\":\"insufficient_evidence\",\"score\":null,\"confidence\":null,\"explanation\":\"Insufficient context\",\"evidenceIds\":[]}",Guid.NewGuid(),[]);
        Assert.Null(result.Score);Assert.Null(result.Confidence);Assert.Empty(result.Evidence);
    }
    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"verdict\":\"scored\",\"verdict\":\"scored\"}")]
    [InlineData("{\"verdict\":\"scored\",\"score\":101,\"confidence\":0.8,\"explanation\":\"test\",\"evidenceIds\":[1]}")]
    [InlineData("{\"verdict\":\"scored\",\"score\":80,\"confidence\":1.1,\"explanation\":\"test\",\"evidenceIds\":[1]}")]
    [InlineData("{\"verdict\":\"scored\",\"score\":80,\"confidence\":0.8,\"explanation\":\"test\",\"evidenceIds\":[999]}")]
    [InlineData("{\"verdict\":\"scored\",\"score\":80,\"confidence\":0.8,\"explanation\":\"test\",\"evidenceIds\":[1,1]}")]
    [InlineData("{\"verdict\":\"scored\",\"score\":80,\"confidence\":0.8,\"explanation\":\"test\",\"evidenceIds\":[]}")]
    [InlineData("{\"verdict\":\"scored\",\"score\":\"80\",\"confidence\":0.8,\"explanation\":\"test\",\"evidenceIds\":[1]}")]
    [InlineData("{\"verdict\":\"insufficient_evidence\",\"score\":0,\"confidence\":null,\"explanation\":\"test\",\"evidenceIds\":[]}")]
    public void Invalid_outputs_are_controlled_and_never_saved(string json)
    {
        Assert.Equal("assessment_invalid_output",Assert.Throws<InterviewProviderException>(()=>AnswerAssessmentProtocol.Parse(json,Guid.NewGuid(),AnswerAssessmentProtocol.Excerpts("test"))).Code);
    }
}
