using System.Security.Cryptography;
using System.Text;
using HireYar.Domain.Hiring;
namespace HireYar.Application.Hiring;

internal static class InterviewRules
{
    // PostgreSQL timestamptz stores microseconds; return the same value before and after a reload.
    internal static DateTimeOffset Utc(DateTimeOffset value) => new(value.UtcTicks - value.UtcTicks % 10, TimeSpan.Zero);
    internal static DateTimeOffset Now(TimeProvider time) => Utc(time.GetUtcNow());
    internal const string ConsentVersion = "text-v1";
    internal const string ConsentText = "پاسخ‌های متنی شما برای بررسی این درخواست استخدام در اختیار تیم استخدام این سازمان قرار می‌گیرد. در این مرحله هیچ صدا یا تصویری ضبط نمی‌شود. با ثبت پاسخ‌های متنی برای این منظور موافقم.";
    internal static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    internal static SessionStatus Effective(InterviewSession s, DateTimeOffset now) => s.Status switch
    {
        SessionStatus.NotStarted when now >= s.ExpiresAtUtc => SessionStatus.Expired,
        SessionStatus.InProgress when now >= s.DeadlineAtUtc => SessionStatus.Expired,
        _ => s.Status
    };
    internal static AnswerDto Answer(InterviewAnswer a) => new(a.Id, a.SnapshotQuestionId, a.Text, a.Status, a.UpdatedAtUtc, a.SubmittedAtUtc, a.RowVersion);
    internal static SessionListDto List(InterviewSession s, DateTimeOffset now) => new(s.Id, s.JobApplicationId, Effective(s, now), s.CandidateName, s.JobTitle, s.InterviewTitle, s.AvailableFromUtc, s.ExpiresAtUtc, s.DeadlineAtUtc, s.RowVersion, new(s.Invitation.DeliveryStatus, s.Invitation.SendAttempts, s.Invitation.LastSentAtUtc, s.Invitation.UsedAtUtc, s.Invitation.RevokedAtUtc));
    internal static SnapshotQuestion? Current(InterviewSession s) => s.Questions.OrderBy(x => x.Order).FirstOrDefault(q => !s.Answers.Any(a => a.SnapshotQuestionId == q.Id && a.Status != AnswerStatus.Draft));
    internal static PublicSessionDto Public(InterviewSession s, DateTimeOffset now)
    {
        var status = Effective(s, now); var current = status == SessionStatus.InProgress ? Current(s) : null;
        return new(s.Id, s.OrganizationName, s.JobTitle, s.InterviewTitle, s.Language, s.DurationMinutes, s.AvailableFromUtc, s.ExpiresAtUtc, status, s.StartedAtUtc, s.DeadlineAtUtc, s.CompletedAtUtc, ConsentVersion, ConsentText, current is null ? null : new(current.Id, current.Order, current.Text), s.Answers.OrderBy(x => x.UpdatedAtUtc).Select(Answer).ToArray());
    }
}
