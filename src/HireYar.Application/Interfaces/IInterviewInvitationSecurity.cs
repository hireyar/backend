namespace HireYar.Application.Interviews;

public interface IInterviewInvitationSecurity
{
    string CreateCode(string? previous = null);
    string Protect(Guid interviewId, string code);
    string? Unprotect(Guid interviewId, string? protectedCode);
    bool Matches(Guid interviewId, string code, string protectedCode);
    string Link(Guid interviewId);
}
