using System.Security.Claims;
using HireYar.Application.Auth;
using HireYar.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
namespace HireYar.Api.Security;

public sealed class AdminAccountValidation(IAdminAccounts accounts, IOptions<SuperAdminOptions> options) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        if (!options.Value.Enabled || context.Principal?.FindFirstValue("admin_entry") != options.Value.EntryFingerprint ||
            !Guid.TryParse(context.Principal?.FindFirstValue("sub"), out var id) ||
            await accounts.ValidateAsync(id, context.Principal.FindFirstValue("admin_stamp") ?? "", context.HttpContext.RequestAborted) is null)
            context.Fail("Invalid administrator session.");
    }
}
