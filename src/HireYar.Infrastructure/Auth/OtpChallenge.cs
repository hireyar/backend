namespace HireYar.Infrastructure.Auth;

public sealed class OtpChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Mobile { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
    public bool Delivered { get; set; }
    public int FailedAttempts { get; set; }
}
