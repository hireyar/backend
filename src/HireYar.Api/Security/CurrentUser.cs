using System.Security.Claims;
using HireYar.Application.Common;
using HireYar.Domain.Organizations;
namespace HireYar.Api.Security;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public bool CanAccess(string section, bool write = false) => Role == OrganizationRole.Owner ||
        Principal.HasClaim("section_access", section + ":Write") || !write && Principal.HasClaim("section_access", section + ":Read");
    private ClaimsPrincipal Principal => accessor.HttpContext?.User ?? throw AppException.Unauthorized();
    public Guid UserId => Guid.TryParse(Principal.FindFirstValue("sub"), out var id) ? id : throw AppException.Unauthorized();
    public Guid OrganizationId => Guid.TryParse(Principal.FindFirstValue("org"), out var id) ? id : throw AppException.Unauthorized();
    public OrganizationRole Role => Enum.TryParse<OrganizationRole>(Principal.FindFirstValue("role"), out var role) ? role : throw AppException.Unauthorized();
}
