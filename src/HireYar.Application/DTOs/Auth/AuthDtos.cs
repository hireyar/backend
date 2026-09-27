using System.ComponentModel.DataAnnotations;
using HireYar.Domain.Organizations;
namespace HireYar.Application.Auth;

public sealed record RegisterRequest([Required, StringLength(200)] string FullName,
    [Required, StringLength(200)] string OrganizationName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128, MinimumLength = 12)] string Password);
public sealed record LoginRequest([Required, EmailAddress, StringLength(256)] string Email, [Required, StringLength(128)] string Password);
public sealed record EmailRequest([Required, EmailAddress, StringLength(256)] string Email);
public sealed record TokenRequest(Guid UserId, [Required, StringLength(4096)] string Token);
public sealed record ResetPasswordRequest(Guid UserId, [Required, StringLength(4096)] string Token, [Required, StringLength(128, MinimumLength = 12)] string Password);
public sealed record ChangePasswordRequest([Required, StringLength(128)] string CurrentPassword, [Required, StringLength(128, MinimumLength = 12)] string NewPassword);
public sealed record NameRequest([Required, StringLength(200)] string FullName);
public sealed record CurrentUserDto(Guid Id, string? Email, string? Mobile, string FullName, Guid OrganizationId, string OrganizationName, OrganizationRole Role);
// Internal boundary result: controllers must return AccessResponse, never this refresh secret.
public sealed record SessionResult(string AccessToken, DateTimeOffset AccessExpiresAtUtc, string RefreshToken, DateTimeOffset RefreshExpiresAtUtc);
public sealed record AccessResponse(string AccessToken, DateTimeOffset ExpiresAtUtc);
public sealed record RegistrationResult(Guid UserId, bool ConfirmationEmailSent);
public sealed record RequestOtpRequest([Required, RegularExpression(@"^09\d{9}$")] string Mobile);
public sealed record VerifyOtpRequest([Required, RegularExpression(@"^09\d{9}$")] string Mobile,
    [Required, RegularExpression(@"^\d{6}$")] string Code);
public sealed record OtpRequestResult(DateTimeOffset ExpiresAtUtc);
