using System.ComponentModel.DataAnnotations;
namespace HireYar.Application.Auth;

public sealed record AdminLoginRequest(
    [Required, RegularExpression("^09[0-9]{9}$")] string Mobile,
    [Required, StringLength(128, MinimumLength = 12)] string Password,
    Guid CaptchaId,
    [Required, RegularExpression("^[0-9]{6}$")] string CaptchaAnswer);
public sealed record AdminCaptchaDto(Guid Id, string ImageDataUrl, DateTimeOffset ExpiresAtUtc);
public sealed record AdminSessionDto(string AccessToken, DateTimeOffset ExpiresAtUtc);
public sealed record AdminIdentityDto(Guid Id, string FullName);
public sealed record AdminGrantRequest([Required, RegularExpression("^09[0-9]{9}$")] string Mobile);
public sealed record AdminGrantDto(Guid Id, string FullName, string Mobile, string Password);
public sealed record AdminUserDto(Guid Id, string FullName, string? Mobile, bool IsActive, bool PhoneNumberConfirmed, DateTimeOffset? LockoutEnd);
