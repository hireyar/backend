using HireYar.Application.Common;
using HireYar.Application.Subscriptions;
using HireYar.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Application.Notifications;

public sealed class NotificationReminderService(IApplicationDbContext db, SubscriptionService subscriptions,
    INotificationRecipients recipients, NotificationService notifications, TimeProvider time)
{
    public async Task GenerateAsync(Guid organizationId, CancellationToken ct)
    {
        var added = await db.WithOrganizationLockAsync(organizationId, async () =>
        {
            var state = await subscriptions.StateAsync(organizationId, ct);
            var recipient = await recipients.OwnerAsync(organizationId, ct);
            var result = new List<Notification>();
            if (recipient is null) return result;
            var openLow = db.Notifications.Where(n => n.OrganizationId == organizationId &&
                n.RecipientUserId == recipient.Value && n.Type == "InterviewCreditsLow" && n.ResolvedAtUtc == null);
            if (state.AvailableInterviews >= 2)
            {
                await openLow.ExecuteUpdateAsync(s => s.SetProperty(n => n.ResolvedAtUtc, time.GetUtcNow()), ct);
            }

            if (state.ExpiresAtUtc is { } expiry)
            {
                var days = (expiry - time.GetUtcNow()).TotalDays;
                var threshold = days > 0 && days <= 2 ? 2 : days > 2 && days <= 5 ? 5 : 0;
                if (threshold != 0)
                {
                    var n = await notifications.AddAsync(organizationId, recipient.Value, "SubscriptionExpiring",
                        $"{expiry.ToUnixTimeSeconds()}:{threshold}", $"کمتر از {threshold} روز تا پایان اشتراک هایریار باقی مانده است",
                        "Subscription", null, ct);
                    if (n is not null) result.Add(n);
                }
            }
            if (state.AvailableInterviews < 2 && !await openLow.AnyAsync(ct))
            {
                var n = await notifications.AddAsync(organizationId, recipient.Value, "InterviewCreditsLow",
                    Guid.NewGuid().ToString("N"), "اعتبار قابل استفاده مصاحبه کمتر از ۲ است", "InterviewCredits", null, ct);
                if (n is not null) result.Add(n);
            }
            return result;
        }, ct);
        if (added.Count > 0) await notifications.PublishAsync(organizationId, added[0].RecipientUserId);
    }
}
