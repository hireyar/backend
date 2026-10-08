using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Interviews;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Tests;

public sealed partial class InterviewFlowTests
{
    [PostgresFact]
    public async Task Diagnostics_disabled_hide_flags_and_endpoints()
    {
        var (hr, invite) = await Invite(); using var candidate = Candidate(invite);
        var state = (await candidate.GetFromJsonAsync<CandidateInterviewDto>($"/api/candidate/interviews/{invite.InterviewId}/current"))!;
        Assert.False(state.DiagnosticsEnabled); Assert.Null(state.DiagnosticsUrl);
        Assert.Equal(HttpStatusCode.NotFound, (await candidate.GetAsync($"/api/candidate/interviews/{invite.InterviewId}/diagnostics")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await hr.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}/diagnostics")).StatusCode);
    }
    [PostgresFact]
    public async Task Diagnostics_stream_is_live_scoped_redacts_prompts_and_closes_after_revocation()
    {
        var options = f.Services.GetRequiredService<InterviewDiagnosticOptions>(); options.Enabled = true;
        try
        {
            var (hr, invite) = await Invite(); using var candidate = Candidate(invite);
            var state = (await candidate.GetFromJsonAsync<CandidateInterviewDto>($"/api/candidate/interviews/{invite.InterviewId}/current"))!;
            Assert.True(state.DiagnosticsEnabled); Assert.EndsWith("/diagnostics", state.DiagnosticsUrl);
            var store = f.Services.GetRequiredService<IInterviewDiagnostics>();
            Guid organizationId = default;
            await f.Db(async db => organizationId = await db.Users.Where(x => x.Id == hr.Id).Select(x => x.OrganizationId).SingleAsync());
            var events = store.Read(organizationId, invite.InterviewId, null, true).Events;
            Assert.Contains(events, x => x.Kind == "api" && x.Operation == "Current" && x.Status == "started");
            Assert.Contains(events, x => x.Kind == "database" && x.Status == "succeeded" && x.Tables!.Contains("InterviewSessions") && x.Columns!.Length > 0);
            Assert.DoesNotContain(invite.Token, JsonSerializer.Serialize(events));
            var marker = new InterviewDiagnosticDto("", default, "request", "operation", "prompt", "llm", "prepared", "synthetic", PromptJson: "synthetic-private-prompt");
            store.Publish(organizationId, invite.InterviewId, marker);
            using var owner = f.CreateClient(); owner.DefaultRequestHeaders.Authorization = new("Bearer", hr.Client.AccessToken);
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var ownerResponse = await owner.SendAsync(new(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}/diagnostics"), HttpCompletionOption.ResponseHeadersRead, cancel.Token);
            Assert.Equal("text/event-stream", ownerResponse.Content.Headers.ContentType!.MediaType);
            using var ownerReader = new StreamReader(await ownerResponse.Content.ReadAsStreamAsync(cancel.Token));
            var ownerPrompt = await DiagnosticData(ownerReader, x => x.GetProperty("kind").GetString() == "prompt", cancel.Token);
            Assert.Equal("synthetic-private-prompt", ownerPrompt.GetProperty("promptJson").GetString());
            using var response = await candidate.SendAsync(new(HttpMethod.Get, state.DiagnosticsUrl), HttpCompletionOption.ResponseHeadersRead, cancel.Token);
            using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancel.Token));
            var candidatePrompt = await DiagnosticData(reader, x => x.GetProperty("kind").GetString() == "prompt", cancel.Token);
            Assert.Equal(JsonValueKind.Null, candidatePrompt.GetProperty("promptJson").ValueKind);
            // A new event arrives on an already open stream without another request.
            store.Publish(organizationId, invite.InterviewId, marker with { Kind = "stage", Operation = "live-test", PromptJson = null });
            Assert.Equal("live-test", (await DiagnosticData(reader, x => x.GetProperty("operation").GetString() == "live-test", cancel.Token)).GetProperty("operation").GetString());
            var other = await f.RegisterOwner();
            Assert.Equal(HttpStatusCode.NotFound, (await other.Client.Send(HttpMethod.Get, $"/api/interviews/{invite.InterviewId}/diagnostics")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await candidate.GetAsync($"/api/interviews/{invite.InterviewId}/diagnostics")).StatusCode);
            await hr.Client.Send(HttpMethod.Post, $"/api/interviews/{invite.InterviewId}/revoke");
            string? line; do { line = await reader.ReadLineAsync(cancel.Token); } while (line is not null && line != "event: closed");
            Assert.Equal("event: closed", line);
            cancel.Cancel();
        }
        finally { options.Enabled = false; }
    }
    private static async Task<JsonElement> DiagnosticData(StreamReader reader, Func<JsonElement, bool> match, CancellationToken ct)
    {
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;
            var data = JsonSerializer.Deserialize<JsonElement>(line[6..]);
            if (data.TryGetProperty("kind", out _) && match(data)) return data;
        }
        throw new InvalidOperationException("Diagnostic stream ended before expected event.");
    }
}
