using HireYar.Domain.Interviews;

namespace HireYar.Application.Interviews;

public static class InterviewInvitationStatus
{
    public static InterviewAccessStatus Get(Guid? replacementId, DateTimeOffset? revokedAt, InterviewStatus status,
        DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (replacementId is not null) return new("changed", "interview_address_changed", "آدرس این مصاحبه تغییر کرده است. لطفاً از دعوت جدید استفاده کنید.");
        if (revokedAt is not null || status == InterviewStatus.Cancelled) return new("revoked", "interview_access_revoked", "دعوت این مصاحبه لغو شده است.");
        if (status == InterviewStatus.Completed) return new("completed", "interview_completed", "این مصاحبه به اتمام رسیده است.");
        if (expiresAt <= now) return new("expired", "interview_expired", "مهلت دسترسی به این مصاحبه به پایان رسیده است.");
        return new("available");
    }
}
