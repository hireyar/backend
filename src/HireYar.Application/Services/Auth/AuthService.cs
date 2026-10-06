using System.ComponentModel.DataAnnotations;
using HireYar.Domain.Organizations;
namespace HireYar.Application.Auth;

public sealed class AuthService(IAccounts accounts, HireYar.Application.Common.ICurrentUser user)
{
    public Task<OtpRequestResult> RequestOtpAsync(RequestOtpRequest r, CancellationToken ct) => accounts.RequestOtpAsync(r, ct);
    public Task<SessionResult> VerifyOtpAsync(VerifyOtpRequest r, CancellationToken ct) => accounts.VerifyOtpAsync(r, ct);
    public Task<SessionResult> RefreshAsync(string token, CancellationToken ct) => accounts.RefreshAsync(token, ct);
    public Task LogoutAsync(string token, CancellationToken ct) => accounts.LogoutAsync(token, ct);
    public Task LogoutAllAsync(CancellationToken ct) => accounts.LogoutAllAsync(user.UserId, ct);
    public Task<CurrentUserDto> GetAsync(CancellationToken ct) => accounts.GetAsync(user.UserId, ct);
    public Task<CurrentUserDto> UpdateAsync(NameRequest r, CancellationToken ct) => accounts.UpdateAsync(user.UserId, r, ct);
}
