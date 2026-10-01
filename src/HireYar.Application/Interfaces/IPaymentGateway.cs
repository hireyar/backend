namespace HireYar.Application.Billing;

public sealed record GatewayPayment(Guid PaymentId, decimal Amount, string Currency);
public sealed record PaymentInitiationResult(string Authority, string RedirectUrl);
public sealed record PaymentVerificationResult(bool IsPaid, string? TransactionId, decimal Amount, string Currency);
public interface IPaymentGateway
{
    string Provider { get; }
    bool IsEnabled { get; }
    Task<PaymentInitiationResult> CreatePaymentAsync(GatewayPayment payment, CancellationToken ct);
    Task<PaymentVerificationResult> VerifyPaymentAsync(GatewayPayment payment, string authority, CancellationToken ct);
}
public interface ISubscriptionEntitlementService
{
    Task<SubscriptionDto?> CurrentAsync(Guid organizationId, CancellationToken ct);
    Task<SubscriptionDto> RequireAsync(Guid organizationId, IEnumerable<string> capabilities, CancellationToken ct);
}
