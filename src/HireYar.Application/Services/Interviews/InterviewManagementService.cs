using HireYar.Application.Common;
using HireYar.Application.Billing;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Interviews;

public sealed class InterviewManagementService(IApplicationDbContext db, ICurrentUser user, TimeProvider time,
    ISubscriptionEntitlementService entitlements, InterviewBillingService billing, IInterviewAudioStorage storage, IInterviewInvitationSecurity invitationSecurity)
{
    private async Task CheckJob(Guid id, CancellationToken ct)
    { if (!await db.JobPositions.AnyAsync(x => x.OrganizationId == user.OrganizationId && x.DeletedAtUtc == null && x.Id == id, ct)) throw AppException.NotFound(); }
    public async Task<Guid> CreateApplicationAsync(Guid jobId, CandidateApplicationRequest r, CancellationToken ct)
    {
        await CheckJob(jobId, ct); var now = time.GetUtcNow();
        var row = new CandidateApplication
        {
            OrganizationId = user.OrganizationId,
            JobPositionId = jobId,
            FirstName = r.FirstName.Trim(),
            LastName = r.LastName.Trim(),
            Email = r.Email.Trim(),
            Phone = r.Phone?.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        if (row.FirstName.Length == 0 || row.LastName.Length == 0) throw AppException.Invalid();
        db.CandidateApplications.Add(row); await db.SaveChangesAsync(ct); return row.Id;
    }
    public async Task<Guid> CreateSkillAsync(SkillRequest r, CancellationToken ct)
    {
        var name = r.Name.Trim(); if (name.Length == 0) throw AppException.Invalid();
        var skill = new Skill { OrganizationId = user.OrganizationId, Name = name };
        db.Skills.Add(skill); await db.SaveChangesAsync(ct); return skill.Id;
    }
    public async Task SetPositionSkillAsync(Guid jobId, Guid skillId, PositionSkillRequest r, CancellationToken ct)
    {
        await CheckJob(jobId, ct);
        if (!await db.Skills.AnyAsync(x => x.Id == skillId && x.OrganizationId == user.OrganizationId && x.IsActive, ct)) throw AppException.NotFound();
        var row = await db.PositionSkills.SingleOrDefaultAsync(x => x.OrganizationId == user.OrganizationId && x.JobPositionId == jobId && x.SkillId == skillId, ct);
        if (row is null) { row = new() { OrganizationId = user.OrganizationId, JobPositionId = jobId, SkillId = skillId }; db.PositionSkills.Add(row); }
        row.IsRequired = r.IsRequired; row.Priority = r.Priority; row.TargetDifficulty = r.TargetDifficulty; await db.SaveChangesAsync(ct);
    }
    public async Task<BankQuestionDto> SaveQuestionAsync(Guid jobId, Guid? id, BankQuestionRequest r, byte[]? version, CancellationToken ct)
    {
        await CheckJob(jobId, ct);
        if (!await db.PositionSkills.AnyAsync(x => x.OrganizationId == user.OrganizationId && x.JobPositionId == jobId && x.SkillId == r.SkillId, ct)) throw AppException.NotFound();
        var now = time.GetUtcNow();
        var row = id is { } key ? await db.BankQuestions.SingleOrDefaultAsync(x => x.Id == key && x.OrganizationId == user.OrganizationId && x.JobPositionId == jobId, ct)
            ?? throw AppException.NotFound() : new BankQuestion { OrganizationId = user.OrganizationId, JobPositionId = jobId, CreatedAtUtc = now };
        if (id is null) db.BankQuestions.Add(row); else db.SetOriginalVersion(row, version!);
        row.SkillId = r.SkillId; row.Text = r.Text.Trim(); row.Topic = r.Topic.Trim(); row.Difficulty = r.Difficulty; row.IsActive = r.IsActive; row.UpdatedAtUtc = now;
        if (row.Text.Length == 0 || row.Topic.Length == 0) throw AppException.Invalid();
        await db.SaveChangesAsync(ct); return new(row.Id, row.SkillId, row.Text, row.Topic, row.Difficulty, row.IsActive, row.RowVersion);
    }
    public async Task<IReadOnlyList<BankQuestionDto>> QuestionsAsync(Guid jobId, CancellationToken ct)
    {
        await CheckJob(jobId, ct);
        return await db.BankQuestions.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId && x.JobPositionId == jobId)
            .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Take(100)
            .Select(x => new BankQuestionDto(x.Id, x.SkillId, x.Text, x.Topic, x.Difficulty, x.IsActive, x.RowVersion)).ToListAsync(ct);
    }
    public Task<InterviewInvitation> InviteAsync(Guid jobId, Guid applicationId, CreateInterviewRequest r, CancellationToken ct) =>
        db.WithOrganizationBillingLockAsync(user.OrganizationId, () => InviteLockedAsync(jobId, applicationId, r, ct), ct);
    private async Task<InterviewInvitation> InviteLockedAsync(Guid jobId, Guid applicationId, CreateInterviewRequest r, CancellationToken ct)
    {
        await CheckJob(jobId, ct);
        if (!await db.CandidateApplications.AnyAsync(x => x.Id == applicationId && x.OrganizationId == user.OrganizationId && x.JobPositionId == jobId, ct))
            throw AppException.NotFound();
        await entitlements.RequireAsync(user.OrganizationId, [r.RequiredCapabilityCode, "ai_interview"], ct);
        var template = await db.InterviewTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.TemplateId
        && x.OrganizationId == user.OrganizationId && x.DeletedAtUtc == null && x.JobPositionId == jobId && !x.IsArchived, ct)
            ?? throw AppException.NotFound();
        if (template.UseTopicQuestions) throw AppException.Invalid("use_wizard_for_topic_template");
        var skills = await (from p in db.PositionSkills
                            where p.OrganizationId == user.OrganizationId && p.JobPositionId == jobId
                            join s in db.Skills on p.SkillId equals s.Id
                            where s.OrganizationId == user.OrganizationId && s.IsActive
                            select new InterviewSkill(s.Id, s.Name, p.IsRequired, p.Priority, p.TargetDifficulty)).ToListAsync(ct);
        var skillIds = skills.Select(x => x.SkillId).ToArray();
        var questions =
            await db.BankQuestions.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId && x.JobPositionId == jobId && x.IsActive && skillIds.Contains(x.SkillId))
            .OrderBy(x => x.Id).Take(30).Select(x => new InterviewQuestionSnapshot(x.Id, x.SkillId, x.Text, x.Topic, x.Difficulty, x.IsActive)).ToListAsync(ct);
        if (skills.Count == 0 || skills.Count > 30 || questions.Count == 0 || skills.Any(s => s.IsRequired && questions.All(q => q.SkillId != s.SkillId)))
            throw AppException.Invalid("interview_plan_incomplete");
        var now = time.GetUtcNow();
        var token = invitationSecurity.CreateCode();
        var session = new InterviewSession(user.OrganizationId, jobId, applicationId, template.Id, questions, skills,
            r.MaxQuestionCount, template.DurationMinutes * 60, now, now.AddHours(r.ExpiresInHours));
        session.SetRequiredCapability(r.RequiredCapabilityCode);
        session.SetInteractionLimit(template.MaxFollowUpQuestionsPerQuestion);
        var job = await db.JobPositions.AsNoTracking().SingleAsync(x => x.Id == jobId && x.OrganizationId == user.OrganizationId, ct);
        session.SetPanelSnapshot(job.Title, job.Description, template.Title, template.WelcomeMessage, template.GoodbyeMessage);
        session.SetAiSnapshot(InterviewDecisionPrompt.Master, InterviewDecisionPrompt.Version, null, template.Language,
            template.CandidateIntroductionEnabled, template.CandidateIntroductionMessage, template.CompanyIntroductionEnabled, template.CompanyIntroductionMessage);
        var encrypted = invitationSecurity.Protect(session.Id, token);
        session.SetProtectedAccessCode(encrypted, InterviewTokens.Hash(encrypted)); db.InterviewSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return new(session.Id, token, session.ExpiresAtUtc, $"/api/candidate/interviews/{session.Id}", token, invitationSecurity.Link(session.Id));
    }
    public async Task<HrInterviewDto> GetAsync(Guid id, CancellationToken ct)
    {
        var s = await Scoped(id, ct);
        var introduction = await db.InterviewIntroductions.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == s.OrganizationId && x.InterviewSessionId == id, ct);
        return new(s.Id, s.CandidateApplicationId, s.JobPositionId, s.Status.ToString(), s.CompletionReason?.ToString(), s.StartedAtUtc, s.CompletedAtUtc,
            s.Questions.Select(q => new HrQuestionDto(q.QuestionId, q.SkillId, q.Text, q.Topic, q.Difficulty)).ToArray(),
            s.Answers.Select(a => new HrAnswerDto(a.Id, a.QuestionId, a.Status.ToString(), a.Transcript, a.SelectionSource, a.FailureStage, a.FailureCode,
                $"/api/interviews/{s.Id}/answers/{a.Id}/audio", a.VideoStorageKey is null ? null : $"/api/interviews/{s.Id}/answers/{a.Id}/video",
                a.ClientTurnId, a.LastSpeechEndUtc, a.DurationSeconds, a.IsInteraction, a.AssistantMessage)).ToArray(),
            s.SkillStates.Select(x => new HrSkillStateDto(x.SkillId, x.CurrentScore, x.EvidenceCount, x.Confidence)).ToArray(),
            s.Evidence.Select(x => new HrEvidenceDto(x.InterviewAnswerId, x.QuestionId, x.SkillId, x.ScoreBefore, x.ScoreAfter, x.Delta, x.Confidence, x.Evidence, x.CreatedAtUtc)).ToArray(),
            s.CoveredTopics.Select(x => new HrTopicDto(x.SkillId, x.Topic, x.Coverage, x.EvidenceCount)).ToArray(),
            s.CandidateIntroductionTranscript,
            introduction is null ? null : new(introduction.Id, introduction.Status.ToString(), introduction.Transcript, introduction.SubmittedAtUtc,
                $"/api/interviews/{s.Id}/introduction/audio", introduction.VideoStorageKey is null ? null : $"/api/interviews/{s.Id}/introduction/video"));
    }
    public async Task<(Stream Content, string ContentType)> OpenIntroductionMediaAsync(Guid id, bool video, CancellationToken ct)
    {
        var s = await MediaScopedAsync(id, ct);
        var introduction = await db.InterviewIntroductions.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == s.OrganizationId && x.InterviewSessionId == id, ct)
            ?? throw AppException.NotFound();
        var key = video ? introduction.VideoStorageKey : introduction.AudioStorageKey;
        if (key is null) throw AppException.NotFound();
        return (await storage.OpenReadAsync(key, ct), video ? introduction.VideoContentType! : introduction.AudioContentType);
    }
    public async Task RevokeAsync(Guid id, CancellationToken ct)
    {
        var s = await Scoped(id, ct);
        await billing.FinishAsync(s, true, async () => {
            await db.ReloadInterviewAsync(s, ct);
            if (s.Status is not (InterviewStatus.Completed or InterviewStatus.Cancelled)) s.Cancel(time.GetUtcNow());
            s.RevokeAccess(time.GetUtcNow()); return true;
        }, ct);
    }
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        // Load answers only after the billing lock, so settlement cannot miss an answer committed while deletion waited.
        var s = await db.InterviewSessions.IgnoreAutoIncludes().SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId && x.DeletedAtUtc == null, ct)
            ?? throw AppException.NotFound();
        await billing.FinishAsync(s, false, async () =>
        {
            await db.ReloadInterviewAsync(s, ct);
            if (s.DeletedAtUtc is not null) throw AppException.NotFound();
            await db.InterviewAnswers.Where(x => x.InterviewSessionId == id).ToListAsync(ct);
            if (s.Status is not (InterviewStatus.Completed or InterviewStatus.Cancelled)) s.Cancel(time.GetUtcNow());
            s.MarkDeleted(time.GetUtcNow());
            return true;
        }, ct);
    }
    public async Task<InterviewSession> Scoped(Guid id, CancellationToken ct) => await db.InterviewSessions.Include(x => x.Answers)
        .SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId && x.DeletedAtUtc == null, ct) ?? throw AppException.NotFound();
    public async Task<InterviewSession> MediaScopedAsync(Guid id, CancellationToken ct)
    { var session = await Scoped(id, ct); await billing.RequireMediaRetentionAsync(session, ct); return session; }
}
