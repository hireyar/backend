using System.Net;
using System.Text.Json;
using HireYar.Application.Common;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using HireYar.Infrastructure.Interviews;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HireYar.Tests;

public sealed class InterviewDiagnosticTests
{
    private static InterviewDiagnosticDto Event(string? prompt = null) => new("", default, "test-request", "test-operation", "prompt", "llm", "prepared", "synthetic", PromptJson: prompt);
    [Fact]
    public void Disabled_capture_scope_redaction_retention_and_replay_are_bounded()
    {
        var options = new InterviewDiagnosticOptions(); var clock = new TestClock();
        using var store = new InterviewDiagnostics(options, clock);
        var org = Guid.NewGuid(); var id = Guid.NewGuid();
        store.Publish(org, id, Event("synthetic private prompt")); Assert.Empty(store.Read(org, id, null, true).Events);
        options.Enabled = true;
        store.Publish(org, id, Event("not captured")); Assert.Null(Assert.Single(store.Read(org, id, null, true).Events).PromptJson);
        options.CapturePrompts = true;
        var before = store.Read(org, id, null, true).Cursor;
        store.Publish(org, id, Event("synthetic private prompt"));
        Assert.Equal("synthetic private prompt", Assert.Single(store.Read(org, id, before, true).Events).PromptJson);
        Assert.Null(Assert.Single(store.Read(org, id, before, false).Events).PromptJson);
        Assert.Empty(store.Read(Guid.NewGuid(), id, null, true).Events);
        Assert.Empty(store.Read(org, Guid.NewGuid(), null, true).Events);
        for (var i = 0; i < 300; i++) store.Publish(org, id, Event());
        var replay = store.Read(org, id, before, true); Assert.True(replay.Reset); Assert.Equal(256, replay.Events.Count);
        Assert.True(store.Read(org, id, "another-process:1", true).Reset);
        clock.Offset = TimeSpan.FromMinutes(11); Assert.Empty(store.Read(org, id, null, true).Events);
        using (store.Subscribe(org, id))
        {
            Assert.True(store.Read(org, id, before, true).Reset);
            Assert.False(store.Read(org, id, null, true).Reset);
        }
        options.Enabled = false; Assert.Throws<AppException>(() => store.Subscribe(org, id));
    }
    [Fact]
    public async Task Async_execution_context_isolated_and_subscription_limit_survives_eviction()
    {
        using var store = new InterviewDiagnostics(new() { Enabled = true }, TimeProvider.System);
        var org = Guid.NewGuid(); var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        await Task.WhenAll(ids.Select(async id => {
            using var scope = InterviewTrace.Begin(store, org, id, id.ToString());
            await Task.Yield(); InterviewTrace.Emit("stage", "test", "started", "synthetic");
        }));
        foreach (var id in ids) Assert.Equal(id.ToString(), Assert.Single(store.Read(org, id, null, false).Events).RequestId);
        Assert.False(InterviewTrace.Active);
        var leases = Enumerable.Range(0, 4).Select(_ => store.Subscribe(org, ids[0])).ToArray();
        for (var i = 0; i < 130; i++) store.Publish(org, Guid.NewGuid(), Event());
        Assert.Equal(429, Assert.Throws<AppException>(() => store.Subscribe(org, ids[0])).Status);
        foreach (var lease in leases) { lease.Dispose(); lease.Dispose(); }
        using var next = store.Subscribe(org, ids[0]);
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct); }
    [Fact]
    public async Task Provider_emits_start_before_completion_and_captures_exact_wire_prompt_without_credentials()
    {
        using var store = new InterviewDiagnostics(new() { Enabled = true, CapturePrompts = true }, TimeProvider.System);
        var org = Guid.NewGuid(); var id = Guid.NewGuid(); string? wire = null;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new Handler(async (request, ct) => {
            wire = await request.Content!.ReadAsStringAsync(ct); entered.SetResult(); await release.Task.WaitAsync(ct);
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"{}\"}}]}") };
        }));
        var client = new NetArzClient(http, Options.Create(new NetArzOptions { ApiKey = "synthetic-secret", LlmTimeoutSeconds = 30 }), NullLogger<NetArzClient>.Instance);
        var q = new InterviewQuestionSnapshot(Guid.NewGuid(), Guid.NewGuid(), "synthetic question", "topic", 3, true);
        using var trace = InterviewTrace.Begin(store, org, id, "request");
        var call = client.EvaluateAsync(new("Synthetic job", q.QuestionId, "ProcessingAnswer", 0, 3, 300, q, "synthetic answer", [], [], [], [q], MasterPrompt: "synthetic system"), default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var live = store.Read(org, id, null, true).Events;
        Assert.Contains(live, x => x.Kind == "provider" && x.Status == "started");
        Assert.DoesNotContain(live, x => x.Status == "httpSucceeded");
        var prompt = Assert.Single(live, x => x.Kind == "prompt"); Assert.Equal(wire, prompt.PromptJson);
        Assert.DoesNotContain("synthetic-secret", JsonSerializer.Serialize(live));
        Assert.Null(Assert.Single(store.Read(org, id, null, false).Events, x => x.Kind == "prompt").PromptJson);
        release.SetResult(); await call;
        var done = Assert.Single(store.Read(org, id, null, true).Events, x => x.Status == "httpSucceeded");
        Assert.Equal(prompt.OperationId, done.OperationId); Assert.Equal(200, done.HttpStatusCode); Assert.True(done.DurationMs >= 0);
    }
}
