using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Auth;
using HireYar.Application.JobPositions;
using HireYar.Application.InterviewTemplates;
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
        var a = await f.RegisterOwner(); var b = await f.RegisterOwner(); var job = await Job(a.Client); var id = job.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.Send(HttpMethod.Get, $"/api/job-positions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.Send(HttpMethod.Put, $"/api/job-positions/{id}", new { job = new { title = "Attack", description = "Text", seniority = "Senior", status = "Active" }, rowVersion = job.GetProperty("rowVersion").GetString() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.Send(HttpMethod.Post, "/api/interview-templates", Template(id))).StatusCode);
        var list = await (await b.Client.Send(HttpMethod.Get, "/api/job-positions")).Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(0, list.GetProperty("total").GetInt32());
        var dashboard = await (await b.Client.Send(HttpMethod.Get, "/api/dashboard")).Content.ReadFromJsonAsync<JsonElement>(); Assert.All(dashboard.GetProperty("jobs").EnumerateArray(), x => Assert.Equal(0, x.GetProperty("count").GetInt32())); Assert.False(dashboard.TryGetProperty("activeMembers", out _));
        var template = await (await a.Client.Send(HttpMethod.Post, "/api/interview-templates", Template(id))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.Send(HttpMethod.Get, $"/api/interview-templates/{template.GetProperty("id").GetGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Client.Send(HttpMethod.Get, "/api/job-positions?pageSize=101")).StatusCode);
    }
    [PostgresFact]
    public async Task Job_and_template_stale_versions_conflict_and_question_order_swaps_preserve_ids()
    {
        var a = await f.RegisterOwner(); var job = await Job(a.Client); var id = job.GetProperty("id").GetGuid();
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
        var a = await f.RegisterOwner(); var job = await Job(a.Client); var id = job.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Client.Send(HttpMethod.Post, "/api/interview-templates", Template(id, Array.Empty<object>()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Client.Send(HttpMethod.Post, "/api/interview-templates", Template(id, new[] { new { text = "A", order = 1, competency = "A", evaluationGuidance = "A" }, new { text = "B", order = 1, competency = "A", evaluationGuidance = "A" } }))).StatusCode);
    }
}
