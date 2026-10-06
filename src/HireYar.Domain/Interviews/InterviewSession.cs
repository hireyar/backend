namespace HireYar.Domain.Interviews;

public sealed class InterviewSession
{
    private readonly List<InterviewAnswer> answers = [];
    private readonly List<SkillEvidence> evidence = [];
    private readonly List<CandidateSkillState> skillStates = [];
    private readonly List<InterviewQuestionSnapshot> questions = [];
    private readonly List<InterviewSkill> skills = [];
    private InterviewSession() { }
    private readonly List<InterviewCoveredTopic> coveredTopics = [];
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; private set; }
    public Guid JobPositionId { get; private set; }
    public Guid CandidateApplicationId { get; private set; }
    public Guid InterviewTemplateId { get; private set; }
    public InterviewStatus Status { get; private set; } = InterviewStatus.Created;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }
    public void MarkDeleted(DateTimeOffset now)
    {
        CheckTime(now); DeletedAtUtc = now; RevokeAccess(now);
        ProcessingLeaseId = null; ProcessingLeaseExpiresAtUtc = null; ProcessingRevision = Guid.NewGuid();
    }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public Guid? CurrentQuestionId { get; private set; }
    public bool AskOneQuestionPerTopic { get; private set; }
    public void EnableTopicQuestions()
    {
        InterviewRules.Require(Status == InterviewStatus.Created && MaxQuestionCount is >= 5 and <= 10 && skills.Count == MaxQuestionCount &&
            questions.GroupBy(q => q.Topic).Count() == MaxQuestionCount && questions.GroupBy(q => q.Topic).All(g =>
                g.Count() == 3 && g.Select(q => q.Difficulty).Order().SequenceEqual(new[] { 1, 3, 5 }) && g.Select(q => q.SkillId).Distinct().Count() == 1), "invalid_topic_plan");
        AskOneQuestionPerTopic = true;
    }
    public string RequiredCapabilityCode { get; private set; } = "technical_interview";
    public string? PositionTitleSnapshot { get; private set; }
    public string? PositionDescriptionSnapshot { get; private set; }
    public string? TemplateTitleSnapshot { get; private set; }
    public string? WelcomeMessage { get; private set; }
    public string? GoodbyeMessage { get; private set; }
    public void SetPanelSnapshot(string title, string description, string templateTitle, string welcome, string goodbye)
    {
        InterviewRules.Require(Status == InterviewStatus.Created, "interview_already_started");
        PositionTitleSnapshot = InterviewRules.Text(title, 200);
        PositionDescriptionSnapshot = InterviewRules.Text(description, 10000);
        TemplateTitleSnapshot = InterviewRules.Text(templateTitle, 200);
        WelcomeMessage = InterviewRules.Text(welcome, 2000);
        GoodbyeMessage = InterviewRules.Text(goodbye, 2000);
    }
    public void RenewInvitation(string hash, DateTimeOffset now, DateTimeOffset expires)
    {
        CheckTime(now); InterviewRules.Utc(expires);
        InterviewRules.Require(Status == InterviewStatus.Created && AccessRevokedAtUtc is null && expires > now, "invitation_not_resendable");
        SetAccessTokenHash(hash); CreatedAtUtc = now; ExpiresAtUtc = expires;
    }
    public DateTimeOffset? QuestionReadyAtUtc { get; private set; }
    public void SetRequiredCapability(string code)
    {
        InterviewRules.Require(Status == InterviewStatus.Created && code is "hr_interview" or "technical_interview", "invalid_interview_capability");
        RequiredCapabilityCode = code;
    }
    public void MarkQuestionReady(DateTimeOffset now)
    {
        CheckTime(now);
        if (Status == InterviewStatus.WaitingForAnswer && QuestionReadyAtUtc is null) QuestionReadyAtUtc = now;
    }
    public InterviewCompletionReason? CompletionReason { get; private set; }
    public int MaxQuestionCount { get; private set; }
    public int MaxDurationSeconds { get; private set; }
    public int AnsweredQuestionCount => answers.Count(x => x.Status == AnswerStatus.Processed);
    public IReadOnlyList<InterviewQuestionSnapshot> Questions => questions.AsReadOnly();
    public IReadOnlyList<InterviewSkill> Skills => skills.AsReadOnly();
    public byte[] RowVersion { get; private set; } = [];
    public Guid? ProcessingLeaseId { get; private set; }
    public DateTimeOffset? ProcessingLeaseExpiresAtUtc { get; private set; }
    public DateTimeOffset? LastProcessingStartedAtUtc { get; private set; }
    public Guid ProcessingRevision { get; private set; }
    public string AccessTokenHash { get; private set; } = "";
    public DateTimeOffset? AccessRevokedAtUtc { get; private set; }

    public void SetAccessTokenHash(string hash) => AccessTokenHash = InterviewRules.Text(hash, 64);
    public void RevokeAccess(DateTimeOffset now) { CheckTime(now); AccessRevokedAtUtc = now; }
    public Guid AcquireLease(Guid answerId, DateTimeOffset now, int leaseSeconds)
    {
        CheckTime(now); var answer = Processing(answerId);
        InterviewRules.Require(leaseSeconds is >= 60 and <= 900, "invalid_lease");
        InterviewRules.Require(ProcessingLeaseId is null || ProcessingLeaseExpiresAtUtc <= now, "answer_processing");
        InterviewRules.Require(answer.Status != AnswerStatus.Processed, "answer_already_processed");
        if (answer.Status == AnswerStatus.Failed) answer.Retry();
        else answer.Resume();
        ProcessingLeaseId = Guid.NewGuid(); ProcessingLeaseExpiresAtUtc = now.AddSeconds(leaseSeconds);
        LastProcessingStartedAtUtc = now; ProcessingRevision = Guid.NewGuid();
        return ProcessingLeaseId.Value;
    }
    public void CheckLease(Guid lease, DateTimeOffset now)
    {
        CheckTime(now);
        InterviewRules.Require(ProcessingLeaseId == lease && ProcessingLeaseExpiresAtUtc > now, "processing_lease_lost");
        ProcessingRevision = Guid.NewGuid();
    }
    public void ReleaseLease(Guid lease, DateTimeOffset now)
    {
        CheckLease(lease, now); ProcessingLeaseId = null; ProcessingLeaseExpiresAtUtc = null;
    }
    public IReadOnlyList<InterviewAnswer> Answers => answers.AsReadOnly();
    public IReadOnlyList<SkillEvidence> Evidence => evidence.AsReadOnly();
    public IReadOnlyList<CandidateSkillState> SkillStates => skillStates.AsReadOnly();
    public IReadOnlyList<InterviewCoveredTopic> CoveredTopics => coveredTopics.AsReadOnly();

    public InterviewSession(Guid organizationId, Guid jobPositionId, Guid candidateApplicationId,
        Guid templateId, IEnumerable<InterviewQuestionSnapshot> questions, IEnumerable<InterviewSkill> skills,
        int maxQuestionCount, int maxDurationSeconds, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        InterviewRules.Utc(now); InterviewRules.Utc(expiresAt);
        InterviewRules.Require(organizationId != Guid.Empty && jobPositionId != Guid.Empty &&
            candidateApplicationId != Guid.Empty && templateId != Guid.Empty, "invalid_id");
        InterviewRules.Require(maxQuestionCount is >= 1 and <= 30 && maxDurationSeconds is >= 300 and <= 3600 && expiresAt > now, "invalid_limits");
        var questionList = questions.ToList(); var skillList = skills.ToList();
        InterviewRules.Require(questionList.Count is >= 1 and <= 30 && skillList.Count is >= 1 and <= 30,
            "invalid_plan");
        InterviewRules.Require(questionList.Select(x => x.QuestionId).Distinct().Count() == questionList.Count &&
            skillList.Select(x => x.SkillId).Distinct().Count() == skillList.Count, "duplicate_plan_id");
        InterviewRules.Require(questionList.All(q => skillList.Any(s => s.SkillId == q.SkillId)), "unrelated_skill");
        OrganizationId = organizationId; JobPositionId = jobPositionId; CandidateApplicationId = candidateApplicationId;
        InterviewTemplateId = templateId; MaxQuestionCount = maxQuestionCount; MaxDurationSeconds = maxDurationSeconds;
        CreatedAtUtc = now; ExpiresAtUtc = expiresAt;
        this.questions.AddRange(questionList.Select(q => q with { InterviewSessionId = Id })); this.skills.AddRange(skillList.Select(s => s with { InterviewSessionId = Id }));
        skillStates = skillList.Select(x => new CandidateSkillState(x.SkillId) { InterviewSessionId = Id }).ToList();
    }

    public int RemainingSeconds(DateTimeOffset now)
    {
        CheckTime(now);
        var end = StartedAtUtc is { } started ? started.AddSeconds(MaxDurationSeconds) : ExpiresAtUtc;
        if (ExpiresAtUtc < end) end = ExpiresAtUtc;
        return (int)Math.Max(0, Math.Floor((end - now).TotalSeconds));
    }

    public void Start(Guid questionId, DateTimeOffset now)
    {
        CheckTime(now);
        InterviewRules.Require(Status == InterviewStatus.Created, "interview_already_started");
        InterviewRules.Require(now < ExpiresAtUtc, "interview_expired");
        CheckQuestion(questionId); StartedAtUtc = now; CurrentQuestionId = questionId;
        Status = InterviewStatus.WaitingForAnswer;
    }

    public InterviewAnswer SubmitAnswer(Guid questionId, string storageKey, string contentType, long size, DateTimeOffset now)
    {
        CheckTime(now);
        InterviewRules.Require(Status == InterviewStatus.WaitingForAnswer, "interview_not_waiting");
        InterviewRules.Require(RemainingSeconds(now) > 0, "interview_expired");
        InterviewRules.Require(CurrentQuestionId == questionId, "wrong_question");
        InterviewRules.Require(answers.All(x => x.QuestionId != questionId), "duplicate_answer");
        var answer = new InterviewAnswer(Id, questionId, storageKey, contentType, size, now);
        // Bounded server response window. Time beyond the first 120 seconds is idle and excluded.
        // This is server-observed engagement, not decoded audio length or browser page-open duration.
        var elapsed = QuestionReadyAtUtc is { } ready ? (int)Math.Max(0, Math.Floor((now - ready).TotalSeconds)) : 0;
        answer.SetBillableSeconds(Math.Min(120, Math.Min(elapsed, RemainingSeconds(QuestionReadyAtUtc ?? now))));
        answers.Add(answer); Status = InterviewStatus.ProcessingAnswer; return answer;
    }

    public void BeginTranscription(Guid answerId) => Processing(answerId).BeginTranscription();
    public void SetTranscript(Guid answerId, string transcript, string provider, string model) =>
        Processing(answerId).SetTranscript(transcript, provider, model);
    public void BeginEvaluation(Guid answerId) => Processing(answerId).BeginEvaluation();
    public void FailAnswer(Guid answerId) => Processing(answerId).Fail();
    public void RetryAnswer(Guid answerId) => Processing(answerId).Retry();

    public void FinishAnswer(Guid answerId, IReadOnlyList<SkillAssessment> updates,
        IReadOnlyList<TopicAssessment> topics, string model, string promptVersion, DateTimeOffset now)
    {
        CheckTime(now); var answer = Processing(answerId);
        InterviewRules.Require(answer.Status == AnswerStatus.Evaluating, "invalid_answer_state");
        var safeModel = InterviewRules.Text(model, 100); var safeVersion = InterviewRules.Text(promptVersion, 100);
        InterviewRules.Require(updates.Count <= Skills.Count && updates.Select(x => x.SkillId).Distinct().Count() == updates.Count,
            "duplicate_skill_update");
        var question = Questions.Single(x => x.QuestionId == answer.QuestionId);
        // Validate the entire assessment before changing any state. Evidence must concern the answered skill/topic.
        foreach (var update in updates)
        {
            InterviewRules.Require(update.SkillId == question.SkillId, "unrelated_skill");
            InterviewRules.Require(update.Delta is >= -1 and <= 1, "invalid_delta");
            InterviewRules.Unit(update.Confidence); InterviewRules.Text(update.Evidence, 2000);
        }
        InterviewRules.Require(topics.Count <= 1, "invalid_topics");
        foreach (var topic in topics)
        {
            InterviewRules.Require(topic.SkillId == question.SkillId && topic.Topic == question.Topic, "unrelated_topic");
            InterviewRules.Unit(topic.Coverage);
        }
        foreach (var update in updates)
            evidence.Add(skillStates.Single(x => x.SkillId == update.SkillId).Apply(Id, answer, update.Delta, update.Confidence, update.Evidence, now));
        foreach (var topic in topics)
        {
            var state = coveredTopics.SingleOrDefault(x => x.SkillId == topic.SkillId && x.Topic == topic.Topic);
            if (state is null) { state = new(topic.SkillId, topic.Topic) { InterviewSessionId = Id }; coveredTopics.Add(state); }
            state.Apply(topic.Coverage, now);
        }
        answer.Finish(safeModel, safeVersion, now);
    }

    public void AskNext(Guid questionId, DateTimeOffset now)
    {
        CheckTime(now); RequireProcessed();
        InterviewRules.Require(AnsweredQuestionCount < MaxQuestionCount && RemainingSeconds(now) > 0, "interview_limit");
        CheckQuestion(questionId); CurrentQuestionId = questionId; QuestionReadyAtUtc = null; Status = InterviewStatus.WaitingForAnswer;
    }

    public void Complete(InterviewCompletionReason reason, DateTimeOffset now)
    {
        CheckTime(now);
        InterviewRules.Require(Status is InterviewStatus.WaitingForAnswer or InterviewStatus.ProcessingAnswer, "invalid_interview_state");
        if (Status == InterviewStatus.ProcessingAnswer) RequireProcessed();
        InterviewRules.Require(Enum.IsDefined(reason), "invalid_completion_reason");
        if (reason == InterviewCompletionReason.QuestionLimit)
            InterviewRules.Require(AnsweredQuestionCount >= MaxQuestionCount, "question_limit_not_reached");
        if (reason == InterviewCompletionReason.DurationLimit)
            InterviewRules.Require(RemainingSeconds(now) == 0, "duration_limit_not_reached");
        CompletionReason = reason; CompletedAtUtc = now; CurrentQuestionId = null; Status = InterviewStatus.Completed;
    }

    public void Cancel(DateTimeOffset now)
    {
        CheckTime(now);
        InterviewRules.Require(Status is not (InterviewStatus.Completed or InterviewStatus.Cancelled), "invalid_interview_state");
        CurrentQuestionId = null; CompletedAtUtc = now; Status = InterviewStatus.Cancelled;
    }

    private void RequireProcessed() => InterviewRules.Require(Status == InterviewStatus.ProcessingAnswer &&
        answers.Any(x => x.QuestionId == CurrentQuestionId && x.Status == AnswerStatus.Processed), "answer_not_processed");

    private InterviewAnswer Processing(Guid answerId)
    {
        InterviewRules.Require(Status == InterviewStatus.ProcessingAnswer, "invalid_interview_state");
        return answers.SingleOrDefault(x => x.Id == answerId && x.QuestionId == CurrentQuestionId)
            ?? throw new InterviewRuleException("answer_not_found");
    }

    private void CheckQuestion(Guid questionId) => InterviewRules.Require(Questions.Any(x => x.QuestionId == questionId && x.IsActive) &&
        answers.All(x => x.QuestionId != questionId) && (!AskOneQuestionPerTopic ||
            answers.All(a => questions.Single(q => q.QuestionId == a.QuestionId).Topic != questions.Single(q => q.QuestionId == questionId).Topic)), "question_not_allowed");

    private void CheckTime(DateTimeOffset now)
    {
        InterviewRules.Utc(now);
        InterviewRules.Require(now >= CreatedAtUtc && (StartedAtUtc is null || now >= StartedAtUtc), "invalid_timestamp");
        InterviewRules.Require(answers.All(x => now >= x.SubmittedAtUtc &&
            (x.ProcessedAtUtc is null || now >= x.ProcessedAtUtc)), "invalid_timestamp");
    }
}
