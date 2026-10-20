using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Common;
using HireYar.Application.Interviews;
using HireYar.Application.Subscriptions;
using HireYar.Infrastructure.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using HireYar.Domain.Interviews;

namespace HireYar.Tests;

public sealed partial class CountBasedSubscriptionTests
{
    private async Task<InterviewUsageDetailDto> Usage(Account owner, Guid id) =>
        await Read<InterviewUsageDetailDto>(await owner.Client.Send(HttpMethod.Get, $"/api/subscriptions/usage/{id}"));

    [PostgresFact]
    public async Task Usage_history_keeps_lifecycle_consumption_and_deleted_interviews_with_scoped_read_only_api()
    {
        var owner = await Owner(); await Subscribe(owner); await BuyBundle(owner);
        var other = await Owner(); var (job, template) = await Setup(owner);
        var first = await Issue(owner, job.Id, template.Id);
        var second = await Issue(owner, job.Id, template.Id);
        var issued = await Usage(owner, first.InterviewId);
        Assert.Equal("Created", issued.Interview.Status); Assert.Equal(1800, issued.Interview.ConfiguredDurationSeconds);
        Assert.True(issued.Interview.IsReserved); Assert.Equal(0, issued.Interview.CreditsConsumed);
        Assert.Equal(new[] { "Created", "Reserved" }, issued.Events.Select(e => e.EventType).Order().ToArray());
        using var candidate = Candidate(first);
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{first.InterviewId}/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{first.InterviewId}/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Post, $"/api/interviews/{first.InterviewId}/cancel")).StatusCode);
        var cancelled = await Usage(owner, first.InterviewId);
        Assert.Equal("Cancelled", cancelled.Interview.Status); Assert.Equal(1, cancelled.Interview.CreditsConsumed);
        Assert.NotNull(cancelled.Interview.StartedAtUtc); Assert.NotNull(cancelled.Interview.EndedAtUtc);
        Assert.True(cancelled.Interview.ActualDurationSeconds >= 0);
        Assert.Single(cancelled.Events, e => e.EventType == "CreditConsumed");
        Assert.Single(cancelled.Events, e => e.EventType == "Started");
        Assert.Equal(1, cancelled.Events.Sum(e => e.CreditsConsumed));
        Assert.DoesNotContain(cancelled.Events, e => e.EventType == "Released");
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Post, $"/api/interviews/{second.InterviewId}/cancel")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Delete, $"/api/interviews/{second.InterviewId}")).StatusCode);
        var deleted = await Usage(owner, second.InterviewId);
        Assert.Equal("Deleted", deleted.Interview.Status); Assert.Equal(0, deleted.Interview.CreditsConsumed);
        Assert.Contains(deleted.Events, e => e.EventType == "Cancelled"); Assert.Contains(deleted.Events, e => e.EventType == "Deleted");
        Assert.Contains(deleted.Events, e => e.EventType == "Released"); Assert.All(deleted.Events, e => Assert.False(e.IsHistorical));
        var response = await owner.Client.Send(HttpMethod.Get, "/api/subscriptions/usage?page=1&pageSize=1&status=Deleted");
        var page = await Read<Page<InterviewUsageDto>>(response);
        Assert.True(response.Headers.CacheControl?.NoStore); Assert.Equal(1, page.Total); Assert.Single(page.Items);
        Assert.Equal(second.InterviewId, page.Items[0].InterviewId);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.Send(HttpMethod.Get, $"/api/subscriptions/usage/{first.InterviewId}")).StatusCode);
        Assert.Empty((await Read<Page<InterviewUsageDto>>(await other.Client.Send(HttpMethod.Get, $"/api/subscriptions/usage?organizationId={owner.Id}"))).Items);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().GetAsync("/api/subscriptions/usage")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await candidate.GetAsync("/api/subscriptions/usage")).StatusCode);
        foreach (var query in new[] { "page=0", "pageSize=101", "status=Unknown", "fromUtc=2026-10-21T00:00:00Z&toUtc=2026-10-20T00:00:00Z" })
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.Send(HttpMethod.Get, "/api/subscriptions/usage?" + query)).StatusCode);
        await f.Db(async db => {
            var count = await db.InterviewUsageEvents.CountAsync();
            await Usage(owner, first.InterviewId);
            Assert.Equal(count, await db.InterviewUsageEvents.CountAsync());
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM \"InterviewUsageEvents\" WHERE \"InterviewSessionId\" = {first.InterviewId}"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"InterviewUsageEvents\" SET \"IsHistorical\" = true WHERE \"InterviewSessionId\" = {first.InterviewId}"));
            var otherOrg = (await db.Users.SingleAsync(u => u.Id == other.Id)).OrganizationId;
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"InterviewUsageEvents\" (\"Id\",\"OrganizationId\",\"InterviewSessionId\",\"EventType\",\"OccurredAtUtc\",\"IsHistorical\") VALUES ({Guid.NewGuid()},{otherOrg},{first.InterviewId},'Completed',{f.Clock.GetUtcNow()},false)"));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        });
    }

    [PostgresFact]
    public async Task Usage_expiration_is_persisted_without_page_reads_and_resend_preserves_both_invites()
    {
        var owner = await Owner(); await Subscribe(owner); await BuyBundle(owner); var (job, template) = await Setup(owner);
        var old = await Issue(owner, job.Id, template.Id);
        var replacement = await Read<PanelInvitationResult>(await owner.Client.Send(HttpMethod.Post,
            $"/api/interviews/{old.InterviewId}/resend", new ResendInterviewRequest()));
        var replaced = await Usage(owner, old.InterviewId);
        Assert.Equal("Replaced", replaced.Interview.Status);
        Assert.Equal(replacement.Invitation.InterviewId, replaced.Interview.ReplacementInterviewId);
        Assert.Contains(replaced.Events, e => e.EventType == "Replaced");
        var due = await Issue(owner, job.Id, template.Id);
        var lateCancellation = await Issue(owner, job.Id, template.Id);
        f.Clock.Offset = TimeSpan.FromDays(4);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Post, $"/api/interviews/{lateCancellation.InterviewId}/cancel")).StatusCode);
        var late = await Usage(owner, lateCancellation.InterviewId);
        Assert.Contains(late.Events, e => e.EventType == "Expired");
        Assert.Contains(late.Events, e => e.EventType == "Cancelled");
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var worker = scope.ServiceProvider.GetRequiredService<InterviewUsageExpiration>();
            await worker.RunAsync(default); await worker.RunAsync(default);
        }
        var expired = await Usage(owner, due.InterviewId);
        Assert.Equal("Expired", expired.Interview.Status); Assert.False(expired.Interview.IsReserved);
        Assert.Equal(0, expired.Interview.CreditsConsumed); Assert.Null(expired.Interview.StartedAtUtc);
        Assert.Null(expired.Interview.EndedAtUtc); Assert.Null(expired.Interview.ActualDurationSeconds);
        Assert.Equal(expired.Interview.ExpiresAtUtc, Assert.Single(expired.Events, e => e.EventType == "Expired").OccurredAtUtc);
        Assert.DoesNotContain((await Usage(owner, old.InterviewId)).Events, e => e.EventType == "Expired");
        var page = await Read<Page<InterviewUsageDto>>(await owner.Client.Send(HttpMethod.Get, "/api/subscriptions/usage?status=Expired&pageSize=1"));
        Assert.Equal(2, page.Total); Assert.Single(page.Items);
        Assert.Equal(10, (await State(owner)).AvailableInterviews);
        f.Clock.Offset = TimeSpan.FromDays(40);
        Assert.False((await State(owner)).HasActiveSubscription);
        Assert.Equal("Expired", (await Usage(owner, due.InterviewId)).Interview.Status);
    }

    [PostgresFact]
    public async Task Usage_migration_reconstructs_known_facts_once_and_preserves_live_completion_and_rollback()
    {
        const string previous = "20261019112006_AddSubscriptionPeriodDiscounts";
        await f.Db(async db => await db.GetService<IMigrator>().MigrateAsync(previous));
        var owner = await Owner(); await Subscribe(owner); await BuyBundle(owner); var (job, template) = await Setup(owner);
        var old = await Issue(owner, job.Id, template.Id);
        using var candidate = Candidate(old);
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/candidate/interviews/{old.InterviewId}/start", null)).StatusCode);
        await f.Db(async db => {
            var s = await db.InterviewSessions.SingleAsync(s => s.Id == old.InterviewId);
            s.Complete(InterviewCompletionReason.Manual, f.Clock.GetUtcNow()); await db.SaveChangesAsync();
            var sql = db.GetService<IMigrator>().GenerateScript(previous, null, MigrationsSqlGenerationOptions.Idempotent);
            await db.Database.ExecuteSqlRawAsync(sql); await db.Database.ExecuteSqlRawAsync(sql);
            Assert.False(db.Database.HasPendingModelChanges());
        });
        var historical = await Usage(owner, old.InterviewId);
        Assert.Equal("Completed", historical.Interview.Status); Assert.Equal(1, historical.Interview.CreditsConsumed);
        Assert.NotNull(historical.Interview.ActualDurationSeconds);
        Assert.Equal(5, historical.Events.Count); Assert.All(historical.Events, e => Assert.True(e.IsHistorical));
        Assert.Contains(historical.Events, e => e.EventType == "Completed");
        var live = await Issue(owner, job.Id, template.Id);
        using var liveCandidate = Candidate(live);
        Assert.Equal(HttpStatusCode.OK, (await liveCandidate.PostAsync($"/api/candidate/interviews/{live.InterviewId}/start", null)).StatusCode);
        await f.Db(async db => {
            var s = await db.InterviewSessions.SingleAsync(s => s.Id == live.InterviewId);
            s.Complete(InterviewCompletionReason.Manual, f.Clock.GetUtcNow()); await db.SaveChangesAsync();
            Assert.Contains(await db.InterviewUsageEvents.Where(e => e.InterviewSessionId == live.InterviewId).ToListAsync(),
                e => e.EventType == "Completed" && !e.IsHistorical);
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(previous));
            Assert.Equal(PostgresErrorCodes.RaiseException, error.SqlState);
        });
        Assert.Equal("Completed", (await Usage(owner, live.InterviewId)).Interview.Status);
    }
}
