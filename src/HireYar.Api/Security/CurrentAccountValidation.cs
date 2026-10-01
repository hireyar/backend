using System.Security.Claims;
using HireYar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Api.Security;

public sealed class CurrentAccountValidation(AppDbContext db) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        if (!Guid.TryParse(context.Principal?.FindFirstValue("sub"), out var id)) { context.Fail("Invalid account."); return; }
        var u = await db.Users.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.IsActive, x.EmailConfirmed, x.PhoneNumberConfirmed, x.OrganizationId, x.Role, x.IsSystemAdmin }).SingleOrDefaultAsync(context.HttpContext.RequestAborted);
        if (u is not { IsActive: true } || !(u.EmailConfirmed || u.PhoneNumberConfirmed)) { context.Fail("Invalid account."); return; }
        var identity = (ClaimsIdentity)context.Principal!.Identity!;
        foreach (var claim in identity.FindAll("org").Concat(identity.FindAll("role")).Concat(identity.FindAll("system_admin")).ToArray()) identity.RemoveClaim(claim);
        identity.AddClaim(new("org", u.OrganizationId.ToString())); identity.AddClaim(new("role", u.Role.ToString()));
        if (u.IsSystemAdmin) identity.AddClaim(new("system_admin", "true"));
    }
}
