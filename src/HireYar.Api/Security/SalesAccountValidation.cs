using System.Security.Claims;
using HireYar.Application.Sales;
using HireYar.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
namespace HireYar.Api.Security;
public sealed class SalesAccountValidation(ISalesAccounts accounts, IOptions<SalesAdminOptions> options) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        if(!options.Value.Enabled || context.Principal?.FindFirstValue("sales_entry")!=options.Value.EntryFingerprint || !Guid.TryParse(context.Principal?.FindFirstValue("sub"),out var id) ||
           !Guid.TryParse(context.Principal?.FindFirstValue("sales_stamp"),out var stamp) ||
           await accounts.ValidateAsync(id,stamp,context.HttpContext.RequestAborted) is null) context.Fail("Invalid sales session.");
    }
}
