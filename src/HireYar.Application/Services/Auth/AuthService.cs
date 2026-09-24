using System.ComponentModel.DataAnnotations;
using HireYar.Domain.Organizations;
namespace HireYar.Application.Auth;

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
