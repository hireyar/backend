using HireYar.Domain.Organizations;
using HireYar.Domain.JobPositions;
using HireYar.Domain.InterviewTemplates;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Common;

public interface ICurrentUser
{
    Guid UserId { get; }
    Guid OrganizationId { get; }
    OrganizationRole Role { get; }
    bool CanAccess(string section, bool write = false) => Role == OrganizationRole.Owner;
}
