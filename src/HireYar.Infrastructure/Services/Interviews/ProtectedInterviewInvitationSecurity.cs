using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HireYar.Application.Interviews;
using HireYar.Infrastructure.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace HireYar.Infrastructure.Interviews;

public sealed class InterviewInvitationOptions
{
    public string CandidateLinkPath { get; set; } = "/interview/{id}";
}

public sealed class ProtectedInterviewInvitationSecurity(IDataProtectionProvider protection, IOptions<SecurityOptions> security,
    IOptions<InterviewInvitationOptions> options) : IInterviewInvitationSecurity
{
    private IDataProtector Protector(Guid id) => protection.CreateProtector("HireYar.InterviewAccessCode.v1", id.ToString("N"));
    public string CreateCode(string? previous = null)
    {
        string code;
        do { code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture); } while (code == previous);
        return code;
    }
    public string Protect(Guid interviewId, string code) => Protector(interviewId).Protect(code);
    public string? Unprotect(Guid interviewId, string? protectedCode)
    {
        if (protectedCode is null) return null;
        try { return Protector(interviewId).Unprotect(protectedCode); }
        catch (CryptographicException) { return null; }
    }
    public bool Matches(Guid interviewId, string code, string protectedCode)
    {
        var expected = Unprotect(interviewId, protectedCode);
        return code.Length == 6 && code.All(char.IsAsciiDigit) && expected?.Length == 6 &&
            CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(code), Encoding.ASCII.GetBytes(expected));
    }
    public string Link(Guid interviewId) => security.Value.FrontendUrl.TrimEnd('/') + options.Value.CandidateLinkPath.Replace("{id}", interviewId.ToString());
}
