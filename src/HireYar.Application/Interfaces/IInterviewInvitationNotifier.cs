namespace HireYar.Application.Interviews;

public sealed record InvitationNotification(string Email, string? Phone, string CandidateName,
    string PositionTitle, InterviewInvitation Invitation);
public sealed record InvitationDelivery(string EmailDelivery, string SmsDelivery);
public interface IInterviewInvitationNotifier
{
    Task<InvitationDelivery> SendAsync(InvitationNotification notification, bool sendEmail, bool sendSms, CancellationToken ct);
}
