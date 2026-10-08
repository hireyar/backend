using HireYar.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Application.Interviews;

public sealed class InterviewAccessStatusService(IApplicationDbContext db, TimeProvider time)
{
    public async Task<InterviewAccessStatus> GetAsync(Guid id, CancellationToken ct)
    {
        var s = await db.InterviewSessions.AsNoTracking().Where(s => s.Id == id && s.DeletedAtUtc == null)
            .Select(s => new { s.SupersededByInterviewId, s.AccessRevokedAtUtc, s.Status, s.ExpiresAtUtc }).SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound();
        return InterviewInvitationStatus.Get(s.SupersededByInterviewId, s.AccessRevokedAtUtc, s.Status, s.ExpiresAtUtc, time.GetUtcNow());
    }
}
