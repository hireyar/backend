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
    public bool AutomaticReportCompletionEnabled { get; private set; }
    public Guid? CurrentQuestionId { get; private set; }
    public string? ProtectedAccessCode { get; private set; }
    public Guid? SupersededByInterviewId { get; private set; }
    public int AccessFailedAttempts { get; private set; }
    public DateTimeOffset? AccessFailureWindowAtUtc { get; private set; }
    public DateTimeOffset? AccessLockedUntilUtc { get; private set; }
    public void SetProtectedAccessCode(string protectedCode, string fingerprint)
    {
        InterviewRules.Require(Status == InterviewStatus.Created, "interview_already_started");
        ProtectedAccessCode = InterviewRules.Text(protectedCode, 2048); SetAccessTokenHash(fingerprint);
    }
    public void RecordAccessFailure(DateTimeOffset now)
    {
        CheckTime(now);
        if (AccessFailureWindowAtUtc is null || now >= AccessFailureWindowAtUtc.Value.AddMinutes(15))
        { AccessFailedAttempts = 0; AccessFailureWindowAtUtc = now; AccessLockedUntilUtc = null; }
        AccessFailedAttempts = Math.Min(5, AccessFailedAttempts + 1);
        if (AccessFailedAttempts == 5) AccessLockedUntilUtc = now.AddMinutes(15);
    }
    public void Supersede(Guid replacementId, DateTimeOffset now)
    {
        CheckTime(now); InterviewRules.Require(replacementId != Id && SupersededByInterviewId is null, "invitation_already_replaced");
        SupersededByInterviewId = replacementId; RevokeAccess(now);
        ProcessingLeaseId = null; ProcessingLeaseExpiresAtUtc = null; ProcessingRevision = Guid.NewGuid();
    }
    public bool AskOneQuestionPerTopic { get; private set; }
    public bool ProgressiveTopicQuestions { get; private set; }
    public bool MediumFirstTopicQuestions { get; private set; }
    public void EnableProgressiveTopicQuestions(bool mediumFirst = false)
    {
        InterviewRules.Require(Status == InterviewStatus.Created && !AskOneQuestionPerTopic &&
            skills.Count is >= 5 and <= 10 && MaxQuestionCount == skills.Count * (mediumFirst ? 2 : 3) &&
            questions.GroupBy(q => q.Topic).Count() == skills.Count && questions.GroupBy(q => q.Topic).All(g =>
                g.Count() == 3 && g.All(q => q.IsActive) &&
                g.Select(q => q.Difficulty).Order().SequenceEqual(new[] { 1, 3, 5 }) &&
                g.Select(q => q.SkillId).Distinct().Count() == 1) &&
            questions.Select(q => q.SkillId).Distinct().Count() == skills.Count, "invalid_topic_plan");
        ProgressiveTopicQuestions = true;
        MediumFirstTopicQuestions = mediumFirst;
    }

    // While evaluation is pending, offer both outcomes to the model. After grading,
    // only the next level of a passed topic or easy questions of unvisited topics remain.
    public IReadOnlyList<InterviewQuestionSnapshot> GetProgressiveQuestions()
    {
        var asked = questions.Where(q => q.AskedAtUtc is not null || q.QuestionId == CurrentQuestionId ||
            answers.Any(a => a.QuestionId == q.QuestionId)).ToArray();
        var unvisited = questions.Where(q => q.IsActive && q.Difficulty == (MediumFirstTopicQuestions ? 3 : 1) && asked.All(a => a.Topic != q.Topic)).ToArray();
        var current = questions.SingleOrDefault(q => q.QuestionId == CurrentQuestionId);
        if (current is null) return unvisited;
        if (MediumFirstTopicQuestions)
        {
            if (current.Difficulty != 3) return unvisited;
            var assessed = answers.SingleOrDefault(a => a.QuestionId == current.QuestionId && a.Status == AnswerStatus.Processed && !a.IsInteraction);
            var branches = questions.Where(q => q.IsActive && q.Topic == current.Topic && q.Difficulty != 3).ToArray();
            if (assessed is null) return branches;
            var difficulty = assessed.Correctness >= 0.7m && assessed.AssessmentConfidence >= 0.6m ? 5 : 1;
            return branches.Where(q => q.Difficulty == difficulty).ToArray();
        }
        var next = questions.SingleOrDefault(q => q.IsActive && q.Topic == current.Topic && q.Difficulty == current.Difficulty + 2);
        var final = answers.SingleOrDefault(a => a.QuestionId == current.QuestionId && a.Status == AnswerStatus.Processed && !a.IsInteraction);
        if (next is null) return unvisited;
        if (final is null) return new[] { next }.Concat(unvisited).ToArray();
        return final.Correctness >= 0.7m && final.AssessmentConfidence >= 0.6m ? [next] : unvisited;
    }
    public void EnableTopicQuestions()
    {
        InterviewRules.Require(Status == InterviewStatus.Created && !ProgressiveTopicQuestions && MaxQuestionCount is >= 5 and <= 10 && skills.Count == MaxQuestionCount &&
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
    public string? CandidateIntroductionMessage { get; private set; }
    public bool CandidateIntroductionEnabled { get; private set; }
    public string? CompanyIntroductionMessage { get; private set; }
    public bool CompanyIntroductionEnabled { get; private set; }
    public string Language { get; private set; } = "fa-IR";
    public string? MasterPromptSnapshot { get; private set; }
    public string? MasterPromptVersion { get; private set; }
    public string? ResumeTextSnapshot { get; private set; }
    public string? CandidateIntroductionTranscript { get; private set; }
    public InterviewStage Stage { get; private set; } = InterviewStage.Questions;
    public DateTimeOffset? StageChangedAtUtc { get; private set; }
    public int RecoveryAttempts { get; private set; }
    public int MaxInteractionsPerQuestion { get; private set; } = 2;
    public string? CurrentInteractionMessage { get; private set; }
    public int RemainingInteractions => Math.Max(0, MaxInteractionsPerQuestion - answers.Count(a => a.QuestionId == CurrentQuestionId && a.IsInteraction));
    public void SetInteractionLimit(int limit)
    { InterviewRules.Require(Status == InterviewStatus.Created && limit is >= 0 and <= 3, "invalid_interaction_limit"); MaxInteractionsPerQuestion = limit; }
    public DateTimeOffset? NextRecoveryAtUtc { get; private set; }
    public void SetAiSnapshot(string prompt, string version, string? resumeText, string language,
        bool candidateEnabled, string candidateMessage, bool companyEnabled, string companyMessage)
    {
        InterviewRules.Require(Status == InterviewStatus.Created, "interview_already_started");
        MasterPromptSnapshot = InterviewRules.Text(prompt, 16000); MasterPromptVersion = InterviewRules.Text(version, 100);
        ResumeTextSnapshot = string.IsNullOrWhiteSpace(resumeText) ? null : InterviewRules.Text(resumeText, 32000);
        Language = InterviewRules.Text(language, 20);
        CandidateIntroductionEnabled = candidateEnabled; CompanyIntroductionEnabled = companyEnabled;
        CandidateIntroductionMessage = candidateEnabled ? InterviewRules.Text(candidateMessage, 2000) : candidateMessage;
        CompanyIntroductionMessage = companyEnabled ? InterviewRules.Text(companyMessage, 2000) : companyMessage;
    }
    public void AdvanceStage(InterviewStage expected, string? introduction, DateTimeOffset now)
    {
        CheckTime(now);
        // A repeated acknowledgement is safe; a stale client cannot skip a stage.
        if (Stage != expected) return;
        var safeIntroduction = expected == InterviewStage.CandidateIntroduction ? InterviewRules.Text(introduction ?? "", 32000) : null;
        Stage = Stage switch {
            InterviewStage.Welcome => CandidateIntroductionEnabled ? InterviewStage.CandidateIntroduction : InterviewStage.Questions,
            InterviewStage.CandidateIntroduction => InterviewStage.Questions,
            InterviewStage.CompanyIntroduction => InterviewStage.Goodbye,
            InterviewStage.Goodbye => InterviewStage.Finished,
            _ => throw new InterviewRuleException("invalid_interview_stage")
        };
        if (expected == InterviewStage.CandidateIntroduction)
            CandidateIntroductionTranscript = safeIntroduction;
        StageChangedAtUtc = now;
        if (Stage == InterviewStage.Questions && CurrentQuestionId is { } id) questions.Single(q => q.QuestionId == id).MarkAsked(now);
    }
    public void ScheduleRecovery(DateTimeOffset now, int delaySeconds)
    { CheckTime(now); NextRecoveryAtUtc = now.AddSeconds(delaySeconds); }
    public void BeginRecoveryAttempt(DateTimeOffset now)
    { CheckTime(now); RecoveryAttempts++; NextRecoveryAtUtc = now.AddSeconds(30 * (1 << Math.Min(RecoveryAttempts, 5))); }
    public void SetPanelSnapshot(string title, string description, string templateTitle, string welcome, string goodbye)
    {
        InterviewRules.Require(Status == InterviewStatus.Created, "interview_already_started");
        PositionTitleSnapshot = InterviewRules.Text(title, 200);
        PositionDescriptionSnapshot = InterviewRules.Text(description, 10000);
        TemplateTitleSnapshot = InterviewRules.Text(templateTitle, 200);
        WelcomeMessage = InterviewRules.Text(welcome, 2000);
        GoodbyeMessage = InterviewRules.Text(goodbye, 2000);
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
    public int AnsweredQuestionCount => answers.Count(x => x.Status == AnswerStatus.Processed && !x.IsInteraction);
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
        Stage = CandidateIntroductionEnabled || CompanyIntroductionEnabled ? InterviewStage.Welcome : InterviewStage.Questions;
        StageChangedAtUtc = now;
        if (Stage == InterviewStage.Questions) questions.Single(q => q.QuestionId == questionId).MarkAsked(now);
        Status = InterviewStatus.WaitingForAnswer;
    }

    public InterviewAnswer SubmitAnswer(Guid questionId, string storageKey, string contentType, long size, DateTimeOffset now)
    {
        CheckTime(now);
        InterviewRules.Require(Status == InterviewStatus.WaitingForAnswer, "interview_not_waiting");
        InterviewRules.Require(Stage == InterviewStage.Questions, "invalid_interview_stage");
        InterviewRules.Require(RemainingSeconds(now) > 0, "interview_expired");
        InterviewRules.Require(CurrentQuestionId == questionId, "wrong_question");
        InterviewRules.Require(answers.All(x => x.QuestionId != questionId || x.IsInteraction), "duplicate_answer");
        var answer = new InterviewAnswer(Id, questionId, storageKey, contentType, size, now);
        // Bounded server response window. Time beyond the first 120 seconds is idle and excluded.
        // This is server-observed engagement, not decoded audio length or browser page-open duration.
        var elapsed = QuestionReadyAtUtc is { } ready ? (int)Math.Max(0, Math.Floor((now - ready).TotalSeconds)) : 0;
        answer.SetBillableSeconds(Math.Min(120, Math.Min(elapsed, RemainingSeconds(QuestionReadyAtUtc ?? now))));
        answers.Add(answer); Status = InterviewStatus.ProcessingAnswer; RecoveryAttempts = 0; NextRecoveryAtUtc = now; return answer;
    }

    public void BeginTranscription(Guid answerId) => Processing(answerId).BeginTranscription();
    public void SetTranscript(Guid answerId, string transcript, string provider, string model) =>
        Processing(answerId).SetTranscript(transcript, provider, model);
    public void BeginEvaluation(Guid answerId) => Processing(answerId).BeginEvaluation();
    public void FailAnswer(Guid answerId) => Processing(answerId).Fail();
    public void RetryAnswer(Guid answerId) => Processing(answerId).Retry();
    public void ContinueQuestion(Guid answerId, string message, string model, string version, DateTimeOffset now)
    {
        CheckTime(now); var answer = Processing(answerId);
        InterviewRules.Require(answer.Status == AnswerStatus.Evaluating && RemainingInteractions > 0 && RemainingSeconds(now) > 0, "interaction_limit");
        answer.FinishInteraction(message, model, version, now); CurrentInteractionMessage = message;
        Status = InterviewStatus.WaitingForAnswer; QuestionReadyAtUtc = null;
    }

    public void FinishAnswer(Guid answerId, IReadOnlyList<SkillAssessment> updates,
        IReadOnlyList<TopicAssessment> topics, string model, string promptVersion, DateTimeOffset now,
        decimal? correctness = null, decimal? assessmentConfidence = null)
    {
        CheckTime(now); var answer = Processing(answerId);
        InterviewRules.Require(answer.Status == AnswerStatus.Evaluating, "invalid_answer_state");
        InterviewRules.Require((correctness is null) == (assessmentConfidence is null), "invalid_answer_assessment");
        if (correctness is { } score) InterviewRules.Unit(score);
        if (assessmentConfidence is { } confidence) InterviewRules.Unit(confidence);
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
        answer.SetAssessment(correctness, assessmentConfidence);
        answer.Finish(safeModel, safeVersion, now);
    }

    public void AskNext(Guid questionId, DateTimeOffset now)
    {
        CheckTime(now); RequireProcessed();
        InterviewRules.Require(AnsweredQuestionCount < MaxQuestionCount && RemainingSeconds(now) > 0, "interview_limit");
        CheckQuestion(questionId); CurrentQuestionId = questionId; CurrentInteractionMessage = null; QuestionReadyAtUtc = null; Status = InterviewStatus.WaitingForAnswer;
        questions.Single(q => q.QuestionId == questionId).MarkAsked(now);
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
        AutomaticReportCompletionEnabled = true;
        Stage = CompanyIntroductionEnabled ? InterviewStage.CompanyIntroduction : InterviewStage.Goodbye;
        StageChangedAtUtc = now; NextRecoveryAtUtc = null;
    }

    public void Cancel(DateTimeOffset now)
    {
        CheckTime(now);
        InterviewRules.Require(Status is not (InterviewStatus.Completed or InterviewStatus.Cancelled), "invalid_interview_state");
        CurrentQuestionId = null; CompletedAtUtc = now; Status = InterviewStatus.Cancelled;
        Stage = InterviewStage.Finished; NextRecoveryAtUtc = null;
    }

    private void RequireProcessed() => InterviewRules.Require(Status == InterviewStatus.ProcessingAnswer &&
        answers.Any(x => x.QuestionId == CurrentQuestionId && x.Status == AnswerStatus.Processed && !x.IsInteraction), "answer_not_processed");

    private InterviewAnswer Processing(Guid answerId)
    {
        InterviewRules.Require(Status == InterviewStatus.ProcessingAnswer, "invalid_interview_state");
        return answers.SingleOrDefault(x => x.Id == answerId && x.QuestionId == CurrentQuestionId)
            ?? throw new InterviewRuleException("answer_not_found");
    }

    private void CheckQuestion(Guid questionId) => InterviewRules.Require(Questions.Any(x => x.QuestionId == questionId && x.IsActive) &&
        answers.All(x => x.QuestionId != questionId) && (!ProgressiveTopicQuestions ||
            GetProgressiveQuestions().Any(q => q.QuestionId == questionId)) && (!AskOneQuestionPerTopic ||
            answers.All(a => questions.Single(q => q.QuestionId == a.QuestionId).Topic != questions.Single(q => q.QuestionId == questionId).Topic)), "question_not_allowed");

    private void CheckTime(DateTimeOffset now)
    {
        InterviewRules.Utc(now);
        InterviewRules.Require(now >= CreatedAtUtc && (StartedAtUtc is null || now >= StartedAtUtc), "invalid_timestamp");
        InterviewRules.Require(answers.All(x => now >= x.SubmittedAtUtc &&
            (x.ProcessedAtUtc is null || now >= x.ProcessedAtUtc)), "invalid_timestamp");
    }
}

public enum InterviewStage { Welcome, CandidateIntroduction, Questions, CompanyIntroduction, Goodbye, Finished }
