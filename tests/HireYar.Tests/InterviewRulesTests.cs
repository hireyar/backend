using System.Text.Json;
using System.Text.Json.Nodes;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;

namespace HireYar.Tests;

public sealed class InterviewRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Sql = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Architecture = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static InterviewQuestionSnapshot Q(int id, Guid? skill = null, string topic = "indexes", bool active = true, int difficulty = 3) =>
        new(new Guid(id, 0, 0, new byte[8]), skill ?? Sql, "Explain indexes", topic, difficulty, active);

    private static InterviewSession Session(int maximum = 12, params InterviewQuestionSnapshot[] questions) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            questions.Length == 0 ? [Q(1), Q(2, topic: "transactions"), Q(3, Architecture, "design")] : questions,
            [new(Sql, "SQL", true, 100), new(Architecture, "Architecture", true, 50)],
            maximum, 1800, Now, Now.AddDays(1));

    private static InterviewAnswer Answer(InterviewSession session)
    {
        session.Start(session.Questions[0].QuestionId, Now);
        var answer = session.SubmitAnswer(session.CurrentQuestionId!.Value, "private/audio", "audio/webm", 100, Now.AddSeconds(10));
        session.BeginTranscription(answer.Id); session.SetTranscript(answer.Id, "Observed answer", "test", "test-stt");
        session.BeginEvaluation(answer.Id); return answer;
    }

    private static string Decision(InterviewSession session, Guid? next, decimal delta = 0.5m, bool shouldComplete = false) =>
        JsonSerializer.Serialize(new InterviewDecision
        {
            AnswerAssessment = new() { Relevance = 0.8m, Clarity = 0.8m, EvidenceQuality = 0.8m },
            SkillUpdates = [new() { SkillId = Sql, Delta = delta, Confidence = 0.8m, Evidence = "Explained indexes" }],
            CoveredTopics = [new() { Topic = "indexes", Coverage = 0.9m }],
            NextQuestionId = next, ShouldComplete = shouldComplete, Reason = "Another skill needs evidence."
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Fact]
    public void Start_sets_question_and_rejects_second_start()
    {
        var s = Session(); s.Start(Q(1).QuestionId, Now);
        Assert.Equal(InterviewStatus.WaitingForAnswer, s.Status); Assert.Equal(Q(1).QuestionId, s.CurrentQuestionId);
        Assert.Throws<InterviewRuleException>(() => s.Start(Q(2).QuestionId, Now));
    }

    [Fact]
    public void Submission_requires_started_session_and_current_question_and_rejects_duplicate()
    {
        var s = Session();
        Assert.Throws<InterviewRuleException>(() => s.SubmitAnswer(Q(1).QuestionId, "key", "audio/webm", 100, Now));
        s.Start(Q(1).QuestionId, Now);
        Assert.Throws<InterviewRuleException>(() => s.SubmitAnswer(Q(2).QuestionId, "key", "audio/webm", 100, Now));
        s.SubmitAnswer(Q(1).QuestionId, "key", "audio/webm", 100, Now);
        Assert.Throws<InterviewRuleException>(() => s.SubmitAnswer(Q(1).QuestionId, "key", "audio/webm", 100, Now));
        Assert.Single(s.Answers);
    }

    [Fact]
    public void Retry_reuses_original_audio_and_transcript_without_creating_answer()
    {
        var s = Session(); var answer = Answer(s); s.FailAnswer(answer.Id); s.RetryAnswer(answer.Id);
        Assert.Equal(AnswerStatus.Transcribed, answer.Status); Assert.Equal("private/audio", answer.AudioStorageKey);
        Assert.Equal("Observed answer", answer.Transcript); Assert.Single(s.Answers);
        s.BeginEvaluation(answer.Id); s.FinishAnswer(answer.Id, [], [], "fallback", "v1", Now.AddSeconds(20));
        Assert.Throws<InterviewRuleException>(() => s.RetryAnswer(answer.Id));
    }

    [Fact]
    public void Transcription_failure_retries_same_answer()
    {
        var s = Session(); s.Start(Q(1).QuestionId, Now);
        var answer = s.SubmitAnswer(Q(1).QuestionId, "key", "audio/webm", 100, Now);
        s.BeginTranscription(answer.Id); s.FailAnswer(answer.Id); s.RetryAnswer(answer.Id); s.BeginTranscription(answer.Id);
        Assert.Single(s.Answers); Assert.Equal(AnswerStatus.Transcribing, answer.Status);
    }

    [Fact]
    public void Evidence_is_normalized_and_invalid_assessment_cannot_partially_mutate_state()
    {
        var s = Session(); var answer = Answer(s);
        Assert.Throws<InterviewRuleException>(() => s.FinishAnswer(answer.Id, [new(Sql, 0.6m, 0.8m, "Evidence")],
            [new(Sql, "indexes", 2m)], "model", "v1", Now.AddSeconds(20)));
        Assert.Empty(s.Evidence); Assert.All(s.SkillStates, x => Assert.Equal(0, x.EvidenceCount));
        s.FinishAnswer(answer.Id, [new(Sql, 0.6m, 0.8m, "Evidence")], [new(Sql, "indexes", 0.9m)], "model", "v1", Now.AddSeconds(20));
        var e = Assert.Single(s.Evidence);
        Assert.Equal(answer.Id, e.InterviewAnswerId); Assert.Equal(0.6m, e.ScoreAfter); Assert.Equal(0.6m, e.Delta);
        Assert.Equal(1, s.SkillStates.Single(x => x.SkillId == Sql).EvidenceCount);
    }

    [Theory]
    [InlineData(-1.1, 0.8)]
    [InlineData(1.1, 0.8)]
    [InlineData(0.1, -0.1)]
    [InlineData(0.1, 1.1)]
    public void Invalid_score_ranges_rejected(double delta, double confidence)
    {
        var s = Session(); var answer = Answer(s);
        Assert.Throws<InterviewRuleException>(() => s.FinishAnswer(answer.Id,
            [new(Sql, (decimal)delta, (decimal)confidence, "Evidence")], [], "model", "v1", Now.AddSeconds(20)));
        Assert.Empty(s.Evidence);
    }

    [Fact]
    public void Policy_excludes_answered_inactive_and_covered_questions_and_prioritizes_evidence_gaps()
    {
        var s = Session(12, Q(1), Q(2), Q(3, topic: "transactions"), Q(4, active: false), Q(5, Architecture, "design"));
        var answer = Answer(s);
        s.FinishAnswer(answer.Id, [new(Sql, 0.5m, 0.8m, "Evidence")], [new(Sql, "indexes", 0.9m)], "model", "v1", Now.AddSeconds(20));
        var allowed = new InterviewPolicy().GetAllowedQuestions(s, Now.AddSeconds(20));
        Assert.Equal(new[] { Q(5).QuestionId, Q(3).QuestionId }, allowed.Select(x => x.QuestionId));
    }

    [Fact]
    public void Unrelated_skill_cannot_enter_session_plan_or_evidence()
    {
        Assert.Throws<InterviewRuleException>(() => Session(12, Q(1, Guid.NewGuid())));
        var s = Session(); var answer = Answer(s);
        Assert.Throws<InterviewRuleException>(() => s.FinishAnswer(answer.Id, [new(Architecture, 0.1m, 0.8m, "Evidence")], [], "model", "v1", Now.AddSeconds(20)));
    }

    [Fact]
    public void Question_limit_completes_and_blocks_further_operations()
    {
        var s = Session(1); var answer = Answer(s); var now = Now.AddSeconds(20);
        s.FinishAnswer(answer.Id, [], [], "fallback", "v1", now);
        Assert.Equal(InterviewCompletionReason.QuestionLimit, new InterviewPolicy().GetCompletionReason(s, now));
        Assert.Empty(new InterviewPolicy().GetAllowedQuestions(s, now));
        Assert.Throws<InterviewRuleException>(() => s.AskNext(Q(2).QuestionId, now));
        s.Complete(InterviewCompletionReason.QuestionLimit, now);
        Assert.Throws<InterviewRuleException>(() => s.Complete(InterviewCompletionReason.Manual, now));
        Assert.Throws<InterviewRuleException>(() => s.SubmitAnswer(Q(2).QuestionId, "key", "audio/webm", 100, now));
    }

    [Fact]
    public void Duration_limit_completes_and_expired_submission_is_rejected()
    {
        var s = Session(); s.Start(Q(1).QuestionId, Now); var now = Now.AddSeconds(1800);
        Assert.Equal(InterviewCompletionReason.DurationLimit, new InterviewPolicy().GetCompletionReason(s, now));
        Assert.Throws<InterviewRuleException>(() => s.SubmitAnswer(Q(1).QuestionId, "key", "audio/webm", 100, now));
        s.Complete(InterviewCompletionReason.DurationLimit, now); Assert.Equal(InterviewStatus.Completed, s.Status);
    }

    [Fact]
    public void No_valid_questions_completes_without_provider()
    {
        var s = Session(12, Q(1)); var answer = Answer(s); var now = Now.AddSeconds(20);
        s.FinishAnswer(answer.Id, [], [], "fallback", "v1", now);
        Assert.Equal(InterviewCompletionReason.NoQuestions, new InterviewPolicy().GetCompletionReason(s, now));
        Assert.Null(new InterviewPolicy().SelectFallback(s, now));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"answerAssessment\":null}")]
    public void Malformed_decision_uses_deterministic_fallback(string json)
    {
        var s = Session(); Answer(s); var policy = new InterviewPolicy();
        var ids = policy.GetAllowedQuestions(s, Now.AddSeconds(20)).Select(x => x.QuestionId).ToArray();
        var result = new InterviewDecisionValidator(policy).ValidateOrFallback(json, s, ids, Now.AddSeconds(20));
        Assert.True(result.UsedFallback); Assert.Equal(policy.SelectFallback(s, Now.AddSeconds(20))!.QuestionId, result.NextQuestionId);
        Assert.Empty(result.SkillUpdates);
    }

    [Fact]
    public void Unknown_repeated_and_outside_shortlist_ids_are_rejected()
    {
        var s = Session(); Answer(s); var policy = new InterviewPolicy(); var validator = new InterviewDecisionValidator(policy);
        foreach (var next in new[] { Guid.NewGuid(), Q(1).QuestionId, Q(3).QuestionId })
        {
            var result = validator.ValidateOrFallback(Decision(s, next), s, [Q(2).QuestionId], Now.AddSeconds(20));
            Assert.True(result.UsedFallback); Assert.Equal(Q(2).QuestionId, result.NextQuestionId); Assert.Empty(result.SkillUpdates);
        }
    }

    [Theory]
    [InlineData("skill")] [InlineData("topic")] [InlineData("confidence")]
    [InlineData("relevance")] [InlineData("clarity")] [InlineData("evidenceQuality")]
    [InlineData("nullSkill")] [InlineData("nullTopic")]
    public void Invalid_assessment_falls_back_without_any_partial_evidence(string invalid)
    {
        var s = Session(); Answer(s); var json = JsonNode.Parse(Decision(s, Q(2).QuestionId))!;
        switch (invalid)
        {
            case "skill": json["skillUpdates"]![0]!["skillId"] = Guid.NewGuid().ToString(); break;
            case "topic": json["coveredTopics"]![0]!["topic"] = "invented-topic"; break;
            case "confidence": json["skillUpdates"]![0]!["confidence"] = 1.1m; break;
            case "nullSkill": json["skillUpdates"]![0] = null; break;
            case "nullTopic": json["coveredTopics"]![0] = null; break;
            default: json["answerAssessment"]![invalid] = -0.1m; break;
        }
        var result = new InterviewDecisionValidator(new InterviewPolicy()).ValidateOrFallback(json.ToJsonString(), s,
            [Q(2).QuestionId], Now.AddSeconds(20));
        Assert.True(result.UsedFallback); Assert.Empty(result.SkillUpdates); Assert.Empty(result.CoveredTopics);
        Assert.Empty(s.Evidence); Assert.Equal(Q(2).QuestionId, result.NextQuestionId);
    }

    [Fact]
    public void Valid_decision_is_accepted_but_ai_cannot_force_completion()
    {
        var s = Session(); Answer(s); var result = new InterviewDecisionValidator(new InterviewPolicy())
            .ValidateOrFallback(Decision(s, Q(2).QuestionId, shouldComplete: true), s, [Q(2).QuestionId], Now.AddSeconds(20));
        Assert.False(result.UsedFallback); Assert.Null(result.CompletionReason); Assert.Single(result.SkillUpdates);
    }

    [Fact]
    public void Invalid_decision_delta_and_unknown_fields_use_fallback()
    {
        var s = Session(); Answer(s); var validator = new InterviewDecisionValidator(new InterviewPolicy());
        var json = Decision(s, Q(2).QuestionId);
        Assert.True(validator.ValidateOrFallback(Decision(s, Q(2).QuestionId, 1.1m), s, [Q(2).QuestionId], Now.AddSeconds(20)).UsedFallback);
        Assert.True(validator.ValidateOrFallback(json.Insert(json.Length - 1, ",\"questionText\":\"Invented\""), s, [Q(2).QuestionId], Now.AddSeconds(20)).UsedFallback);
    }

    [Fact]
    public void Required_skills_need_actual_evidence_before_sufficient_evidence_completion()
    {
        var s = Session(); var answer = Answer(s); var policy = new InterviewPolicy();
        s.FinishAnswer(answer.Id, [new(Sql, 0.5m, 0.9m, "Evidence")], [], "model", "v1", Now.AddSeconds(20));
        Assert.NotEqual(InterviewCompletionReason.SufficientEvidence, policy.GetCompletionReason(s, Now.AddSeconds(20), 1));
        s.AskNext(Q(3).QuestionId, Now.AddSeconds(20));
        var second = s.SubmitAnswer(Q(3).QuestionId, "second", "audio/webm", 100, Now.AddSeconds(30));
        s.BeginTranscription(second.Id); s.SetTranscript(second.Id, "Design evidence", "test", "test-stt"); s.BeginEvaluation(second.Id);
        s.FinishAnswer(second.Id, [new(Architecture, 0.5m, 0.9m, "Evidence")], [], "model", "v1", Now.AddSeconds(40));
        Assert.Equal(InterviewCompletionReason.SufficientEvidence, policy.GetCompletionReason(s, Now.AddSeconds(40), 1));
    }

    [Fact]
    public void Historical_answer_and_evidence_survive_advancement_and_scores_are_clamped()
    {
        var s = Session(); var first = Answer(s);
        s.FinishAnswer(first.Id, [new(Sql, 0.8m, 0.9m, "First evidence")], [], "model", "v1", Now.AddSeconds(20));
        s.AskNext(Q(2).QuestionId, Now.AddSeconds(20));
        var second = s.SubmitAnswer(Q(2).QuestionId, "second", "audio/webm", 100, Now.AddSeconds(30));
        s.BeginTranscription(second.Id); s.SetTranscript(second.Id, "Second answer", "test", "test-stt"); s.BeginEvaluation(second.Id);
        s.FinishAnswer(second.Id, [new(Sql, 0.8m, 0.9m, "Second evidence")], [], "model", "v1", Now.AddSeconds(40));
        Assert.Equal("Observed answer", first.Transcript); Assert.Equal(2, s.Answers.Count);
        Assert.Equal(1m, s.SkillStates.Single(x => x.SkillId == Sql).CurrentScore);
        Assert.Equal(0.2m, s.Evidence[1].Delta); Assert.Equal(2, s.Evidence.Count);
    }

    [Fact]
    public void Plan_is_copied_and_backward_timestamps_are_rejected()
    {
        var questions = new[] { Q(1), Q(2) }; var s = Session(12, questions); questions[0] = Q(9);
        Assert.Equal(Q(1).QuestionId, s.Questions[0].QuestionId);
        var answer = Answer(s);
        Assert.Throws<InterviewRuleException>(() => s.FinishAnswer(answer.Id, [], [], "model", "v1", Now));
        Assert.Equal(AnswerStatus.Evaluating, answer.Status);
    }

    [Fact]
    public void Validator_accepts_exact_attempt_shortlist_beyond_default_ten()
    {
        var questions = Enumerable.Range(1, 20).Select(x => Q(x, topic: "topic-" + x)).ToArray();
        questions[0] = Q(1); var s = Session(30, questions); Answer(s);
        var exact = new InterviewPolicy().GetAllowedQuestions(s, Now.AddSeconds(20), 20);
        var next = exact.Last().QuestionId;
        var result = new InterviewDecisionValidator(new InterviewPolicy()).ValidateOrFallback(Decision(s, next), s,
            exact.Select(x => x.QuestionId).ToArray(), Now.AddSeconds(20));
        Assert.False(result.UsedFallback); Assert.Equal(next, result.NextQuestionId);
    }
}
