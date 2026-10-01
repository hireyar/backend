using HireYar.Application.Billing;
using HireYar.Application.Common;
namespace HireYar.Infrastructure.Billing;

public sealed class DisabledPaymentGateway : IPaymentGateway
{
    public string Provider => "Disabled";
    public bool IsEnabled => false;
    public Task<PaymentInitiationResult> CreatePaymentAsync(GatewayPayment payment, CancellationToken ct) =>
        throw new AppException(503, "payment_gateway_unavailable", "Payment gateway is not configured.");
    public Task<PaymentVerificationResult> VerifyPaymentAsync(GatewayPayment payment, string authority, CancellationToken ct) =>
        throw new AppException(503, "payment_gateway_unavailable", "Payment gateway is not configured.");
}
