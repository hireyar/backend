using HireYar.Application.Common;
using HireYar.Domain.Hiring;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Hiring;

public sealed class CandidateInterviewService(IApplicationDbContext db, TimeProvider time)
{
    private IQueryable<InterviewSession> Sessions => db.InterviewSessions.Include(x => x.Invitation).Include(x => x.Questions).Include(x => x.Answers);
    private static AppException Error(int status, string code) => new(status, code, code switch {
        "invitation_used" => "This invitation was used. Continue in the browser with the existing session, or ask HR to resend if the interview has not started.",
        "session_too_early" => "The interview is not available yet.",
        _ => "The interview operation is not available."
    });
    public async Task<PublicSessionDto> PreviewAsync(string token, CancellationToken ct)
    {
        var hash = InterviewRules.Hash(token);
        var s = await Sessions.AsNoTracking().SingleOrDefaultAsync(x => x.Invitation.TokenHash == hash, ct) ?? throw Error(400, "invitation_invalid");
        CheckInvitation(s, InterviewRules.Now(time), allowUsed: false);
        return InterviewRules.Public(s, InterviewRules.Now(time));
    }
    private static void CheckInvitation(InterviewSession s, DateTimeOffset now, bool allowUsed)
    {
        if (s.Invitation.RevokedAtUtc.HasValue || s.Status == SessionStatus.Cancelled) throw Error(410, "session_cancelled");
        if (now >= s.Invitation.ExpiresAtUtc || InterviewRules.Effective(s, now) == SessionStatus.Expired) throw Error(410, "invitation_expired");
        if (!allowUsed && s.Invitation.UsedAtUtc.HasValue) throw Error(409, "invitation_used");
    }
    public async Task<CandidateExchange> ExchangeAsync(string token, string? credential, CancellationToken ct)
    {
        var hash = InterviewRules.Hash(token);
        var org = await db.InterviewInvitations.AsNoTracking().Where(x => x.TokenHash == hash).Select(x => (Guid?)x.OrganizationId).SingleOrDefaultAsync(ct) ?? throw Error(400, "invitation_invalid");
        await using var tx = await db.BeginHiringTransactionAsync(org, ct);
        var s = await Sessions.SingleOrDefaultAsync(x => x.Invitation.TokenHash == hash && x.OrganizationId == org, ct) ?? throw Error(400, "invitation_invalid");
        var now = InterviewRules.Now(time); CheckInvitation(s, now, allowUsed: true);
        if (s.Invitation.UsedAtUtc.HasValue)
        {
            if (credential is null || s.Invitation.CredentialHash != InterviewRules.Hash(credential) || s.Invitation.CredentialExpiresAtUtc <= now) throw Error(409, "invitation_used");
            return new(null, s.Invitation.CredentialExpiresAtUtc!.Value, InterviewRules.Public(s, now));
        }
        var secret = InterviewRules.NewToken(); s.Invitation.UsedAtUtc = now; s.Invitation.CredentialHash = InterviewRules.Hash(secret); s.Invitation.CredentialExpiresAtUtc = s.ExpiresAtUtc;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(secret, s.ExpiresAtUtc, InterviewRules.Public(s, now));
    }
    private async Task<PublicSessionDto> OperateAsync(string? credential, Func<InterviewSession, DateTimeOffset, Task> action, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(credential) || credential.Length > 128) throw Error(401, "candidate_session_invalid");
        var hash = InterviewRules.Hash(credential);
        var org = await db.InterviewInvitations.AsNoTracking().Where(x => x.CredentialHash == hash).Select(x => (Guid?)x.OrganizationId).SingleOrDefaultAsync(ct) ?? throw Error(401, "candidate_session_invalid");
        await using var tx = await db.BeginHiringTransactionAsync(org, ct);
        var s = await Sessions.SingleOrDefaultAsync(x => x.Invitation.CredentialHash == hash && x.OrganizationId == org, ct) ?? throw Error(401, "candidate_session_invalid");
        var now = InterviewRules.Now(time);
        if (s.Invitation.RevokedAtUtc.HasValue || s.Status == SessionStatus.Cancelled) throw Error(410, "session_cancelled");
        var effective = InterviewRules.Effective(s, now);
        if (effective == SessionStatus.Expired)
        {
            s.Status = effective; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); throw Error(410, "session_expired");
        }
        if (s.Invitation.CredentialExpiresAtUtc <= now) throw Error(401, "candidate_session_expired");
        await action(s, now); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return InterviewRules.Public(s, now);
    }
    public Task<PublicSessionDto> GetAsync(string? credential, CancellationToken ct) => OperateAsync(credential, (_, _) => Task.CompletedTask, ct);
    public Task<PublicSessionDto> StartAsync(string? credential, ConsentRequest r, CancellationToken ct) => OperateAsync(credential, async (s, now) =>
    {
        if (s.Status == SessionStatus.InProgress) return;
        if (s.Status != SessionStatus.NotStarted) throw Error(409, "session_terminal");
        if (!r.Accepted || r.Version != InterviewRules.ConsentVersion) throw Error(400, "consent_required");
        if (now < s.AvailableFromUtc) throw Error(409, "session_too_early");
        if (!await db.JobApplications.AnyAsync(x => x.Id == s.JobApplicationId && x.OrganizationId == s.OrganizationId && x.Status == ApplicationStatus.Active, ct)) throw Error(409, "application_inactive");
        s.Status = SessionStatus.InProgress; s.StartedAtUtc = now; s.DeadlineAtUtc = now.AddMinutes(s.DurationMinutes) < s.ExpiresAtUtc ? now.AddMinutes(s.DurationMinutes) : s.ExpiresAtUtc;
        s.ConsentVersion = r.Version; s.ConsentAcceptedAtUtc = now; s.LastActivityAtUtc = now;
    }, ct);
    public Task<PublicSessionDto> AnswerAsync(string? credential, Guid questionId, AnswerRequest r, CancellationToken ct) => OperateAsync(credential, (s, now) =>
    {
        if (s.Status != SessionStatus.InProgress) throw Error(409, "session_not_in_progress");
        var text = r.Text?.Trim(); if (text?.Length == 0) text = null;
        if (r.Status == AnswerStatus.Submitted && text is null || r.Status == AnswerStatus.Skipped && text is not null) throw Error(400, "invalid_answer");
        var existing = s.Answers.SingleOrDefault(x => x.SnapshotQuestionId == questionId);
        if (existing is not null && existing.Status != AnswerStatus.Draft)
        {
            if (existing.Status == r.Status && existing.Text == text) return Task.CompletedTask;
            throw Error(409, "answer_finalized");
        }
        if (InterviewRules.Current(s)?.Id != questionId) throw Error(409, "question_not_current");
        if (existing is not null)
        {
            if (r.RowVersion is null || !existing.RowVersion.SequenceEqual(r.RowVersion)) throw Error(409, "concurrency_conflict");
            db.SetOriginalVersion(existing, r.RowVersion);
        }
        else
        {
            if (r.RowVersion is not null) throw Error(409, "concurrency_conflict");
            existing = new() { InterviewSessionId = s.Id, SnapshotQuestionId = questionId }; s.Answers.Add(existing); db.InterviewAnswers.Add(existing);
        }
        existing.Text = text; existing.Status = r.Status; existing.UpdatedAtUtc = now; existing.SubmittedAtUtc = r.Status == AnswerStatus.Draft ? null : now;
        s.LastActivityAtUtc = now; return Task.CompletedTask;
    }, ct);
    public Task<PublicSessionDto> CompleteAsync(string? credential, CancellationToken ct) => OperateAsync(credential, (s, now) =>
    {
        if (s.Status == SessionStatus.Completed) return Task.CompletedTask;
        if (s.Status != SessionStatus.InProgress) throw Error(409, "session_not_in_progress");
        if (InterviewRules.Current(s) is not null) throw Error(409, "answers_incomplete");
        s.Status = SessionStatus.Completed; s.CompletedAtUtc = now; s.LastActivityAtUtc = now; return Task.CompletedTask;
    }, ct);
}


