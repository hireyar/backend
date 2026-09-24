using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;
using HireYar.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Organizations;

public sealed record OrganizationDto(Guid Id, string Name);
public sealed record OrganizationRequest([Required, StringLength(200)] string Name);
public sealed class OrganizationService(IApplicationDbContext db, ICurrentUser user, TimeProvider time)
{
    public async Task<OrganizationDto> GetAsync(CancellationToken ct) => await db.Organizations.AsNoTracking().Where(x => x.Id == user.OrganizationId).Select(x => new OrganizationDto(x.Id, x.Name)).SingleAsync(ct);
    public async Task<OrganizationDto> UpdateAsync(OrganizationRequest r, CancellationToken ct)
    {
        if (user.Role != OrganizationRole.Owner) throw new AppException(403, "forbidden", "Owner access required.");
        var org = await db.Organizations.SingleAsync(x => x.Id == user.OrganizationId, ct); org.Name = r.Name.Trim(); org.UpdatedAtUtc = time.GetUtcNow(); await db.SaveChangesAsync(ct); return new(org.Id, org.Name);
    }
}

