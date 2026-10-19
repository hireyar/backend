using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;

namespace HireYar.Tests;

public sealed class ProgressiveTopicRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
    private static InterviewSession Plan(bool progressive = true, bool mediumFirst = false)
    {
        var skills = Enumerable.Range(1, 5).Select(i => new InterviewSkill(Guid.NewGuid(), $"topic-{i}", true, 100, 3)).ToArray();
        var questions = skills.SelectMany((s, i) => new[] { 1, 3, 5 }.Select((d, j) =>
            new InterviewQuestionSnapshot(Guid.NewGuid(), s.SkillId, $"question-{i}-{d}", s.Name, d, true, i * 3 + j))).ToArray();
        var session = new InterviewSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            questions, skills, progressive ? (mediumFirst ? 10 : 15) : 5, 3600, Now, Now.AddDays(1));
        if (progressive) session.EnableProgressiveTopicQuestions(mediumFirst); else session.EnableTopicQuestions();
        return session;
    }
    private static InterviewAnswer Pending(InterviewSession s)
    {
        var a = s.SubmitAnswer(s.CurrentQuestionId!.Value, "synthetic", "audio/webm", 100, Now);
        s.BeginTranscription(a.Id); s.SetTranscript(a.Id, "Synthetic answer", "Test", "Test"); s.BeginEvaluation(a.Id);
        return a;
    }
    [Theory]
    [InlineData(0.7, 0.6, 5)] [InlineData(0.69, 1d, 1)] [InlineData(1d, 0.59, 1)] [InlineData(null, null, 1)]
    public void Medium_first_asks_exactly_one_branch_then_closes_topic(double? score, double? confidence, int difficulty)
    {
        var s = Plan(mediumFirst: true); var p = new InterviewPolicy();
        Assert.All(p.GetAllowedQuestions(s, Now), q => Assert.Equal(3, q.Difficulty));
        Assert.Throws<InterviewRuleException>(() => s.Start(s.Questions[0].QuestionId, Now));
        s.Start(s.Questions[1].QuestionId, Now); Grade(s, (decimal?)score, (decimal?)confidence);
        var next = Assert.Single(p.GetAllowedQuestions(s, Now)); Assert.Equal(difficulty, next.Difficulty);
        Assert.Throws<InterviewRuleException>(() => s.AskNext(s.Questions[4].QuestionId, Now));
        s.AskNext(next.QuestionId, Now); Grade(s, 1, 1);
        Assert.All(p.GetAllowedQuestions(s, Now), q => { Assert.Equal(3, q.Difficulty); Assert.NotEqual(next.Topic, q.Topic); });
        var unused = s.Questions.Single(q => q.Topic == next.Topic && q.Difficulty != 3 && q.QuestionId != next.QuestionId);
        Assert.Throws<InterviewRuleException>(() => s.AskNext(unused.QuestionId, Now));
        Assert.Equal(2, s.AnsweredQuestionCount);
    }
    private static void Grade(InterviewSession s, decimal? score, decimal? confidence)
    {
        var a = Pending(s); var q = s.Questions.Single(q => q.QuestionId == a.QuestionId);
        s.FinishAnswer(a.Id, [new(q.SkillId, 0.9m, 0.9m, "Synthetic evidence")],
            [new(q.SkillId, q.Topic, 1)], "Test", "Test", Now, score, confidence);
    }

    [Theory]
    [InlineData("sameTopicEasy", 0.9, 0.9, 5)]
    [InlineData("otherTopic", 0.9, 0.9, 5)]
    [InlineData("unknown", 0.7, 0.6, 5)]
    [InlineData("current", 0.9, 0.9, 5)]
    [InlineData("null", 0.9, 0.9, 5)]
    [InlineData("unknown", 0.69, 1.0, 1)]
    [InlineData("otherTopic", 1.0, 0.59, 1)]
    public void Medium_first_preserves_valid_grade_when_next_selection_is_invalid(string selection, double score, double confidence, int expectedDifficulty)
    {
        var s = Plan(mediumFirst: true); var policy = new InterviewPolicy();
        s.Start(policy.SelectFallback(s, Now)!.QuestionId, Now);
        var answer = Pending(s);
        var allowed = policy.GetAllowedQuestions(s, Now);
        var current = s.Questions.Single(q => q.QuestionId == s.CurrentQuestionId);
        Guid? proposed = selection switch {
            "sameTopicEasy" => allowed.Single(q => q.Difficulty == 1).QuestionId,
            "otherTopic" => s.Questions.First(q => q.Topic != current.Topic && q.Difficulty == 3).QuestionId,
            "unknown" => Guid.NewGuid(),
            "current" => current.QuestionId,
            _ => null
        };
        var decision = new InterviewDecision {
            AnswerAssessment = new() { Relevance = 1, Clarity = 1, EvidenceQuality = 1, Correctness = (decimal)score, Confidence = (decimal)confidence },
            SkillUpdates = [new() { SkillId = current.SkillId, Delta = 0.1m, Confidence = 0.9m, Evidence = "Synthetic observation" }],
            CoveredTopics = [new() { Topic = current.Topic, Coverage = 1 }], NextQuestionId = proposed,
            ShouldComplete = false, Reason = "Synthetic passed answer"
        };
        var validated = new InterviewDecisionValidator(policy).ValidateOrFallback(
            JsonSerializer.Serialize(decision, new JsonSerializerOptions(JsonSerializerDefaults.Web)), s,
            allowed.Select(q => q.QuestionId).ToArray(), Now);
        Assert.False(validated.UsedFallback);
        Assert.Equal(selection == "sameTopicEasy" ? proposed : null, validated.NextQuestionId);
        Assert.Equal((decimal)score, validated.Correctness);
        Assert.Equal((decimal)confidence, validated.AssessmentConfidence);
        Assert.Single(validated.SkillUpdates); Assert.Single(validated.CoveredTopics);
        s.FinishAnswer(answer.Id, validated.SkillUpdates, validated.CoveredTopics, "Test", "Test", Now,
            validated.Correctness, validated.AssessmentConfidence);
        var next = Assert.Single(policy.GetAllowedQuestions(s, Now));
        Assert.Equal(expectedDifficulty, next.Difficulty);
        Assert.Equal(s.Questions.Single(q => q.QuestionId == s.CurrentQuestionId).Topic, next.Topic);
        Assert.Single(s.Evidence);
    }

    [Theory]
    [InlineData("general", 1)]
    [InlineData("specialized", 4)]
    public void Medium_first_topic_order_is_fixed_only_for_hr(string interviewType, int allowedTopicCount)
    {
        var s = Plan(mediumFirst: true); s.SetInterviewType(interviewType);
        var policy = new InterviewPolicy();
        var first = policy.SelectFallback(s, Now)!;
        Assert.Equal("topic-1", first.Topic); Assert.Equal(3, first.Difficulty);
        s.Start(first.QuestionId, Now); Grade(s, 1, 1);
        s.AskNext(Assert.Single(policy.GetAllowedQuestions(s, Now)).QuestionId, Now); Grade(s, 1, 1);
        var next = policy.GetAllowedQuestions(s, Now);
        Assert.Equal(allowedTopicCount, next.Count);
        Assert.Equal("topic-2", next[0].Topic);
        Assert.All(next, q => Assert.Equal(3, q.Difficulty));
    }

    [Theory]
    [InlineData("grade")]
    [InlineData("missingGrade")]
    [InlineData("unrelatedSkill")]
    [InlineData("interaction")]
    public void Invalid_assessment_or_interaction_is_not_salvaged_by_progressive_reselection(string invalid)
    {
        var s = Plan(mediumFirst: true); s.SetInteractionLimit(2);
        var policy = new InterviewPolicy(); s.Start(policy.SelectFallback(s, Now)!.QuestionId, Now);
        var answer = Pending(s);
        var decision = new InterviewDecision {
            AnswerAssessment = new() { Relevance = 1, Clarity = 1, EvidenceQuality = 1,
                Correctness = invalid == "missingGrade" ? null : invalid == "grade" ? 1.1m : 0.9m,
                Confidence = invalid == "missingGrade" ? null : 0.9m },
            SkillUpdates = invalid == "unrelatedSkill"
                ? [new() { SkillId = Guid.NewGuid(), Delta = 0.1m, Confidence = 1, Evidence = "Synthetic" }] : [],
            CoveredTopics = [], NextQuestionId = Guid.NewGuid(), ShouldComplete = false, Reason = "Synthetic",
            Action = invalid == "interaction" ? "clarify" : "advance",
            CandidateMessage = invalid == "interaction" ? "Clarify the current question" : null
        };
        var result = new InterviewDecisionValidator(policy).ValidateOrFallback(
            JsonSerializer.Serialize(decision, new JsonSerializerOptions(JsonSerializerDefaults.Web)), s,
            policy.GetAllowedQuestions(s, Now).Select(q => q.QuestionId).ToArray(), Now);
        Assert.True(result.UsedFallback); Assert.Null(result.Correctness); Assert.Empty(result.SkillUpdates);
        s.FinishAnswer(answer.Id, result.SkillUpdates, result.CoveredTopics, "Test", "Test", Now,
            result.Correctness, result.AssessmentConfidence);
        Assert.Equal(1, Assert.Single(policy.GetAllowedQuestions(s, Now)).Difficulty);
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
