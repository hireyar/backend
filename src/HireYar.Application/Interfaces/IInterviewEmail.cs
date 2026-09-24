namespace HireYar.Application.Hiring;

public sealed record InterviewEmail(string Recipient, string OrganizationName, string JobTitle, int DurationMinutes, DateTimeOffset AvailableFromUtc, DateTimeOffset ExpiresAtUtc, string Token);
public interface IInterviewEmail
{
    Task SendAsync(InterviewEmail message, CancellationToken ct);
}
