using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;

namespace HireYar.Tests;

public sealed class ProgressiveTopicRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
    private static InterviewSession Plan(bool progressive = true)
    {
        var skills = Enumerable.Range(1, 5).Select(i => new InterviewSkill(Guid.NewGuid(), $"topic-{i}", true, 100, 3)).ToArray();
        var questions = skills.SelectMany((s, i) => new[] { 1, 3, 5 }.Select((d, j) =>
            new InterviewQuestionSnapshot(Guid.NewGuid(), s.SkillId, $"question-{i}-{d}", s.Name, d, true, i * 3 + j))).ToArray();
        var session = new InterviewSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            questions, skills, progressive ? 15 : 5, 3600, Now, Now.AddDays(1));
        if (progressive) session.EnableProgressiveTopicQuestions(); else session.EnableTopicQuestions();
        return session;
    }
    private static InterviewAnswer Pending(InterviewSession s)
    {
        var a = s.SubmitAnswer(s.CurrentQuestionId!.Value, "synthetic", "audio/webm", 100, Now);
        s.BeginTranscription(a.Id); s.SetTranscript(a.Id, "Synthetic answer", "Test", "Test"); s.BeginEvaluation(a.Id);
        return a;
    }
    private static void Grade(InterviewSession s, decimal? score, decimal? confidence)
    {
        var a = Pending(s); var q = s.Questions.Single(q => q.QuestionId == a.QuestionId);
        s.FinishAnswer(a.Id, [new(q.SkillId, 0.9m, 0.9m, "Synthetic evidence")],
            [new(q.SkillId, q.Topic, 1)], "Test", "Test", Now, score, confidence);
    }
    [Fact]
    public void Passed_topic_keeps_next_level_despite_coverage_and_sufficient_evidence()
    {
        var s = Plan(); var policy = new InterviewPolicy();
        Assert.All(policy.GetAllowedQuestions(s, Now), q => Assert.Equal(1, q.Difficulty));
        s.Start(s.Questions[0].QuestionId, Now);
        Assert.Throws<InterviewRuleException>(() => s.Start(s.Questions[2].QuestionId, Now));
        Grade(s, 0.7m, 0.6m);
        Assert.Equal(s.Questions[1].QuestionId, Assert.Single(policy.GetAllowedQuestions(s, Now)).QuestionId);
        Assert.Throws<InterviewRuleException>(() => s.AskNext(s.Questions[3].QuestionId, Now));
        Assert.Throws<InterviewRuleException>(() => s.AskNext(s.Questions[2].QuestionId, Now));
        s.AskNext(s.Questions[1].QuestionId, Now); Grade(s, 1, 1);
        Assert.Equal(s.Questions[2].QuestionId, Assert.Single(policy.GetAllowedQuestions(s, Now)).QuestionId);
        Assert.Null(policy.GetCompletionReason(s, Now, 1));
        s.AskNext(s.Questions[2].QuestionId, Now); Grade(s, 1, 1);
        Assert.All(policy.GetAllowedQuestions(s, Now), q => { Assert.Equal(1, q.Difficulty); Assert.NotEqual(s.Questions[0].Topic, q.Topic); });
    }
    [Theory]
    [InlineData(0.69, 1d)] [InlineData(1d, 0.59)] [InlineData(null, null)]
    public void Failed_or_unknown_answer_closes_topic_independent_of_cumulative_score(double? score, double? confidence)
    {
        var s = Plan(); s.Start(s.Questions[0].QuestionId, Now);
        Grade(s, (decimal?)score, (decimal?)confidence);
        Assert.All(new InterviewPolicy().GetAllowedQuestions(s, Now), q => { Assert.Equal(1, q.Difficulty); Assert.NotEqual(s.Questions[0].Topic, q.Topic); });
        Assert.Throws<InterviewRuleException>(() => s.AskNext(s.Questions[1].QuestionId, Now));
        Assert.Throws<InterviewRuleException>(() => s.AskNext(s.Questions[2].QuestionId, Now));
    }
    [Fact]
    public void Legacy_plan_keeps_medium_and_one_question_per_topic()
    {
        var s = Plan(false); var p = new InterviewPolicy();
        Assert.Equal(3, p.SelectFallback(s, Now)!.Difficulty);
        s.Start(s.Questions[1].QuestionId, Now); Grade(s, 1, 1);
        Assert.DoesNotContain(p.GetAllowedQuestions(s, Now), q => q.Topic == s.Questions[0].Topic);
        Assert.Throws<InterviewRuleException>(() => s.AskNext(s.Questions[2].QuestionId, Now));
    }
    [Theory]
    [InlineData(null, null, true)] [InlineData(1.1, 1d, true)] [InlineData(1d, null, true)]
    [InlineData(0.7, 0.6, false)]
    public void Validator_requires_valid_per_answer_grade_for_progression(double? score, double? confidence, bool fallback)
    {
        var s = Plan(); s.Start(s.Questions[0].QuestionId, Now); Pending(s);
        var decision = new InterviewDecision { AnswerAssessment = new() { Relevance = 1, Clarity = 1, EvidenceQuality = 1,
            Correctness = (decimal?)score, Confidence = (decimal?)confidence }, SkillUpdates = [], CoveredTopics = [],
            NextQuestionId = null, ShouldComplete = true, Reason = "Synthetic assessment" };
        var p = new InterviewPolicy(); var result = new InterviewDecisionValidator(p).ValidateOrFallback(
            JsonSerializer.Serialize(decision, new JsonSerializerOptions(JsonSerializerDefaults.Web)), s,
            p.GetAllowedQuestions(s, Now).Select(q => q.QuestionId).ToArray(), Now);
        Assert.Equal(fallback, result.UsedFallback);
        Assert.Null(result.CompletionReason);
        Assert.Equal(fallback ? null : (decimal?)score, result.Correctness);
    }
    [Fact]
    public void Clarification_does_not_close_topic_or_advance_level()
    {
        var s = Plan(); s.SetInteractionLimit(2); s.Start(s.Questions[0].QuestionId, Now);
        var a = Pending(s); s.ContinueQuestion(a.Id, "Clarify the question", "Test", "Test", Now);
        Assert.Equal(s.Questions[0].QuestionId, s.CurrentQuestionId);
        Assert.Equal(0, s.AnsweredQuestionCount); Assert.Null(a.Correctness);
        Grade(s, 1, 1);
        Assert.Equal(s.Questions[1].QuestionId, Assert.Single(new InterviewPolicy().GetAllowedQuestions(s, Now)).QuestionId);
    }
    [Fact]
    public void High_confidence_in_all_skills_does_not_end_before_last_hard_question()
    {
        var s = Plan(); var p = new InterviewPolicy(); s.Start(s.Questions[0].QuestionId, Now);
        for (var i = 0; i < 15; i++)
        {
            Grade(s, 1, 1);
            if (i == 14) Assert.Equal(InterviewCompletionReason.QuestionLimit, p.GetCompletionReason(s, Now));
            else { Assert.Null(p.GetCompletionReason(s, Now)); s.AskNext(p.SelectFallback(s, Now)!.QuestionId, Now); }
        }
    }
}
