using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace HireYar.Api.Security;

public sealed class CsrfFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method)) return;
        try { await antiforgery.ValidateRequestAsync(context.HttpContext); }
        catch (AntiforgeryValidationException)
        {
            var detail = new ProblemDetails { Status = 400, Title = "CSRF validation failed." };
            detail.Extensions["code"] = "invalid_csrf"; detail.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            context.Result = new BadRequestObjectResult(detail);
        }
    }
}
