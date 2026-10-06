using System.ComponentModel.DataAnnotations;
using HireYar.Domain.Organizations;
namespace HireYar.Application.Auth;

public interface IAccounts
{
    Task<OtpRequestResult> RequestOtpAsync(RequestOtpRequest r, CancellationToken ct);
    Task<SessionResult> VerifyOtpAsync(VerifyOtpRequest r, CancellationToken ct);
    Task<SessionResult> RefreshAsync(string token, CancellationToken ct);
    Task LogoutAsync(string token, CancellationToken ct);
    Task LogoutAllAsync(Guid userId, CancellationToken ct);
    Task<CurrentUserDto> GetAsync(Guid userId, CancellationToken ct);
    Task<CurrentUserDto> UpdateAsync(Guid userId, NameRequest r, CancellationToken ct);
}
