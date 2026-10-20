using HireYar.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
namespace HireYar.Api.Security;
public sealed class SalesEntryFilter(IOptions<SalesAdminOptions> options) : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var entry=context.HttpContext.Request.Headers["X-Sales-Entry"];
        if(entry.Count!=1 || !options.Value.MatchesEntry(entry[0] ?? "")) context.Result=new NotFoundResult();
        return Task.CompletedTask;
    }
}
