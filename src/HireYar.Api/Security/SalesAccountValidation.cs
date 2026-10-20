using System.Security.Claims;
using HireYar.Application.Sales;
using Microsoft.AspNetCore.Authentication.JwtBearer;
namespace HireYar.Api.Security;
public sealed class SalesAccountValidation(ISalesAccounts accounts) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        if(!Guid.TryParse(context.Principal?.FindFirstValue("sub"),out var id) ||
           !Guid.TryParse(context.Principal?.FindFirstValue("sales_stamp"),out var stamp) ||
           await accounts.ValidateAsync(id,stamp,context.HttpContext.RequestAborted) is null) context.Fail("Invalid sales session.");
    }
}
