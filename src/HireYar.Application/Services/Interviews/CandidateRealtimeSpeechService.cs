using HireYar.Application.Common;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Interviews;
public sealed partial class CandidateInterviewService
{
    public SpeechCapabilitiesDto SpeechCapabilities(Guid id) => stt is IRealtimeSpeechToTextService
        ? new("realtime", $"/api/candidate/interviews/{id}/speech/stream") : new("batch", null);
    public async Task<SpeechCapabilitiesDto> SpeechCapabilitiesAsync(Guid id, CancellationToken ct)
    { await Load(id, ct); return SpeechCapabilities(id); }
    public async Task<RealtimeSpeechContext> RealtimeContextAsync(Guid id, string purpose, string turnId, Guid? questionId, CancellationToken ct)
    {
        if (access.InterviewId != id) throw AppException.NotFound();
        if (string.IsNullOrWhiteSpace(turnId) || turnId.Length > 128 || turnId != turnId.Trim()) throw AppException.Invalid("client_turn_id_invalid");
        var session = await db.InterviewSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        CheckAccess(session);
        if (session.AccessLockedUntilUtc > time.GetUtcNow()) throw new AppException(429, "interview_access_locked", "دسترسی مصاحبه موقتاً قفل است.");
        if (purpose == "audioCheck") {
            if (session.Status != InterviewStatus.Created || questionId is not null) throw AppException.Conflict();
        } else {
            if (session.Status != InterviewStatus.WaitingForAnswer || session.RemainingSeconds(time.GetUtcNow()) <= 0) throw AppException.Conflict();
            if (purpose == "answer") {
                if (session.Stage != InterviewStage.Questions || questionId is null || session.CurrentQuestionId != questionId) throw AppException.Conflict();
            } else if (purpose == "introduction") {
                if (session.Stage != InterviewStage.CandidateIntroduction || !session.CandidateIntroductionEnabled || questionId is not null ||
                    await db.InterviewIntroductions.AnyAsync(x => x.InterviewSessionId == id, ct)) throw AppException.Conflict();
            } else throw AppException.Invalid("realtime_purpose_invalid");
        }
        if (await db.InterviewAnswers.AnyAsync(x => x.InterviewSessionId == id && x.ClientTurnId == turnId, ct)) throw AppException.Conflict();
        return new(session.OrganizationId, id, purpose, turnId, questionId, await db.InterviewAnswers.CountAsync(x => x.InterviewSessionId == id, ct));
    }
}
