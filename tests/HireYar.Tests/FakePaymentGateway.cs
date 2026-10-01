using System.Collections.Concurrent;
using HireYar.Application.Billing;
namespace HireYar.Tests;

internal static class BillingJson
{
    public static System.Text.Json.JsonSerializerOptions Options { get; } = Create();
    private static System.Text.Json.JsonSerializerOptions Create()
    {
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false)); return options;
    }
}

// Only registered by the test fixture. Runtime environments always use Disabled until a real adapter is installed.
public sealed class FakePaymentGateway : IPaymentGateway
{
    public string Provider => "SyntheticTest";
    public bool IsEnabled => true;
    public ConcurrentDictionary<Guid, PaymentVerificationResult> Results { get; } = new();
    public Task<PaymentInitiationResult> CreatePaymentAsync(GatewayPayment p, CancellationToken ct) => Task.FromResult(new PaymentInitiationResult("test-authority-" + p.PaymentId, "https://gateway.example.test/checkout"));
    public Task<PaymentVerificationResult> VerifyPaymentAsync(GatewayPayment p, string authority, CancellationToken ct) =>
        Task.FromResult(Results.GetValueOrDefault(p.PaymentId, new(true, "test-transaction-" + p.PaymentId, p.Amount, p.Currency)));
}
