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
public sealed record CurrentUserDto(Guid Id, string Email, string FullName, Guid OrganizationId, string OrganizationName, OrganizationRole Role);
// Internal boundary result: controllers must return AccessResponse, never this refresh secret.
public sealed record SessionResult(string AccessToken, DateTimeOffset AccessExpiresAtUtc, string RefreshToken, DateTimeOffset RefreshExpiresAtUtc);
public sealed record AccessResponse(string AccessToken, DateTimeOffset ExpiresAtUtc);
public sealed record RegistrationResult(Guid UserId, bool ConfirmationEmailSent);
public interface IAccounts
{
    Task<RegistrationResult> RegisterAsync(RegisterRequest r, CancellationToken ct);
    Task ConfirmAsync(TokenRequest r, CancellationToken ct);
    Task ResendAsync(EmailRequest r, CancellationToken ct);
    Task<SessionResult> LoginAsync(LoginRequest r, CancellationToken ct);
    Task<SessionResult> RefreshAsync(string token, CancellationToken ct);
    Task LogoutAsync(string token, CancellationToken ct);
    Task LogoutAllAsync(Guid userId, CancellationToken ct);
    Task ForgotAsync(EmailRequest r, CancellationToken ct);
    Task ResetAsync(ResetPasswordRequest r, CancellationToken ct);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest r, CancellationToken ct);
    Task<CurrentUserDto> GetAsync(Guid userId, CancellationToken ct);
    Task<CurrentUserDto> UpdateAsync(Guid userId, NameRequest r, CancellationToken ct);
}
public sealed class AuthService(IAccounts accounts, HireYar.Application.Common.ICurrentUser user)
{
    public Task<RegistrationResult> RegisterAsync(RegisterRequest r, CancellationToken ct) => accounts.RegisterAsync(r, ct);
    public Task ConfirmAsync(TokenRequest r, CancellationToken ct) => accounts.ConfirmAsync(r, ct);
    public Task ResendAsync(EmailRequest r, CancellationToken ct) => accounts.ResendAsync(r, ct);
    public Task<SessionResult> LoginAsync(LoginRequest r, CancellationToken ct) => accounts.LoginAsync(r, ct);
    public Task<SessionResult> RefreshAsync(string token, CancellationToken ct) => accounts.RefreshAsync(token, ct);
    public Task LogoutAsync(string token, CancellationToken ct) => accounts.LogoutAsync(token, ct);
    public Task LogoutAllAsync(CancellationToken ct) => accounts.LogoutAllAsync(user.UserId, ct);
    public Task ForgotAsync(EmailRequest r, CancellationToken ct) => accounts.ForgotAsync(r, ct);
    public Task ResetAsync(ResetPasswordRequest r, CancellationToken ct) => accounts.ResetAsync(r, ct);
    public Task ChangePasswordAsync(ChangePasswordRequest r, CancellationToken ct) => accounts.ChangePasswordAsync(user.UserId, r, ct);
    public Task<CurrentUserDto> GetAsync(CancellationToken ct) => accounts.GetAsync(user.UserId, ct);
    public Task<CurrentUserDto> UpdateAsync(NameRequest r, CancellationToken ct) => accounts.UpdateAsync(user.UserId, r, ct);
}

