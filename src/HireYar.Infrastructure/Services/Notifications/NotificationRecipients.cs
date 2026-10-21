using HireYar.Application.Notifications;
using HireYar.Domain.Organizations;
using HireYar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Infrastructure.Notifications;

public sealed class NotificationRecipients(AppDbContext db) : INotificationRecipients
{
    public Task<List<Guid>> ActiveMembersAsync(Guid organizationId, CancellationToken ct) => db.Users.AsNoTracking()
        .Where(u => u.OrganizationId == organizationId && (u.Role == OrganizationRole.Owner || u.Role == OrganizationRole.Recruiter) && u.IsActive && u.PhoneNumberConfirmed)
        .Select(u => u.Id).ToListAsync(ct);
}
