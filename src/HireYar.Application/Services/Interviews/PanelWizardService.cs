using HireYar.Application.Common;
using HireYar.Application.Billing;
using HireYar.Domain.Interviews;
using HireYar.Domain.InterviewTemplates;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Application.Interviews;

public sealed class PanelWizardService(IApplicationDbContext db, ICurrentUser user, TimeProvider time,
    ISubscriptionEntitlementService entitlements, IInterviewAudioStorage storage)
{
    public async Task<PanelInvitationResult> InviteAsync(WizardInvitationRequest r, Stream resume, string filename, CancellationToken ct)
    {
        var job = await db.JobPositions.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == user.OrganizationId && x.Id == r.JobPositionId, ct)
            ?? throw AppException.NotFound();
        if (job.Status == HireYar.Domain.JobPositions.JobStatus.Archived) throw AppException.Invalid("position_archived");
        var template = await db.InterviewTemplates.AsNoTracking().Include(x => x.Questions)
            .SingleOrDefaultAsync(x => x.OrganizationId == user.OrganizationId && x.Id == r.TemplateId && !x.IsArchived, ct)
            ?? throw AppException.NotFound();
        if (template.Questions.Count is < 5 or > 30) throw AppException.Invalid("template_requires_five_questions");
        var capability = r.InterviewType == "specialized" ? "technical_interview" : "hr_interview";
        await entitlements.RequireAsync(user.OrganizationId, [capability, "ai_interview"], ct);
        var file = await ResumeUploadValidator.ReadAsync(resume, filename, ct);
        var now = time.GetUtcNow();
        // Keep the agreed position/template FK and reuse an organization's template by copying it to the selected position.
        if (template.JobPositionId != job.Id)
        {
            template = new InterviewTemplate { OrganizationId = user.OrganizationId, JobPositionId = job.Id,
                Title = template.Title, InterviewType = template.InterviewType, Language = template.Language,
                WelcomeMessage = template.WelcomeMessage, GoodbyeMessage = template.GoodbyeMessage,
                DurationMinutes = template.DurationMinutes, MaxFollowUpQuestionsPerQuestion = 0, CreatedAtUtc = now, UpdatedAtUtc = now,
                Questions = template.Questions.OrderBy(x => x.Order).Select(x => new InterviewQuestion {
                    Text = x.Text, Order = x.Order, Competency = x.Competency, EvaluationGuidance = x.EvaluationGuidance }).ToList() };
            foreach (var question in template.Questions) question.InterviewTemplateId = template.Id;
            db.InterviewTemplates.Add(template);
        }
        var application = new CandidateApplication { OrganizationId = user.OrganizationId, JobPositionId = job.Id,
            FirstName = r.FirstName.Trim(), LastName = r.LastName.Trim(), Email = r.Email.Trim(), Phone = r.Phone,
            CreatedAtUtc = now, UpdatedAtUtc = now, ResumeContentType = file.ContentType, ResumeExtension = file.Extension };
        // A simple text bank uses a session-local job-evidence skill; no HR skill setup is required.
        var skillId = Guid.NewGuid();
        var questions = template.Questions.OrderBy(x => x.Order).Select(x => new InterviewQuestionSnapshot(
            x.Id, skillId, x.Text, $"question-{x.Order}", 3, true, x.Order)).ToArray();
        var session = new InterviewSession(user.OrganizationId, job.Id, application.Id, template.Id,
            questions, [new InterviewSkill(skillId, "ارزیابی شغلی", true, 100, 3)], questions.Length,
            template.DurationMinutes * 60, now, now.AddDays(r.DeadlineDays));
        session.SetRequiredCapability(capability);
        session.SetPanelSnapshot(job.Title, job.Description, template.Title, template.WelcomeMessage, template.GoodbyeMessage);
        var token = InterviewTokens.Create(); session.SetAccessTokenHash(InterviewTokens.Hash(token));
        var saved = await storage.SaveAsync(new MemoryStream(file.Bytes), "resumes", application.Id, ResumeUploadValidator.MaxBytes, ct);
        try
        {
            application.ResumeStorageKey = saved.Key;
            db.CandidateApplications.Add(application); db.InterviewSessions.Add(session);
            // Application, optional template copy and session commit atomically in one SaveChanges.
            await db.SaveChangesAsync(ct);
        }
        catch { await storage.DeleteAsync(saved.Key, CancellationToken.None); throw; }
        return Result(session, token);
    }

    private static PanelInvitationResult Result(InterviewSession s, string token) => new(
        new(s.Id, token, s.ExpiresAtUtc, $"/api/candidate/interviews/{s.Id}"), "NotConfigured", "NotConfigured");

    public async Task<PanelInvitationResult> ResendAsync(Guid id, ResendInterviewRequest r, CancellationToken ct)
    {
        var session = await db.InterviewSessions.SingleOrDefaultAsync(x => x.OrganizationId == user.OrganizationId && x.Id == id, ct)
            ?? throw AppException.NotFound();
        if (session.Status != InterviewStatus.Created || session.AccessRevokedAtUtc is not null) throw AppException.Conflict();
        var token = InterviewTokens.Create(); var now = time.GetUtcNow();
        session.RenewInvitation(InterviewTokens.Hash(token), now, now.AddDays(r.DeadlineDays));
        await db.SaveChangesAsync(ct); return Result(session, token);
    }

    public async Task<(Stream Content, string ContentType, string Extension)> ResumeAsync(Guid id, CancellationToken ct)
    {
        var applicationId = await db.InterviewSessions.Where(x => x.Id == id && x.OrganizationId == user.OrganizationId)
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
                    where s.OrganizationId == user.OrganizationId
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
                x.s.AccessRevokedAtUtc,
                Score = x.s.SkillStates.Where(y => y.EvidenceCount > 0).Select(y => (decimal?)y.CurrentScore).Average() }).ToListAsync(ct);
        return new(rows.Select(x => new PanelInterviewDto(x.Id, x.FirstName, x.LastName, x.Email, x.Phone, x.JobPositionId,
            x.PositionTitle, x.TemplateId, x.TemplateTitle, x.RequiredCapabilityCode == "hr_interview" ? "general" : "specialized",
            x.status, x.CreatedAtUtc, x.ExpiresAtUtc, x.StartedAtUtc, x.CompletedAtUtc, (int)(x.ExpiresAtUtc - x.CreatedAtUtc).TotalDays,
            x.status == "completed" && x.Score.HasValue ? decimal.Round(x.Score.Value * 100, 2) : null,
            $"/api/interviews/{x.Id}", x.ResumeStorageKey is null ? null : $"/api/interviews/{x.Id}/resume",
            x.status is "pending" or "expired" && x.StartedAtUtc is null && x.AccessRevokedAtUtc is null,
            x.status is not ("completed" or "cancelled"), false)).ToArray(), total, r.Page, r.PageSize);
    }
}
