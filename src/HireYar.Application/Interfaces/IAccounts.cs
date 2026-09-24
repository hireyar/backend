using System.ComponentModel.DataAnnotations;
using HireYar.Domain.Organizations;
namespace HireYar.Application.Auth;

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
