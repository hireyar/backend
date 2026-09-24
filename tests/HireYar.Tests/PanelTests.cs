using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Auth;
using HireYar.Application.JobPositions;
using HireYar.Application.InterviewTemplates;
using HireYar.Application.Members;
using HireYar.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class PanelTests(ApiFixture f)
{
    private static async Task<JsonElement> Job(Browser c, string title = "Backend")
    { var response = await c.Send(HttpMethod.Post, "/api/job-positions", new { title, description = "Job description", seniority = "Senior", status = "Draft" }); Assert.Equal(HttpStatusCode.Created, response.StatusCode); return await response.Content.ReadFromJsonAsync<JsonElement>(); }
    private static object Template(Guid job, IEnumerable<object>? questions = null) => new { jobPositionId = job, title = "Interview", durationMinutes = 30, maxFollowUpQuestionsPerQuestion = 2, language = "fa-IR", isArchived = false, questions = questions ?? new[] { new { text = "Explain a decision", order = 1, competency = "Judgement", evaluationGuidance = "Evidence" } } };
    [PostgresFact]
    public async Task Tenant_reads_writes_dashboard_and_related_records_are_isolated()
    {
        var a = await f.RegisterConfirmed(); var b = await f.RegisterConfirmed(); var job = await Job(a.Client); var id = job.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.Send(HttpMethod.Get, $"/api/job-positions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.Send(HttpMethod.Put, $"/api/job-positions/{id}", new { job = new { title = "Attack", description = "Text", seniority = "Senior", status = "Active" }, rowVersion = job.GetProperty("rowVersion").GetString() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.Send(HttpMethod.Post, "/api/interview-templates", Template(id))).StatusCode);
        var list = await (await b.Client.Send(HttpMethod.Get, "/api/job-positions")).Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(0, list.GetProperty("total").GetInt32());
        var dashboard = await (await b.Client.Send(HttpMethod.Get, "/api/dashboard")).Content.ReadFromJsonAsync<JsonElement>(); Assert.All(dashboard.GetProperty("jobs").EnumerateArray(), x => Assert.Equal(0, x.GetProperty("count").GetInt32())); Assert.Equal(1, dashboard.GetProperty("activeMembers").GetInt32());
        var template = await (await a.Client.Send(HttpMethod.Post, "/api/interview-templates", Template(id))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.Send(HttpMethod.Get, $"/api/interview-templates/{template.GetProperty("id").GetGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Client.Send(HttpMethod.Get, "/api/job-positions?pageSize=101")).StatusCode);
    }
    [PostgresFact]
    public async Task Job_and_template_stale_versions_conflict_and_question_order_swaps_preserve_ids()
    {
        var a = await f.RegisterConfirmed(); var job = await Job(a.Client); var id = job.GetProperty("id").GetGuid();
        var update = new { job = new { title = "Changed", description = "Text", seniority = "Senior", status = "Active" }, rowVersion = job.GetProperty("rowVersion").GetString() };
        Assert.Equal(HttpStatusCode.OK, (await a.Client.Send(HttpMethod.Put, $"/api/job-positions/{id}", update)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await a.Client.Send(HttpMethod.Put, $"/api/job-positions/{id}", update)).StatusCode);
        var initial = Template(id, new[] { new { text = "First", order = 1, competency = "A", evaluationGuidance = "A" }, new { text = "Second", order = 2, competency = "B", evaluationGuidance = "B" } });
        var response = await a.Client.Send(HttpMethod.Post, "/api/interview-templates", initial); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var template = await response.Content.ReadFromJsonAsync<TemplateDto>(); var first = template!.Questions[0]; var second = template.Questions[1];
        var edit = new { template = Template(id, new[] { new { id = first.Id, text = first.Text, order = 2, competency = first.Competency, evaluationGuidance = first.EvaluationGuidance }, new { id = second.Id, text = second.Text, order = 1, competency = second.Competency, evaluationGuidance = second.EvaluationGuidance } }), rowVersion = template.RowVersion };
        var changed = await a.Client.Send(HttpMethod.Put, $"/api/interview-templates/{template.Id}", edit); Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var result = (await changed.Content.ReadFromJsonAsync<TemplateDto>())!; Assert.Equal(second.Id, result.Questions[0].Id); Assert.Equal(first.Id, result.Questions[1].Id);
        Assert.Equal(HttpStatusCode.Conflict, (await a.Client.Send(HttpMethod.Put, $"/api/interview-templates/{template.Id}", edit)).StatusCode);
        var remove = new { template = Template(id, new object[] { new { id = first.Id, text = "Kept", order = 1, competency = "A", evaluationGuidance = "A" }, new { text = "New", order = 2, competency = "B", evaluationGuidance = "B" } }), rowVersion = result.RowVersion };
        var added = await a.Client.Send(HttpMethod.Put, $"/api/interview-templates/{template.Id}", remove); Assert.Equal(HttpStatusCode.OK, added.StatusCode); var final = (await added.Content.ReadFromJsonAsync<TemplateDto>())!;
        Assert.Equal(first.Id, final.Questions[0].Id); Assert.DoesNotContain(final.Questions, q => q.Id == second.Id);
        var foreign = new { template = Template(id, new[] { new { id = Guid.NewGuid(), text = "Unknown", order = 1, competency = "A", evaluationGuidance = "A" } }), rowVersion = final.RowVersion };
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Client.Send(HttpMethod.Put, $"/api/interview-templates/{template.Id}", foreign)).StatusCode);
    }
    [PostgresFact]
    public async Task Template_validation_rejects_invalid_order_count_and_limits()
    {
        var a = await f.RegisterConfirmed(); var job = await Job(a.Client); var id = job.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Client.Send(HttpMethod.Post, "/api/interview-templates", Template(id, Array.Empty<object>()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Client.Send(HttpMethod.Post, "/api/interview-templates", Template(id, new[] { new { text = "A", order = 1, competency = "A", evaluationGuidance = "A" }, new { text = "B", order = 1, competency = "A", evaluationGuidance = "A" } }))).StatusCode);
    }
    [PostgresFact]
    public async Task Invitations_are_single_use_and_recruiter_cannot_manage_members_or_organization()
    {
        var owner = await f.RegisterConfirmed(); var email = $"{Guid.NewGuid():N}@example.test";
        var invited = await owner.Client.Send(HttpMethod.Post, "/api/members/invitations", new InviteRequest(email)); Assert.Equal(HttpStatusCode.OK, invited.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.Send(HttpMethod.Post, "/api/members/invitations", new InviteRequest(email))).StatusCode);
        var c = f.Browser(); var accept = new AcceptInviteRequest(f.Email.Token(email), "Recruiter", "Valid-test-password1!");
        Assert.Equal(HttpStatusCode.NoContent, (await c.Send(HttpMethod.Post, "/api/auth/invitations/accept", accept)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/api/auth/invitations/accept", accept)).StatusCode);
        var login = await c.Send(HttpMethod.Post, "/api/auth/login", new LoginRequest(email, accept.Password)); c.AccessToken = (await login.Content.ReadFromJsonAsync<AccessResponse>())!.AccessToken;
        Assert.Equal(HttpStatusCode.Forbidden, (await c.Send(HttpMethod.Get, "/api/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.Send(HttpMethod.Put, "/api/organization", new { name = "Changed" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Get, "/api/organization")).StatusCode); await Job(c);
        var me = await (await c.Send(HttpMethod.Get, "/api/auth/me")).Content.ReadFromJsonAsync<JsonElement>(); var id = me.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Put, $"/api/members/{id}/active", new ActiveRequest(false))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.Send(HttpMethod.Get, "/api/dashboard")).StatusCode);
        c.AccessToken = null; Assert.Equal(HttpStatusCode.Unauthorized, (await c.Send(HttpMethod.Post, "/api/auth/refresh")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Put, $"/api/members/{id}/active", new ActiveRequest(true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.Send(HttpMethod.Put, $"/api/members/{owner.Id}/active", new ActiveRequest(false))).StatusCode);
    }
    [PostgresFact]
    public async Task Expired_cancelled_resent_and_cross_tenant_invitations_are_safe()
    {
        var a = await f.RegisterConfirmed(); var b = await f.RegisterConfirmed(); var email = $"{Guid.NewGuid():N}@example.test";
        var response = await a.Client.Send(HttpMethod.Post, "/api/members/invitations", new InviteRequest(email)); var invitation = (await response.Content.ReadFromJsonAsync<InvitationDelivery>())!; var old = f.Email.Token(email);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.Send(HttpMethod.Delete, $"/api/members/invitations/{invitation.Id}")).StatusCode);
        f.Clock.Offset = TimeSpan.FromDays(4);
        try { Assert.Equal(HttpStatusCode.BadRequest, (await f.Browser().Send(HttpMethod.Post, "/api/auth/invitations/accept", new AcceptInviteRequest(old, "Name", "Valid-test-password1!"))).StatusCode); }
        finally { f.Clock.Offset = TimeSpan.Zero; }
        Assert.Equal(HttpStatusCode.OK, (await a.Client.Send(HttpMethod.Post, $"/api/members/invitations/{invitation.Id}/resend")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Browser().Send(HttpMethod.Post, "/api/auth/invitations/accept", new AcceptInviteRequest(old, "Name", "Valid-test-password1!"))).StatusCode);
        var current = f.Email.Token(email); Assert.Equal(HttpStatusCode.NoContent, (await a.Client.Send(HttpMethod.Delete, $"/api/members/invitations/{invitation.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Browser().Send(HttpMethod.Post, "/api/auth/invitations/accept", new AcceptInviteRequest(current, "Name", "Valid-test-password1!"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await a.Client.Send(HttpMethod.Post, "/api/members/invitations", new InviteRequest(b.Email))).StatusCode);
    }
    [PostgresFact]
    public async Task Concurrent_invitation_acceptance_creates_one_recruiter()
    {
        var a = await f.RegisterConfirmed(); var email = $"{Guid.NewGuid():N}@example.test"; await a.Client.Send(HttpMethod.Post, "/api/members/invitations", new InviteRequest(email));
        var accept = new AcceptInviteRequest(f.Email.Token(email), "Name", "Valid-test-password1!");
        var responses = await Task.WhenAll(f.Browser().Send(HttpMethod.Post, "/api/auth/invitations/accept", accept), f.Browser().Send(HttpMethod.Post, "/api/auth/invitations/accept", accept));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.NoContent); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.BadRequest);
        await f.Db(async db => Assert.Equal(1, await db.Users.CountAsync(x => x.Email == email)));
    }
    [PostgresFact]
    public async Task Existing_access_token_uses_current_role()
    {
        var a = await f.RegisterConfirmed(); await f.Db(async db => { var u = await db.Users.FindAsync(a.Id); u!.Role = OrganizationRole.Recruiter; await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Forbidden, (await a.Client.Send(HttpMethod.Get, "/api/members")).StatusCode);
    }
}
