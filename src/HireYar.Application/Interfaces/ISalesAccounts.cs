using HireYar.Application.Common;
namespace HireYar.Application.Sales;
public interface ISalesAccounts
{
    Task<SalesSessionDto> LoginAsync(SalesLoginRequest request, CancellationToken ct);
    Task<SalesAgentDto?> ValidateAsync(Guid id, Guid stamp, CancellationToken ct);
    Task LogoutAsync(Guid id, CancellationToken ct);
    Task<Page<SalesAgentDto>> ListAsync(PageQuery page, CancellationToken ct);
    Task<SalesCredentialsDto> CreateAsync(Guid actor, SalesAgentRequest request, CancellationToken ct);
    Task<SalesAgentDto> UpdateAsync(Guid actor, Guid id, SalesAgentRequest request, CancellationToken ct);
    Task<SalesCredentialsDto> ResetPasswordAsync(Guid actor, Guid id, CancellationToken ct);
}
public interface ISalesLeads
{
    Task<SalesLeadDto> CreateAsync(Guid agent, SalesLeadRequest request, CancellationToken ct);
}
