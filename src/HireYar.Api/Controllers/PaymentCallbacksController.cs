using HireYar.Application.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace HireYar.Api.Controllers;

[ApiController, Route("api/payment-callbacks"), AllowAnonymous, EnableRateLimiting("auth")]
public sealed class PaymentCallbacksController(PaymentService payments) : ControllerBase
{
    // A callback is only a wake-up signal. The persisted authority is verified directly with the provider.
    [HttpPost("{paymentId:guid}")]
    public async Task<IActionResult> Verify(Guid paymentId, CancellationToken ct)
    { await payments.VerifyCallbackAsync(paymentId, ct); return NoContent(); }
}
