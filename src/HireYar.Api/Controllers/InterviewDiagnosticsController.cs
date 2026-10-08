using System.Diagnostics;
using System.Text.Json;
using HireYar.Application.Common;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using HireYar.Domain.Organizations;
using HireYar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Api.Controllers;

[ApiController, Authorize(Roles = "Owner")]
public sealed class InterviewDiagnosticsController : ControllerBase
{
    [HttpGet("api/interviews/{id:guid}/diagnostics"), Produces("text/event-stream")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task Stream(Guid id, CancellationToken ct) => InterviewDiagnosticStream.SendAsync(HttpContext, id, true, ct);
}

internal static class InterviewDiagnosticStream
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static async Task SendAsync(HttpContext context, Guid id, bool owner, CancellationToken ct)
    {
        var diagnostics = context.RequestServices.GetRequiredService<IInterviewDiagnostics>();
        if (!diagnostics.Enabled) throw AppException.NotFound();
        var db = context.RequestServices.GetRequiredService<AppDbContext>();
        var time = context.RequestServices.GetRequiredService<TimeProvider>();
        var user = owner ? context.RequestServices.GetRequiredService<ICurrentUser>() : null;
        var organizationId = owner ? user!.OrganizationId : Guid.Parse(context.User.FindFirst("interview_organization")!.Value);
        async Task<bool> Accessible()
        {
            var session = await db.InterviewSessions.IgnoreAutoIncludes().AsNoTracking()
                .Where(x => x.Id == id && x.OrganizationId == organizationId && x.DeletedAtUtc == null)
                .Select(x => new { x.Status, x.AccessRevokedAtUtc, x.SupersededByInterviewId, x.ExpiresAtUtc }).SingleOrDefaultAsync(ct);
            if (session is null) return false;
            if (!owner) return session.AccessRevokedAtUtc is null && session.SupersededByInterviewId is null && session.ExpiresAtUtc > time.GetUtcNow() &&
                session.Status is not (InterviewStatus.Completed or InterviewStatus.Cancelled);
            if (!long.TryParse(context.User.FindFirst("exp")?.Value, out var expiry) || time.GetUtcNow() >= DateTimeOffset.FromUnixTimeSeconds(expiry)) return false;
            return await db.Users.AsNoTracking().AnyAsync(x => x.Id == user!.UserId && x.OrganizationId == organizationId && x.IsActive && x.PhoneNumberConfirmed && x.Role == OrganizationRole.Owner, ct);
        }
        if (!await Accessible()) throw AppException.NotFound();
        using var subscription = diagnostics.Subscribe(organizationId, id);
        var cursor = context.Request.Headers["Last-Event-ID"].FirstOrDefault() ?? context.Request.Query["after"].FirstOrDefault();
        if (cursor?.Length > 100) throw AppException.Invalid("invalid_diagnostic_cursor");
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Accel-Buffering"] = "no";
        context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        await context.Response.WriteAsync(": connected\n\n", ct);
        await context.Response.Body.FlushAsync(ct);
        var watch = Stopwatch.StartNew(); var checkedAt = watch.Elapsed; var heartbeatAt = watch.Elapsed;
        try
        {
            while (watch.Elapsed < TimeSpan.FromSeconds(30))
            {
                if (watch.Elapsed - checkedAt >= TimeSpan.FromSeconds(1))
                {
                    checkedAt = watch.Elapsed;
                    if (!await Accessible()) { await Write("closed", new { reason = "access_unavailable" }); return; }
                }
                var batch = diagnostics.Read(organizationId, id, cursor, owner);
                if (batch.Reset) await Write("reset", new { reason = "buffer_expired_or_restarted", cursor = batch.Cursor });
                foreach (var diagnostic in batch.Events)
                {
                    await context.Response.WriteAsync($"id: {diagnostic.Id}\n", ct);
                    await Write("diagnostic", diagnostic);
                }
                cursor = batch.Cursor;
                if (watch.Elapsed - heartbeatAt >= TimeSpan.FromSeconds(10))
                { await context.Response.WriteAsync(": heartbeat\n\n", ct); heartbeatAt = watch.Elapsed; }
                await context.Response.Body.FlushAsync(ct);
                await Task.Delay(250, ct);
            }
            await Write("reconnect", new { cursor });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        async Task Write(string name, object data)
        {
            await context.Response.WriteAsync($"event: {name}\ndata: {JsonSerializer.Serialize(data, Json)}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
        }
    }
}
