namespace HireYar.Application.Auth;

public interface IAdminAccounts
{
    Task<AdminSessionDto> LoginAsync(AdminLoginRequest request, CancellationToken ct);
    Task<AdminIdentityDto?> ValidateAsync(Guid userId, string securityStamp, CancellationToken ct);
    Task RevokeSessionsAsync(Guid userId, CancellationToken ct);
    Task<AdminGrantDto> GrantAsync(AdminGrantRequest request, CancellationToken ct);
    Task<HireYar.Application.Common.Page<AdminUserDto>> ListAsync(HireYar.Application.Common.PageQuery pagination, string? search, CancellationToken ct);
    Task RevokeAdminAsync(Guid actorId, Guid userId, CancellationToken ct);
}

public interface IAdminCaptcha
{
    AdminCaptchaDto Create();
    bool Consume(Guid id, string answer);
}
