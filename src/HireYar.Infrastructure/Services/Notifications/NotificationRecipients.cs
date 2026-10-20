using HireYar.Application.Notifications;
using HireYar.Domain.Organizations;
using HireYar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Infrastructure.Notifications;

public sealed class NotificationRecipients(AppDbContext db) : INotificationRecipients
{
    public Task<Guid?> OwnerAsync(Guid organizationId, CancellationToken ct) => db.Users.AsNoTracking()
        .Where(u => u.OrganizationId == organizationId && u.Role == OrganizationRole.Owner && u.IsActive && u.PhoneNumberConfirmed)
        .Select(u => (Guid?)u.Id).SingleOrDefaultAsync(ct);
}
