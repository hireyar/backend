namespace HireYar.Application.Notifications;

public interface INotificationRecipients
{
    Task<Guid?> OwnerAsync(Guid organizationId, CancellationToken ct);
}
public interface INotificationPublisher
{
    // Invalidation only; clients reload authorized data through REST.
    Task ChangedAsync(Guid organizationId, Guid recipientUserId, CancellationToken ct);
}
