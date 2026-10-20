namespace HireYar.Domain.Sales;

// Platform staff; independent of tenant users and administrator privileges.
public sealed class SalesAgent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = "";
    public string Mobile { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();
    public bool IsActive { get; set; } = true;
    public int FailedAttempts { get; set; }
    public DateTimeOffset? LockoutEndUtc { get; set; }
    public decimal FirstSalePercent { get; set; } = 20;
    public decimal RenewalPercent { get; set; } = 5;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
public sealed class SalesLead
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? AgentId { get; set; }
    public string Mobile { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string Note { get; set; } = "";
    public string ContactOrganizationName { get; set; } = "";
    public string? ContactEmail { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset LastContactAtUtc { get; set; }
    public DateTimeOffset? FollowUpAtUtc { get; set; }
    public Guid? OrganizationId { get; set; }
    public DateTimeOffset? RegisteredAtUtc { get; set; }
}
public sealed class SalesCommission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AgentId { get; set; }
    public Guid LeadId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid OrderId { get; set; }
    public string Kind { get; set; } = "FirstSale";
    public long InvoiceAmountRial { get; set; }
    public decimal Percent { get; set; }
    public long AmountRial { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset AvailableAtUtc { get; set; }
    public Guid? PayoutId { get; set; }
}
public sealed class SalesPayout
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AgentId { get; set; }
    public Guid RequestId { get; set; }
    public string Fingerprint { get; set; } = "";
    public long AmountRial { get; set; }
    public string Status { get; set; } = "Requested";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public Guid? ReviewedById { get; set; }
    public string? ReviewNote { get; set; }
}
public sealed class SalesAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActorId { get; set; }
    public Guid? LeadId { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
