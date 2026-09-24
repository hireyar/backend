using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Domain.Hiring;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class HiringTests(ApiFixture f)
{
    private const string Public = "/api/candidate-interview";
    private static async Task<JsonElement> Ok(Task<HttpResponseMessage> task, HttpStatusCode status = HttpStatusCode.OK)
    {
        using var response = await task; Assert.Equal(status, response.StatusCode); return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static Guid Id(JsonElement x) => x.GetProperty("id").GetGuid();
    private static string Version(JsonElement x) => x.GetProperty("rowVersion").GetString()!;
    private static object Template(Guid job, string text = "First question", bool archived = false) => new { jobPositionId = job, title = "Interview", language = "fa-IR", durationMinutes = 5, maxFollowUpQuestionsPerQuestion = 1, isArchived = archived, questions = new[] { new { text, order = 1, competency = "Internal competency", evaluationGuidance = "SECRET_GUIDANCE" }, new { text = "Second question", order = 2, competency = "Private", evaluationGuidance = "SECRET_GUIDANCE" } } };
    private async Task<(Account Hr, JsonElement Candidate, JsonElement Job, JsonElement Template, JsonElement Application, JsonElement Session, object Request, string Email)> Setup(TimeSpan? delay = null, bool failDelivery = false)
    {
        var hr = await f.RegisterConfirmed(); var email = $"{Guid.NewGuid():N}@example.test";
        var candidate = await Ok(hr.Client.Send(HttpMethod.Post, "/api/candidates", new { fullName = "Test Candidate", email, phoneNumber = "123" }), HttpStatusCode.Created);
        var job = await Ok(hr.Client.Send(HttpMethod.Post, "/api/job-positions", new { title = "Backend", description = "Description", seniority = "Senior", status = "Active" }), HttpStatusCode.Created);
        var template = await Ok(hr.Client.Send(HttpMethod.Post, "/api/interview-templates", Template(Id(job))), HttpStatusCode.Created);
        var application = await Ok(hr.Client.Send(HttpMethod.Post, "/api/job-applications", new { candidateId = Id(candidate), jobPositionId = Id(job) }));
        var request = new { jobApplicationId = Id(application), interviewTemplateId = Id(template), availableFromUtc = f.Clock.GetUtcNow().Add(delay ?? TimeSpan.FromMinutes(-1)), expiresAtUtc = f.Clock.GetUtcNow().AddHours(1), idempotencyKey = Guid.NewGuid().ToString("N") };
        f.Email.Fail = failDelivery; JsonElement session; try { session = await Ok(hr.Client.Send(HttpMethod.Post, "/api/interview-sessions", request)); } finally { f.Email.Fail = false; }
        return (hr, candidate, job, template, application, session, request, email);
    }
    private async Task<Browser> Enter(string email)
    {
        var browser = f.Browser(); await Ok(browser.Send(HttpMethod.Post, Public + "/exchange", new { token = f.Email.Token(email) })); return browser;
    }
    private static Task<JsonElement> Start(Browser b) => Ok(b.Send(HttpMethod.Post, Public + "/start", new { accepted = true, version = "text-v1" }));

    [PostgresFact]
    public async Task Text_interview_resumes_with_snapshot_and_completes_idempotently()
    {
        var x = await Setup(); var token = f.Email.Token(x.Email); var browser = f.Browser();
        var preview = await Ok(browser.Send(HttpMethod.Post, Public + "/preview", new { token })); Assert.Equal("NotStarted", preview.GetProperty("status").GetString());
        await Ok(browser.Send(HttpMethod.Post, Public + "/preview", new { token }));
        await Ok(x.Hr.Client.Send(HttpMethod.Put, $"/api/interview-templates/{Id(x.Template)}", new { template = Template(Id(x.Job), "Changed", true), rowVersion = Version(x.Template) }));
        await Ok(x.Hr.Client.Send(HttpMethod.Put, $"/api/candidates/{Id(x.Candidate)}", new { candidate = new { fullName = "Changed", email = $"{Guid.NewGuid():N}@example.test" }, rowVersion = Version(x.Candidate) }));
        var exchange = await browser.Send(HttpMethod.Post, Public + "/exchange", new { token }); Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        Assert.Contains(exchange.Headers.GetValues("Set-Cookie"), c => c.StartsWith("hireyar.candidate=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase) && c.Contains("path=/api/candidate-interview"));
        Assert.DoesNotContain(exchange.Headers.GetValues("Set-Cookie"), c => c.StartsWith("hireyar.refresh="));
        await Ok(browser.Send(HttpMethod.Post, Public + "/exchange", new { token }));
        Assert.Equal(HttpStatusCode.Conflict, (await f.Browser().Send(HttpMethod.Post, Public + "/exchange", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Send(HttpMethod.Post, Public + "/start", new { accepted = false, version = "text-v1" })).StatusCode);
        var started = await Start(browser); var repeated = await Start(browser); Assert.Equal(started.GetProperty("deadlineAtUtc").GetDateTimeOffset(), repeated.GetProperty("deadlineAtUtc").GetDateTimeOffset());
        Assert.Equal("First question", started.GetProperty("currentQuestion").GetProperty("text").GetString());
        Assert.DoesNotContain("SECRET_GUIDANCE", started.GetRawText()); Assert.DoesNotContain("competency", started.GetRawText(), StringComparison.OrdinalIgnoreCase);
        var question = Id(started.GetProperty("currentQuestion"));
        Assert.Equal(HttpStatusCode.Conflict, (await browser.Send(HttpMethod.Post, Public + "/complete")).StatusCode);
        var draft = await Ok(browser.Send(HttpMethod.Put, $"{Public}/answers/{question}", new { status = "Draft", text = "Draft answer" }));
        var resume = await Ok(browser.Send(HttpMethod.Get, Public + "/session")); Assert.Equal("Draft answer", resume.GetProperty("answers")[0].GetProperty("text").GetString());
        var rowVersion = Version(draft.GetProperty("answers")[0]);
        var submit = new { status = "Submitted", text = "Final answer", rowVersion };
        var submitted = await Ok(browser.Send(HttpMethod.Put, $"{Public}/answers/{question}", submit)); await Ok(browser.Send(HttpMethod.Put, $"{Public}/answers/{question}", submit));
        Assert.Equal(HttpStatusCode.Conflict, (await browser.Send(HttpMethod.Put, $"{Public}/answers/{question}", new { status = "Submitted", text = "Changed" })).StatusCode);
        var second = Id(submitted.GetProperty("currentQuestion"));
        await Ok(browser.Send(HttpMethod.Put, $"{Public}/answers/{second}", new { status = "Skipped" }));
        var completed = await Ok(browser.Send(HttpMethod.Post, Public + "/complete")); var again = await Ok(browser.Send(HttpMethod.Post, Public + "/complete")); Assert.Equal(completed.GetProperty("completedAtUtc").GetDateTimeOffset(), again.GetProperty("completedAtUtc").GetDateTimeOffset());
        Assert.Equal(HttpStatusCode.Conflict, (await browser.Send(HttpMethod.Put, $"{Public}/answers/{second}", new { status = "Draft", text = "Late" })).StatusCode);
        var hr = await Ok(x.Hr.Client.Send(HttpMethod.Get, $"/api/interview-sessions/{Id(x.Session)}")); Assert.Equal(x.Email, hr.GetProperty("candidateEmail").GetString()); Assert.Equal("text-v1", hr.GetProperty("consentVersion").GetString()); Assert.Equal(2, hr.GetProperty("answers").GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, (await x.Hr.Client.Send(HttpMethod.Post, $"/api/interview-sessions/{Id(x.Session)}/cancel", new { rowVersion = Version(hr.GetProperty("session")) })).StatusCode);
    }
    [PostgresFact]
    public async Task Candidate_uniqueness_relationships_and_stale_versions_are_enforced()
    {
        var x = await Setup(); var other = await f.RegisterConfirmed();
        Assert.Equal(HttpStatusCode.Conflict, (await x.Hr.Client.Send(HttpMethod.Post, "/api/candidates", new { fullName = "Duplicate", email = x.Email.ToUpperInvariant() })).StatusCode);
        await Ok(other.Client.Send(HttpMethod.Post, "/api/candidates", new { fullName = "Other organization", email = x.Email }), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.Send(HttpMethod.Get, $"/api/candidates/{Id(x.Candidate)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.Send(HttpMethod.Post, "/api/job-applications", new { candidateId = Id(x.Candidate), jobPositionId = Id(x.Job) })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await x.Hr.Client.Send(HttpMethod.Post, "/api/job-applications", new { candidateId = Id(x.Candidate), jobPositionId = Id(x.Job) })).StatusCode);
        var job2 = await Ok(x.Hr.Client.Send(HttpMethod.Post, "/api/job-positions", new { title = "Second", description = "Text", seniority = "Senior", status = "Active" }), HttpStatusCode.Created);
        await Ok(x.Hr.Client.Send(HttpMethod.Post, "/api/job-applications", new { candidateId = Id(x.Candidate), jobPositionId = Id(job2) }));
        var update = new { candidate = new { fullName = "New", email = x.Email }, rowVersion = Version(x.Candidate) };
        await Ok(x.Hr.Client.Send(HttpMethod.Put, $"/api/candidates/{Id(x.Candidate)}", update)); Assert.Equal(HttpStatusCode.Conflict, (await x.Hr.Client.Send(HttpMethod.Put, $"/api/candidates/{Id(x.Candidate)}", update)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await x.Hr.Client.Send(HttpMethod.Get, "/api/candidates?pageSize=101")).StatusCode);
        var list = await Ok(x.Hr.Client.Send(HttpMethod.Get, $"/api/job-applications?candidateId={Id(x.Candidate)}")); Assert.Equal(2, list.GetProperty("total").GetInt32());
        await f.Db(async db => {
            var otherOrg = (await db.Users.FindAsync(other.Id))!.OrganizationId;
            db.JobApplications.Add(new() { OrganizationId = otherOrg, CandidateId = Id(x.Candidate), JobPositionId = Id(x.Job), CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }
    [PostgresFact]
    public async Task Creation_is_atomic_and_idempotent_and_does_not_resend_email()
    {
        var x = await Setup(); var original = f.Email.Token(x.Email);
        var responses = await Task.WhenAll(x.Hr.Client.Send(HttpMethod.Post, "/api/interview-sessions", x.Request), x.Hr.Client.Send(HttpMethod.Post, "/api/interview-sessions", x.Request));
        foreach (var response in responses) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(Id(x.Session), Id(await response.Content.ReadFromJsonAsync<JsonElement>())); }
        Assert.Equal(original, f.Email.Token(x.Email));
        var payload = JsonSerializer.SerializeToElement(x.Request); var key = payload.GetProperty("idempotencyKey").GetString();
        Assert.Equal(HttpStatusCode.Conflict, (await x.Hr.Client.Send(HttpMethod.Post, "/api/interview-sessions", new { jobApplicationId = Id(x.Application), interviewTemplateId = Id(x.Template), availableFromUtc = f.Clock.GetUtcNow(), expiresAtUtc = f.Clock.GetUtcNow().AddHours(2), idempotencyKey = key })).StatusCode);
        await f.Db(async db => { var s = await db.InterviewSessions.Include(s => s.Questions).Include(s => s.Invitation).SingleAsync(s => s.Id == Id(x.Session)); Assert.Equal(2, s.Questions.Count); Assert.NotNull(s.Invitation); Assert.Equal(1, s.Invitation.SendAttempts); Assert.DoesNotContain(original, s.Invitation.TokenHash); });
    }
    [PostgresFact]
    public async Task Exchange_race_csrf_and_authentication_boundaries()
    {
        var x = await Setup(); var token = f.Email.Token(x.Email); var a = f.Browser(); var b = f.Browser();
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Send(HttpMethod.Post, Public + "/exchange", new { token }, csrf: false)).StatusCode);
        var results = await Task.WhenAll(a.Send(HttpMethod.Post, Public + "/exchange", new { token }), b.Send(HttpMethod.Post, Public + "/exchange", new { token }));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        var winner = results[0].IsSuccessStatusCode ? a : b;
        Assert.Equal(HttpStatusCode.Unauthorized, (await winner.Send(HttpMethod.Get, "/api/candidates")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await x.Hr.Client.Send(HttpMethod.Get, Public + "/session")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await winner.Send(HttpMethod.Post, Public + "/start", new { accepted = true, version = "text-v1" }, csrf: false)).StatusCode);
    }
    [PostgresFact]
    public async Task Failed_delivery_resend_rotation_and_snapshot_destination()
    {
        f.Email.Fail = false;
        (Account Hr, JsonElement Candidate, JsonElement Job, JsonElement Template, JsonElement Application, JsonElement Session, object Request, string Email) x;
        try { x = await Setup(failDelivery: true); } finally { f.Email.Fail = false; }
        Assert.Equal("Failed", x.Session.GetProperty("invitation").GetProperty("deliveryStatus").GetString());
        await Ok(x.Hr.Client.Send(HttpMethod.Put, $"/api/candidates/{Id(x.Candidate)}", new { candidate = new { fullName = "Changed", email = $"{Guid.NewGuid():N}@example.test" }, rowVersion = Version(x.Candidate) }));
        f.Clock.Offset = TimeSpan.FromMinutes(2);
        try
        {
            var resent = await Ok(x.Hr.Client.Send(HttpMethod.Post, $"/api/interview-sessions/{Id(x.Session)}/resend")); Assert.Equal("Sent", resent.GetProperty("invitation").GetProperty("deliveryStatus").GetString());
            var old = f.Email.Token(x.Email); Assert.Equal(HttpStatusCode.TooManyRequests, (await x.Hr.Client.Send(HttpMethod.Post, $"/api/interview-sessions/{Id(x.Session)}/resend")).StatusCode);
            f.Clock.Offset = TimeSpan.FromMinutes(4); await Ok(x.Hr.Client.Send(HttpMethod.Post, $"/api/interview-sessions/{Id(x.Session)}/resend"));
            Assert.Equal(HttpStatusCode.BadRequest, (await f.Browser().Send(HttpMethod.Post, Public + "/exchange", new { token = old })).StatusCode);
        }
        finally { f.Clock.Offset = TimeSpan.Zero; }
    }
    [PostgresFact]
    public async Task Server_deadline_expiry_and_dashboard_are_effective_without_scheduler()
    {
        var x = await Setup(TimeSpan.FromMinutes(2)); var b = await Enter(x.Email);
        Assert.Equal(HttpStatusCode.Conflict, (await b.Send(HttpMethod.Post, Public + "/start", new { accepted = true, version = "text-v1" })).StatusCode);
        f.Clock.Offset = TimeSpan.FromMinutes(3);
        try
        {
            var started = await Start(b); var deadline = started.GetProperty("deadlineAtUtc").GetDateTimeOffset();
            Assert.Equal(TimeSpan.FromMinutes(5), deadline - started.GetProperty("startedAtUtc").GetDateTimeOffset());
            f.Clock.Offset = TimeSpan.FromMinutes(9);
            var list = await Ok(x.Hr.Client.Send(HttpMethod.Get, "/api/interview-sessions")); Assert.Equal("Expired", list.GetProperty("items")[0].GetProperty("status").GetString());
            var dashboard = await Ok(x.Hr.Client.Send(HttpMethod.Get, "/api/dashboard")); Assert.Equal(1, dashboard.GetProperty("sessions").EnumerateArray().Single(i => i.GetProperty("status").GetString() == "Expired").GetProperty("count").GetInt32());
            Assert.Equal(HttpStatusCode.Gone, (await b.Send(HttpMethod.Post, Public + "/complete")).StatusCode);
            await f.Db(async db => Assert.Equal(SessionStatus.Expired, (await db.InterviewSessions.FindAsync(Id(x.Session)))!.Status));
        }
        finally { f.Clock.Offset = TimeSpan.Zero; }
    }
    [PostgresFact]
    public async Task Cancellation_revokes_candidate_and_preserves_answers()
    {
        var x = await Setup(); var b = await Enter(x.Email); var started = await Start(b); var q = Id(started.GetProperty("currentQuestion"));
        await Ok(b.Send(HttpMethod.Put, $"{Public}/answers/{q}", new { status = "Submitted", text = "Preserved" }));
        var hr = await Ok(x.Hr.Client.Send(HttpMethod.Get, $"/api/interview-sessions/{Id(x.Session)}"));
        await Ok(x.Hr.Client.Send(HttpMethod.Post, $"/api/interview-sessions/{Id(x.Session)}/cancel", new { rowVersion = Version(hr.GetProperty("session")) }));
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Send(HttpMethod.Get, Public + "/session")).StatusCode);
        var final = await Ok(x.Hr.Client.Send(HttpMethod.Get, $"/api/interview-sessions/{Id(x.Session)}")); Assert.Equal("Preserved", final.GetProperty("answers")[0].GetProperty("text").GetString());
    }
    [PostgresFact]
    public async Task Closed_application_blocks_creation_and_start_and_wrong_question_is_rejected()
    {
        var x = await Setup(); var b = await Enter(x.Email);
        await Ok(x.Hr.Client.Send(HttpMethod.Put, $"/api/job-applications/{Id(x.Application)}/status", new { status = "Closed", rowVersion = Version(x.Application) }));
        Assert.Equal(HttpStatusCode.Conflict, (await b.Send(HttpMethod.Post, Public + "/start", new { accepted = true, version = "text-v1" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await x.Hr.Client.Send(HttpMethod.Post, "/api/interview-sessions", new { jobApplicationId = Id(x.Application), interviewTemplateId = Id(x.Template), availableFromUtc = f.Clock.GetUtcNow(), expiresAtUtc = f.Clock.GetUtcNow().AddHours(1), idempotencyKey = Guid.NewGuid().ToString("N") })).StatusCode);
        var y = await Setup(); var other = await Enter(y.Email); await Start(other);
        Assert.Equal(HttpStatusCode.Conflict, (await other.Send(HttpMethod.Put, $"{Public}/answers/{Guid.NewGuid()}", new { status = "Submitted", text = "Wrong question" })).StatusCode);
    }
}


