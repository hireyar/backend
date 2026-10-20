using HireYar.Application.Common;
namespace HireYar.Application.Administration;

public interface IAdminCustomers
{
    Task<AdminCustomerPage> ListAsync(AdminCustomerQuery query, CancellationToken ct);
    Task<AdminCustomerDetail> GetAsync(Guid id, CancellationToken ct);
    Task<Guid> OrganizationAsync(Guid id, CancellationToken ct);
    Task<AdminCustomerMutationDto> ProfileAsync(Guid actor, Guid id, AdminCustomerProfileRequest request, CancellationToken ct);
    Task<AdminCustomerMutationDto> SellerAsync(Guid actor, Guid id, AdminCustomerSellerRequest request, CancellationToken ct);
    Task<Page<AdminCustomerHistoryDto>> HistoryAsync(Guid id, PageQuery page, CancellationToken ct);
    Task<byte[]> ExportAsync(AdminCustomerQuery query, CancellationToken ct);
}
