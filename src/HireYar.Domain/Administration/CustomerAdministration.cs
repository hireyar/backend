namespace HireYar.Domain.Administration;

public sealed class CustomerAdministrationEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public string CustomerKind { get; set; } = "User";
    public Guid? OrganizationId { get; set; }
    public Guid ActorId { get; set; }
    public Guid RequestId { get; set; }
    public string Fingerprint { get; set; } = "";
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public string DetailsJson { get; set; } = "{}";
    public int CreditQuantity { get; set; }
    public string? BundleCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
// An explicit administrative replacement of coverage. Historical paid memberships stay immutable.
// Orders excluded here were already confirmed when the override was saved; later paid orders remain effective.
public sealed class CustomerSubscriptionOverride
{
    public Guid OrganizationId { get; set; }
    public Guid EventId { get; set; }
    public Guid[] ExcludedOrderIds { get; set; } = [];
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public bool IsStopped { get; set; }
    public int TeamUsers { get; set; }
    public bool UnlimitedTeam { get; set; }
    public int RetentionDays { get; set; }
}
