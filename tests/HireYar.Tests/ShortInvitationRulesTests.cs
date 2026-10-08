using HireYar.Application.Interviews;
using HireYar.Infrastructure.Auth;
using HireYar.Infrastructure.Interviews;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace HireYar.Tests;

public sealed class ShortInvitationRulesTests
{
    private static ProtectedInterviewInvitationSecurity Security() => new(new EphemeralDataProtectionProvider(),
        Options.Create(new SecurityOptions { FrontendUrl = "https://frontend.example.test" }), Options.Create(new InterviewInvitationOptions()));

    [Fact]
    public void Six_digit_codes_keep_leading_zero_are_protected_and_bound_to_one_interview()
    {
        var service = Security(); var id = Guid.NewGuid(); var encrypted = service.Protect(id, "001234");
        Assert.NotEqual("001234", encrypted); Assert.Equal("001234", service.Unprotect(id, encrypted));
        Assert.True(service.Matches(id, "001234", encrypted)); Assert.False(service.Matches(id, "1234", encrypted));
        Assert.False(service.Matches(id, "۰۰۱۲۳۴", encrypted)); Assert.False(service.Matches(Guid.NewGuid(), "001234", encrypted));
        Assert.Null(service.Unprotect(id, "broken")); Assert.Null(service.Unprotect(id, null));
        Assert.NotEqual(InterviewTokens.Hash("001234"), InterviewTokens.Hash(encrypted));
        Assert.Equal($"https://frontend.example.test/interview/{id}", service.Link(id));
        Assert.DoesNotContain("001234", service.Link(id));
        for (var i = 0; i < 100; i++)
        {
            var code = service.CreateCode("001234"); Assert.Matches("^[0-9]{6}$", code); Assert.NotEqual("001234", code);
        }
    }
}
