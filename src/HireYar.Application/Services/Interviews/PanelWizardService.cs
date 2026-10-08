using HireYar.Application.Common;
using HireYar.Application.Billing;
using HireYar.Domain.Interviews;
using HireYar.Domain.InterviewTemplates;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Application.Interviews;

public sealed class PanelWizardService(IApplicationDbContext db, ICurrentUser user, TimeProvider time,
    ISubscriptionEntitlementService entitlements, IInterviewAudioStorage storage, IResumeTextExtractor resumeText,
    IInterviewInvitationSecurity invitationSecurity, InterviewBillingService billing, IInterviewInvitationNotifier notifier)
{
    public async Task<PanelInvitationResult> InviteAsync(WizardInvitationRequest r, Stream resume, string filename, CancellationToken ct)
    {
        var job = await db.JobPositions.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == user.OrganizationId && x.DeletedAtUtc == null && x.Id == r.JobPositionId, ct)
            ?? throw AppException.NotFound();
        if (job.Status == HireYar.Domain.JobPositions.JobStatus.Archived) throw AppException.Invalid("position_archived");
        var template = await db.InterviewTemplates.AsNoTracking().Include(x => x.Questions)
            .SingleOrDefaultAsync(x => x.OrganizationId == user.OrganizationId && x.DeletedAtUtc == null && x.Id == r.TemplateId && !x.IsArchived, ct)
            ?? throw AppException.NotFound();
        if (template.Questions.Count is < 5 or > 30) throw AppException.Invalid("template_requires_five_questions");
        var capability = r.InterviewType == "specialized" ? "technical_interview" : "hr_interview";
        await entitlements.RequireAsync(user.OrganizationId, template.UseTopicQuestions ? [capability, "ai_interview", "advanced_ai_workflow", "adaptive_questions", "adaptive_difficulty"] : [capability, "ai_interview"], ct);
        var sourceTemplateId = template.Id;
        var file = await ResumeUploadValidator.ReadAsync(resume, filename, ct);
        var extracted = string.IsNullOrWhiteSpace(r.ResumeText) ? await resumeText.ExtractAsync(file.Bytes, file.Extension, ct) : r.ResumeText.Trim();
        if (string.IsNullOrWhiteSpace(extracted)) throw AppException.Invalid("resume_text_required");
        var now = time.GetUtcNow();
        // Keep the agreed position/template FK and reuse an organization's template by copying it to the selected position.
        if (template.JobPositionId != job.Id)
        {
            template = new InterviewTemplate { OrganizationId = user.OrganizationId, JobPositionId = job.Id,
                UseTopicQuestions = template.UseTopicQuestions, Title = template.Title, InterviewType = template.InterviewType, Language = template.Language,
                WelcomeMessage = template.WelcomeMessage, GoodbyeMessage = template.GoodbyeMessage,
                CandidateIntroductionEnabled = template.CandidateIntroductionEnabled, CandidateIntroductionMessage = template.CandidateIntroductionMessage,
                CompanyIntroductionEnabled = template.CompanyIntroductionEnabled, CompanyIntroductionMessage = template.CompanyIntroductionMessage,
                DurationMinutes = template.DurationMinutes, MaxFollowUpQuestionsPerQuestion = 0, CreatedAtUtc = now, UpdatedAtUtc = now,
                Questions = template.Questions.OrderBy(x => x.Order).Select(x => new InterviewQuestion {
                    TopicName = x.TopicName, TopicOrder = x.TopicOrder, Difficulty = x.Difficulty, Text = x.Text, Order = x.Order, Competency = x.Competency, EvaluationGuidance = x.EvaluationGuidance }).ToList() };
            foreach (var question in template.Questions) question.InterviewTemplateId = template.Id;
            db.InterviewTemplates.Add(template);
        }
        var application = new CandidateApplication { OrganizationId = user.OrganizationId, JobPositionId = job.Id,
            FirstName = r.FirstName.Trim(), LastName = r.LastName.Trim(), Email = r.Email.Trim(), Phone = r.Phone,
            CreatedAtUtc = now, UpdatedAtUtc = now, ResumeContentType = file.ContentType, ResumeExtension = file.Extension };
        // A simple text bank uses a session-local job-evidence skill; no HR skill setup is required.
        var skillId = Guid.NewGuid();
        var topicSkills = template.UseTopicQuestions ? template.Questions.GroupBy(x => x.TopicOrder)
            .ToDictionary(g => g.Key!.Value, g => new InterviewSkill(Guid.NewGuid(), g.First().TopicName!, true, 100, 3)) : [];
        var questions = template.Questions.OrderBy(x => x.Order).Select(x => new InterviewQuestionSnapshot(
            x.Id, template.UseTopicQuestions ? topicSkills[x.TopicOrder!.Value].SkillId : skillId,
            x.Text, template.UseTopicQuestions ? x.TopicName! : $"question-{x.Order}", x.Difficulty, true, x.Order)).ToArray();
        var session = new InterviewSession(user.OrganizationId, job.Id, application.Id, template.Id,
            questions, template.UseTopicQuestions ? topicSkills.Values : [new InterviewSkill(skillId, "ارزیابی شغلی", true, 100, 3)],
            template.UseTopicQuestions ? topicSkills.Count : questions.Length,
            template.DurationMinutes * 60, now, now.AddDays(r.DeadlineDays));
        if (template.UseTopicQuestions) session.EnableTopicQuestions();
        session.SetRequiredCapability(capability);
        session.SetInteractionLimit(2);
        session.SetPanelSnapshot(job.Title, job.Description, template.Title, template.WelcomeMessage, template.GoodbyeMessage);
        session.SetAiSnapshot(InterviewDecisionPrompt.Master, InterviewDecisionPrompt.Version, extracted, template.Language,
            template.CandidateIntroductionEnabled, template.CandidateIntroductionMessage, template.CompanyIntroductionEnabled, template.CompanyIntroductionMessage);
        var token = IssueCode(session);
        var saved = await storage.SaveAsync(new MemoryStream(file.Bytes), "resumes", application.Id, ResumeUploadValidator.MaxBytes, ct);
        try
        {
            application.ResumeStorageKey = saved.Key;
            await db.WithOrganizationBillingLockAsync(user.OrganizationId, async () =>
            {
                // Serialize invitation persistence with position/template removal. File upload stays outside the lock.
                if (!await db.JobPositions.AnyAsync(x => x.Id == job.Id && x.OrganizationId == user.OrganizationId && x.DeletedAtUtc == null && x.Status != HireYar.Domain.JobPositions.JobStatus.Archived, ct) ||
                    !await db.InterviewTemplates.AnyAsync(x => x.Id == sourceTemplateId && x.OrganizationId == user.OrganizationId && x.DeletedAtUtc == null && !x.IsArchived, ct))
                    throw AppException.NotFound();
                db.CandidateApplications.Add(application); db.InterviewSessions.Add(session);
                return true;
            }, ct);
        }
        catch { await storage.DeleteAsync(saved.Key, CancellationToken.None); throw; }
        return await NotifyAsync(session, token, application, r.SendEmail, r.SendSms, ct);
    }

    private string IssueCode(InterviewSession session, string? previous = null)
    {
        var code = invitationSecurity.CreateCode(previous); var encrypted = invitationSecurity.Protect(session.Id, code);
        session.SetProtectedAccessCode(encrypted, InterviewTokens.Hash(encrypted)); return code;
    }
    private PanelInvitationResult Result(InterviewSession s, string token) => new(
        new(s.Id, token, s.ExpiresAtUtc, $"/api/candidate/interviews/{s.Id}", token, invitationSecurity.Link(s.Id)), "NotConfigured", "NotConfigured");

    private async Task<PanelInvitationResult> NotifyAsync(InterviewSession s, string code, CandidateApplication candidate, bool email, bool sms, CancellationToken ct)
    {
        var result = Result(s, code);
        var delivery = await notifier.SendAsync(new(candidate.Email, candidate.Phone, $"{candidate.FirstName} {candidate.LastName}",
            s.PositionTitleSnapshot ?? "", result.Invitation), email, sms, ct);
        return result with { EmailDelivery = delivery.EmailDelivery, SmsDelivery = delivery.SmsDelivery };
    }

    public async Task<InterviewAccessDetails> AccessAsync(Guid id, CancellationToken ct)
    {
        var s = await db.InterviewSessions.AsNoTracking().IgnoreAutoIncludes().SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId && x.DeletedAtUtc == null, ct)
            ?? throw AppException.NotFound();
        return new(s.Id, invitationSecurity.Link(s.Id), invitationSecurity.Unprotect(s.Id, s.ProtectedAccessCode), s.ExpiresAtUtc,
            s.SupersededByInterviewId is null &&
            await db.JobPositions.AnyAsync(j => j.Id == s.JobPositionId && j.OrganizationId == user.OrganizationId && j.DeletedAtUtc == null && j.Status != HireYar.Domain.JobPositions.JobStatus.Archived, ct) &&
            await db.InterviewTemplates.AnyAsync(t => t.Id == s.InterviewTemplateId && t.OrganizationId == user.OrganizationId && t.DeletedAtUtc == null && !t.IsArchived, ct),
            InterviewInvitationStatus.Get(s.SupersededByInterviewId, s.AccessRevokedAtUtc, s.Status, s.ExpiresAtUtc, time.GetUtcNow()).Status);
    }

    public async Task<PanelInvitationResult> ResendAsync(Guid id, ResendInterviewRequest r, CancellationToken ct)
    {
        var session = await db.InterviewSessions.SingleOrDefaultAsync(x => x.OrganizationId == user.OrganizationId && x.DeletedAtUtc == null && x.Id == id, ct)
            ?? throw AppException.NotFound();
        if (r.DeadlineDays is < 1 or > 14) throw AppException.Invalid();
        var result = await billing.FinishAsync(session, false, async () => {
            await db.ReloadInterviewAsync(session, ct);
            if (session.DeletedAtUtc is not null) throw AppException.NotFound();
            if (session.SupersededByInterviewId is not null) throw new AppException(409, "invitation_already_replaced", "برای این مصاحبه دعوت جدیدی ساخته شده است.");
            var job = await db.JobPositions.AsNoTracking().SingleOrDefaultAsync(j => j.Id == session.JobPositionId && j.OrganizationId == user.OrganizationId && j.DeletedAtUtc == null && j.Status != HireYar.Domain.JobPositions.JobStatus.Archived, ct) ?? throw AppException.NotFound();
            var template = await db.InterviewTemplates.AsNoTracking().SingleOrDefaultAsync(t => t.Id == session.InterviewTemplateId && t.OrganizationId == user.OrganizationId && t.DeletedAtUtc == null && !t.IsArchived, ct) ?? throw AppException.NotFound();
            await entitlements.RequireAsync(user.OrganizationId, session.AskOneQuestionPerTopic
                ? [session.RequiredCapabilityCode, "ai_interview", "advanced_ai_workflow", "adaptive_questions", "adaptive_difficulty"]
                : [session.RequiredCapabilityCode, "ai_interview"], ct);
            await db.InterviewAnswers.Where(a => a.InterviewSessionId == session.Id).ToListAsync(ct);
            var now = time.GetUtcNow();
            var fresh = new InterviewSession(session.OrganizationId, session.JobPositionId, session.CandidateApplicationId, session.InterviewTemplateId,
                session.Questions.Select(q => new InterviewQuestionSnapshot(q.QuestionId, q.SkillId, q.Text, q.Topic, q.Difficulty, q.IsActive, q.Order)),
                session.Skills, session.MaxQuestionCount, session.MaxDurationSeconds, now, now.AddDays(r.DeadlineDays));
            if (session.AskOneQuestionPerTopic) fresh.EnableTopicQuestions();
            fresh.SetRequiredCapability(session.RequiredCapabilityCode); fresh.SetInteractionLimit(session.MaxInteractionsPerQuestion);
            fresh.SetPanelSnapshot(session.PositionTitleSnapshot ?? job.Title, session.PositionDescriptionSnapshot ?? job.Description,
                session.TemplateTitleSnapshot ?? template.Title, session.WelcomeMessage ?? template.WelcomeMessage, session.GoodbyeMessage ?? template.GoodbyeMessage);
            fresh.SetAiSnapshot(session.MasterPromptSnapshot ?? InterviewDecisionPrompt.Master, session.MasterPromptVersion ?? InterviewDecisionPrompt.Version,
                session.ResumeTextSnapshot, session.Language, session.CandidateIntroductionEnabled, session.CandidateIntroductionMessage ?? "",
                session.CompanyIntroductionEnabled, session.CompanyIntroductionMessage ?? "");
            var code = IssueCode(fresh, invitationSecurity.Unprotect(session.Id, session.ProtectedAccessCode));
            db.InterviewSessions.Add(fresh);
            // Persist the new principal before the self-reference; this is still the same organization transaction.
            await db.SaveChangesAsync(ct);
            if (session.Status is not (InterviewStatus.Completed or InterviewStatus.Cancelled)) session.Cancel(now);
            session.Supersede(fresh.Id, now);
            var application = await db.CandidateApplications.SingleAsync(a => a.Id == session.CandidateApplicationId && a.OrganizationId == user.OrganizationId, ct);
            application.Status = CandidateApplicationStatus.Invited; application.UpdatedAtUtc = now;
            return Result(fresh, code);
        }, ct);
        var candidate = await db.CandidateApplications.AsNoTracking().SingleAsync(a => a.Id == session.CandidateApplicationId && a.OrganizationId == user.OrganizationId, ct);
        var delivery = await notifier.SendAsync(new(candidate.Email, candidate.Phone, $"{candidate.FirstName} {candidate.LastName}",
            session.PositionTitleSnapshot ?? "", result.Invitation), r.SendEmail, r.SendSms, ct);
        return result with { EmailDelivery = delivery.EmailDelivery, SmsDelivery = delivery.SmsDelivery };
    }

    public async Task<(Stream Content, string ContentType, string Extension)> ResumeAsync(Guid id, CancellationToken ct)
    {
        var applicationId = await db.InterviewSessions.Where(x => x.Id == id && x.OrganizationId == user.OrganizationId && x.DeletedAtUtc == null)
            .Select(x => (Guid?)x.CandidateApplicationId).SingleOrDefaultAsync(ct) ?? throw AppException.NotFound();
        var a = await db.CandidateApplications.AsNoTracking().SingleAsync(x => x.OrganizationId == user.OrganizationId && x.Id == applicationId, ct);
        if (a.ResumeStorageKey is null) throw AppException.NotFound();
        return (await storage.OpenReadAsync(a.ResumeStorageKey, ct), a.ResumeContentType!, a.ResumeExtension!);
    }

    public async Task<Page<PanelInterviewDto>> ListAsync(InterviewListQuery r, CancellationToken ct)
    {
        string[] valid = ["pending", "inProgress", "completed", "expired", "cancelled"];
        if (r.Statuses?.Any(x => !valid.Contains(x)) == true ||
            r.InvitedFromUtc > r.InvitedToUtc || r.CompletedFromUtc > r.CompletedToUtc) throw AppException.Invalid();
        var now = time.GetUtcNow();
        var query = from s in db.InterviewSessions.AsNoTracking().IgnoreAutoIncludes()
                    where s.OrganizationId == user.OrganizationId && s.DeletedAtUtc == null
                    join a in db.CandidateApplications on s.CandidateApplicationId equals a.Id
                    join j in db.JobPositions on s.JobPositionId equals j.Id
                    join t in db.InterviewTemplates on s.InterviewTemplateId equals t.Id
                    where a.OrganizationId == user.OrganizationId && j.OrganizationId == user.OrganizationId && t.OrganizationId == user.OrganizationId
                    let status = s.Status == InterviewStatus.Cancelled ? "cancelled" : s.Status == InterviewStatus.Completed ? "completed"
                        : s.ExpiresAtUtc <= now ? "expired" : s.Status == InterviewStatus.Created ? "pending" : "inProgress"
                    select new { s, a, j, t, status };
        if (r.JobPositionId.HasValue) query = query.Where(x => x.s.JobPositionId == r.JobPositionId);
        if (r.Statuses?.Length > 0) query = query.Where(x => r.Statuses.Contains(x.status));
        if (!string.IsNullOrWhiteSpace(r.Search)) { var search = r.Search.Trim(); query = query.Where(x => (x.a.FirstName + " " + x.a.LastName).Contains(search) || x.a.Email.Contains(search) || (x.a.Phone != null && x.a.Phone.Contains(search))); }
        if (r.InvitedFromUtc.HasValue) query = query.Where(x => x.s.CreatedAtUtc >= r.InvitedFromUtc.Value);
        if (r.InvitedToUtc.HasValue) query = query.Where(x => x.s.CreatedAtUtc <= r.InvitedToUtc.Value);
        if (r.CompletedFromUtc.HasValue) query = query.Where(x => x.s.Status == InterviewStatus.Completed && x.s.CompletedAtUtc >= r.CompletedFromUtc.Value);
        if (r.CompletedToUtc.HasValue) query = query.Where(x => x.s.Status == InterviewStatus.Completed && x.s.CompletedAtUtc <= r.CompletedToUtc.Value);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.s.CreatedAtUtc).ThenBy(x => x.s.Id).Skip(r.Skip).Take(r.PageSize)
            .Select(x => new { x.s.Id, x.a.FirstName, x.a.LastName, x.a.Email, x.a.Phone, x.s.JobPositionId,
                PositionTitle = x.s.PositionTitleSnapshot ?? x.j.Title, TemplateId = x.s.InterviewTemplateId,
                TemplateTitle = x.s.TemplateTitleSnapshot ?? x.t.Title, x.s.RequiredCapabilityCode, x.status,
                x.s.CreatedAtUtc, x.s.ExpiresAtUtc, x.s.StartedAtUtc, x.s.CompletedAtUtc, x.a.ResumeStorageKey,
                x.s.AccessRevokedAtUtc, x.s.SupersededByInterviewId, x.s.ProtectedAccessCode, InterviewStatus = x.s.Status,
                CanResend = x.s.SupersededByInterviewId == null && x.j.DeletedAtUtc == null && x.j.Status != HireYar.Domain.JobPositions.JobStatus.Archived && x.t.DeletedAtUtc == null && !x.t.IsArchived,
                VideoAvailable = x.s.Answers.Any(a => a.VideoStorageKey != null),
                Score = x.s.SkillStates.Where(y => y.EvidenceCount > 0).Select(y => (decimal?)y.CurrentScore).Average() }).ToListAsync(ct);
        return new(rows.Select(x => new PanelInterviewDto(x.Id, x.FirstName, x.LastName, x.Email, x.Phone, x.JobPositionId,
            x.PositionTitle, x.TemplateId, x.TemplateTitle, x.RequiredCapabilityCode == "hr_interview" ? "general" : "specialized",
            x.status, x.CreatedAtUtc, x.ExpiresAtUtc, x.StartedAtUtc, x.CompletedAtUtc, (int)(x.ExpiresAtUtc - x.CreatedAtUtc).TotalDays,
            x.status == "completed" && x.Score.HasValue ? decimal.Round(x.Score.Value * 100, 2) : null,
            $"/api/interviews/{x.Id}", x.ResumeStorageKey is null ? null : $"/api/interviews/{x.Id}/resume",
            x.CanResend,
            x.status is not ("completed" or "cancelled"), x.VideoAvailable, invitationSecurity.Link(x.Id),
            invitationSecurity.Unprotect(x.Id, x.ProtectedAccessCode),
            InterviewInvitationStatus.Get(x.SupersededByInterviewId, x.AccessRevokedAtUtc, x.InterviewStatus, x.ExpiresAtUtc, now).Status)).ToArray(), total, r.Page, r.PageSize);
    }
}
