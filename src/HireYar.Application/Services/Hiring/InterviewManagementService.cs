using System.Text.Json;
using HireYar.Application.Common;
using HireYar.Domain.Hiring;
using HireYar.Domain.JobPositions;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Hiring;

public sealed class InterviewManagementService(IApplicationDbContext db, ICurrentUser user, TimeProvider time, IInterviewEmail email)
{
    private IQueryable<InterviewSession> Scoped => db.InterviewSessions.Where(x => x.OrganizationId == user.OrganizationId);
    public async Task<Page<SessionListDto>> ListAsync(PageQuery page, Guid? applicationId, CancellationToken ct)
    {
        if (applicationId.HasValue && !await db.JobApplications.AnyAsync(x => x.Id == applicationId && x.OrganizationId == user.OrganizationId, ct)) throw AppException.NotFound();
        var query = Scoped.AsNoTracking(); if (applicationId.HasValue) query = query.Where(x => x.JobApplicationId == applicationId);
        var total = await query.CountAsync(ct);
        var rows = await query.Include(x => x.Invitation).OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new(rows.Select(x => InterviewRules.List(x, InterviewRules.Now(time))).ToArray(), total, page.Page, page.PageSize);
    }
    public async Task<HrSessionDto> GetAsync(Guid id, CancellationToken ct)
    {
        var s = await Scoped.AsNoTracking().Include(x => x.Invitation).Include(x => x.Questions).Include(x => x.Answers).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        return new(InterviewRules.List(s, InterviewRules.Now(time)), s.OrganizationName, s.CandidateEmail, s.Language, s.DurationMinutes, s.MaxFollowUpQuestionsPerQuestion, s.StartedAtUtc, s.CompletedAtUtc, s.CancelledAtUtc, s.LastActivityAtUtc, s.ConsentVersion, s.ConsentAcceptedAtUtc, s.Questions.OrderBy(q => q.Order).Select(q => new HrQuestionDto(q.Id, q.Order, q.Text, q.Competency, q.EvaluationGuidance)).ToArray(), s.Answers.Select(InterviewRules.Answer).ToArray());
    }
    public async Task<SessionListDto> CreateAsync(CreateSessionRequest r, CancellationToken ct)
    {
        var normalized = r with { AvailableFromUtc = InterviewRules.Utc(r.AvailableFromUtc), ExpiresAtUtc = InterviewRules.Utc(r.ExpiresAtUtc) };
        var hash = InterviewRules.Hash(JsonSerializer.Serialize(normalized));
        InterviewSession s; string token;
        await using (var tx = await db.BeginHiringTransactionAsync(user.OrganizationId, ct))
        {
            var existing = await Scoped.Include(x => x.Invitation).SingleOrDefaultAsync(x => x.IdempotencyKey == r.IdempotencyKey, ct);
            if (existing is not null)
            {
                if (existing.RequestHash != hash) throw new AppException(409, "idempotency_conflict", "The key was already used with a different request.");
                return InterviewRules.List(existing, InterviewRules.Now(time));
            }
            var now = InterviewRules.Now(time);
            if (normalized.ExpiresAtUtc <= now || normalized.AvailableFromUtc >= normalized.ExpiresAtUtc) throw AppException.Invalid("invalid_interview_window");
            var application = await db.JobApplications.SingleOrDefaultAsync(x => x.Id == r.JobApplicationId && x.OrganizationId == user.OrganizationId, ct) ?? throw AppException.NotFound();
            if (application.Status != ApplicationStatus.Active) throw AppException.Invalid("application_inactive");
            await db.LockInterviewSourcesAsync(user.OrganizationId, application.JobPositionId, r.InterviewTemplateId, ct);
            var job = await db.JobPositions.SingleAsync(x => x.Id == application.JobPositionId && x.OrganizationId == user.OrganizationId, ct);
            if (job.Status == JobStatus.Archived) throw AppException.Invalid("job_archived");
            var template = await db.InterviewTemplates.Include(x => x.Questions).SingleOrDefaultAsync(x => x.Id == r.InterviewTemplateId && x.OrganizationId == user.OrganizationId && x.JobPositionId == application.JobPositionId, ct) ?? throw AppException.NotFound();
            if (template.IsArchived) throw AppException.Invalid("template_archived");
            if (template.Questions.Count is < 1 or > 30 || !template.Questions.OrderBy(x => x.Order).Select(x => x.Order).SequenceEqual(Enumerable.Range(1, template.Questions.Count))) throw AppException.Invalid("invalid_template_questions");
            var candidate = await db.Candidates.SingleAsync(x => x.Id == application.CandidateId && x.OrganizationId == user.OrganizationId, ct);
            var organization = await db.Organizations.SingleAsync(x => x.Id == user.OrganizationId, ct);
            s = new() { OrganizationId = user.OrganizationId, JobApplicationId = application.Id, SourceInterviewTemplateId = template.Id, AvailableFromUtc = normalized.AvailableFromUtc, ExpiresAtUtc = normalized.ExpiresAtUtc, CreatedAtUtc = now, OrganizationName = organization.Name, JobTitle = job.Title, InterviewTitle = template.Title, CandidateName = candidate.FullName, CandidateEmail = candidate.Email, Language = template.Language, DurationMinutes = template.DurationMinutes, MaxFollowUpQuestionsPerQuestion = template.MaxFollowUpQuestionsPerQuestion, IdempotencyKey = r.IdempotencyKey, RequestHash = hash };
            s.Questions = template.Questions.Select(q => new SnapshotQuestion { InterviewSessionId = s.Id, Order = q.Order, Text = q.Text, Competency = q.Competency, EvaluationGuidance = q.EvaluationGuidance }).ToList();
            token = InterviewRules.NewToken(); s.Invitation = new() { InterviewSessionId = s.Id, OrganizationId = s.OrganizationId, TokenHash = InterviewRules.Hash(token), ExpiresAtUtc = s.ExpiresAtUtc, LastAttemptAtUtc = now, SendAttempts = 1 };
            db.InterviewSessions.Add(s); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        await DeliverAsync(s, token, ct); return InterviewRules.List(s, InterviewRules.Now(time));
    }
    public async Task<SessionListDto> ResendAsync(Guid id, CancellationToken ct)
    {
        InterviewSession s; string token;
        await using (var tx = await db.BeginHiringTransactionAsync(user.OrganizationId, ct))
        {
            s = await Scoped.Include(x => x.Invitation).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
            var now = InterviewRules.Now(time);
            if (InterviewRules.Effective(s, now) != SessionStatus.NotStarted) throw AppException.Invalid("session_not_invitable");
            if (s.Invitation.LastAttemptAtUtc > now.AddMinutes(-1)) throw new AppException(429, "invitation_rate_limited", "Wait before resending this invitation.");
            var application = await db.JobApplications.SingleAsync(x => x.Id == s.JobApplicationId && x.OrganizationId == user.OrganizationId, ct);
            if (application.Status != ApplicationStatus.Active) throw AppException.Invalid("application_inactive");
            if (await db.JobPositions.AnyAsync(x => x.Id == application.JobPositionId && x.OrganizationId == user.OrganizationId && x.Status == JobStatus.Archived, ct)) throw AppException.Invalid("job_archived");
            token = InterviewRules.NewToken(); var i = s.Invitation;
            i.TokenHash = InterviewRules.Hash(token); i.UsedAtUtc = null; i.RevokedAtUtc = null; i.CredentialHash = null; i.CredentialExpiresAtUtc = null;
            i.DeliveryStatus = DeliveryStatus.Pending; i.LastAttemptAtUtc = now; i.SendAttempts++;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        await DeliverAsync(s, token, ct); return InterviewRules.List(s, InterviewRules.Now(time));
    }
    private async Task DeliverAsync(InterviewSession s, string token, CancellationToken ct)
    {
        var tokenHash = InterviewRules.Hash(token); var sent = false;
        try { await email.SendAsync(new(s.CandidateEmail, s.OrganizationName, s.JobTitle, s.DurationMinutes, s.AvailableFromUtc, s.ExpiresAtUtc, token), ct); sent = true; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { /* Delivery status is persisted without exposing recipient, token or provider errors. */ }
        // A resend or exchange may have occurred during SMTP. Update only this attempt.
        await db.InterviewInvitations.Where(x => x.InterviewSessionId == s.Id && x.OrganizationId == s.OrganizationId && x.TokenHash == tokenHash)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.DeliveryStatus, sent ? DeliveryStatus.Sent : DeliveryStatus.Failed).SetProperty(x => x.LastSentAtUtc, sent ? InterviewRules.Now(time) : s.Invitation.LastSentAtUtc), ct);
        s.Invitation.DeliveryStatus = sent ? DeliveryStatus.Sent : DeliveryStatus.Failed;
        if (sent) s.Invitation.LastSentAtUtc = InterviewRules.Now(time);
    }
    public async Task<SessionListDto> CancelAsync(Guid id, VersionRequest r, CancellationToken ct)
    {
        await using var tx = await db.BeginHiringTransactionAsync(user.OrganizationId, ct);
        var s = await Scoped.Include(x => x.Invitation).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        if (InterviewRules.Effective(s, InterviewRules.Now(time)) is not (SessionStatus.NotStarted or SessionStatus.InProgress)) throw AppException.Invalid("session_terminal");
        db.SetOriginalVersion(s, r.RowVersion); s.Status = SessionStatus.Cancelled; s.CancelledAtUtc = InterviewRules.Now(time); s.Invitation.RevokedAtUtc = s.CancelledAtUtc; s.Invitation.CredentialHash = null;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return InterviewRules.List(s, InterviewRules.Now(time));
    }
}


