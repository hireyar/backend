using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Application.InterviewTemplates;
using HireYar.Application.JobPositions;
using HireYar.Domain.JobPositions;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class TitleUniquenessTests(ApiFixture f)
{
    private static JobRequest Job(string title) => new(title, "Synthetic job description", "Senior", JobStatus.Active);
    private static TemplateRequest Template(Guid job, string title) => new(job, title, 30, 0,
        [new(null, "Synthetic question", 1, "Skill", "Evidence")]);
    private static WizardTemplateRequest WizardTemplate(Guid job, string title) => new(job, title, "general",
        Enumerable.Range(1, 5).Select(i => $"Synthetic question {i}").ToList(), "Welcome", "Goodbye");
    private static async Task<T> Read<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(BillingJson.Options))!;
    }
    private static async Task Duplicate(HttpResponseMessage response, string code)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, problem.GetProperty("code").GetString());
    }

    [PostgresFact]
    public async Task Job_titles_are_shared_between_routes_scoped_to_tenant_and_released_only_on_delete()
    {
        var owner = await f.RegisterOwner();
        var first = await Read<JobDto>(await owner.Client.Send(HttpMethod.Post, "/api/job-positions", Job("Backend")), HttpStatusCode.Created);
        await Duplicate(await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/positions",
            new WizardPositionRequest(" backend ", "Synthetic description")), "job_title_duplicate");
        var other = await f.RegisterOwner();
        await Read<JobDto>(await other.Client.Send(HttpMethod.Post, "/api/job-positions", Job("Backend")), HttpStatusCode.Created);
        var same = await Read<JobDto>(await owner.Client.Send(HttpMethod.Put, $"/api/job-positions/{first.Id}",
            new UpdateJobRequest(Job(" Backend "), first.RowVersion)), HttpStatusCode.OK);
        var second = await Read<JobDto>(await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/positions",
            new WizardPositionRequest("Another", "Synthetic description")), HttpStatusCode.OK);
        await Duplicate(await owner.Client.Send(HttpMethod.Put, $"/api/panel-wizard/positions/{second.Id}",
            new { position = new WizardPositionRequest("BACKEND", "Changed"), rowVersion = second.RowVersion }), "job_title_duplicate");
        await f.Db(async db =>
        {
            var saved = await db.JobPositions.SingleAsync(x => x.Id == second.Id);
            Assert.Equal("Another", saved.Title);
            Assert.Equal(second.RowVersion, saved.RowVersion);
        });
        var archived = await Read<JobDto>(await owner.Client.Send(HttpMethod.Put, $"/api/job-positions/{same.Id}",
            new UpdateJobRequest(Job(same.Title) with { Status = JobStatus.Archived }, same.RowVersion)), HttpStatusCode.OK);
        await Duplicate(await owner.Client.Send(HttpMethod.Post, "/api/job-positions", Job("backend")), "job_title_duplicate");
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Delete, $"/api/job-positions/{archived.Id}")).StatusCode);
        await Read<JobDto>(await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/positions",
            new WizardPositionRequest("Backend", "Synthetic description")), HttpStatusCode.OK);
    }

    [PostgresFact]
    public async Task Template_titles_are_unique_across_positions_and_routes_and_edit_preserves_questions_on_conflict()
    {
        var owner = await f.RegisterOwner();
        var job = await Read<JobDto>(await owner.Client.Send(HttpMethod.Post, "/api/job-positions", Job("First")), HttpStatusCode.Created);
        var secondJob = await Read<JobDto>(await owner.Client.Send(HttpMethod.Post, "/api/job-positions", Job("Second")), HttpStatusCode.Created);
        var first = await Read<TemplateDto>(await owner.Client.Send(HttpMethod.Post, "/api/interview-templates",
            Template(job.Id, "Interview")), HttpStatusCode.Created);
        await Duplicate(await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/templates",
            WizardTemplate(secondJob.Id, " interview ")), "template_title_duplicate");
        var same = await Read<TemplateDto>(await owner.Client.Send(HttpMethod.Put, $"/api/interview-templates/{first.Id}",
            new UpdateTemplateRequest(Template(job.Id, "Interview"), first.RowVersion)), HttpStatusCode.OK);
        var second = await Read<TemplateDto>(await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/templates",
            WizardTemplate(secondJob.Id, "Another")), HttpStatusCode.OK);
        await Duplicate(await owner.Client.Send(HttpMethod.Put, $"/api/panel-wizard/templates/{second.Id}",
            new UpdateWizardTemplateRequest(WizardTemplate(secondJob.Id, "INTERVIEW"), second.RowVersion)), "template_title_duplicate");
        await Duplicate(await owner.Client.Send(HttpMethod.Put, $"/api/interview-templates/{same.Id}",
            new UpdateTemplateRequest(Template(job.Id, "Another"), same.RowVersion)), "template_title_duplicate");
        await f.Db(async db =>
        {
            var saved = await db.InterviewTemplates.Include(x => x.Questions).SingleAsync(x => x.Id == second.Id);
            Assert.Equal("Another", saved.Title);
            Assert.Equal(second.RowVersion, saved.RowVersion);
            Assert.Equal(5, saved.Questions.Count);
        });
        var archived = await Read<TemplateDto>(await owner.Client.Send(HttpMethod.Put, $"/api/interview-templates/{same.Id}",
            new UpdateTemplateRequest(Template(job.Id, "Interview") with { IsArchived = true }, same.RowVersion)), HttpStatusCode.OK);
        await Duplicate(await owner.Client.Send(HttpMethod.Post, "/api/interview-templates",
            Template(secondJob.Id, "interview")), "template_title_duplicate");
        var other = await f.RegisterOwner();
        var otherJob = await Read<JobDto>(await other.Client.Send(HttpMethod.Post, "/api/job-positions", Job("First")), HttpStatusCode.Created);
        await Read<TemplateDto>(await other.Client.Send(HttpMethod.Post, "/api/interview-templates",
            Template(otherJob.Id, "Interview")), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Delete, $"/api/interview-templates/{archived.Id}")).StatusCode);
        await Read<TemplateDto>(await owner.Client.Send(HttpMethod.Post, "/api/panel-wizard/templates",
            WizardTemplate(secondJob.Id, "Interview")), HttpStatusCode.OK);
    }

    [PostgresFact]
    public async Task Concurrent_creates_from_both_routes_have_only_one_winner_for_each_title()
    {
        var owner = await f.RegisterOwner();
        var client = f.Browser();
        client.AccessToken = owner.Client.AccessToken;
        var jobs = await Task.WhenAll(
            owner.Client.Send(HttpMethod.Post, "/api/job-positions", Job("Concurrent")),
            client.Send(HttpMethod.Post, "/api/panel-wizard/positions", new WizardPositionRequest(" concurrent ", "Synthetic description")));
        var winner = Assert.Single(jobs, x => x.IsSuccessStatusCode);
        await Duplicate(Assert.Single(jobs, x => x.StatusCode == HttpStatusCode.Conflict), "job_title_duplicate");
        var job = (await winner.Content.ReadFromJsonAsync<JobDto>(BillingJson.Options))!;
        var templates = await Task.WhenAll(
            owner.Client.Send(HttpMethod.Post, "/api/interview-templates", Template(job.Id, "Concurrent")),
            client.Send(HttpMethod.Post, "/api/panel-wizard/templates", WizardTemplate(job.Id, " concurrent ")));
        Assert.Single(templates, x => x.IsSuccessStatusCode);
        await Duplicate(Assert.Single(templates, x => x.StatusCode == HttpStatusCode.Conflict), "template_title_duplicate");
        await f.Db(async db =>
        {
            var organizationId = await db.Users.Where(x => x.Id == owner.Id).Select(x => x.OrganizationId).SingleAsync();
            Assert.Equal(1, await db.JobPositions.CountAsync(x => x.OrganizationId == organizationId));
            Assert.Equal(1, await db.InterviewTemplates.CountAsync(x => x.OrganizationId == organizationId));
        });
    }

    [PostgresFact]
    public async Task Concurrent_renames_to_the_same_title_have_only_one_winner()
    {
        var owner = await f.RegisterOwner();
        var first = await Read<JobDto>(await owner.Client.Send(HttpMethod.Post, "/api/job-positions", Job("First")), HttpStatusCode.Created);
        var second = await Read<JobDto>(await owner.Client.Send(HttpMethod.Post, "/api/job-positions", Job("Second")), HttpStatusCode.Created);
        var firstTemplate = await Read<TemplateDto>(await owner.Client.Send(HttpMethod.Post, "/api/interview-templates",
            Template(first.Id, "First")), HttpStatusCode.Created);
        var secondTemplate = await Read<TemplateDto>(await owner.Client.Send(HttpMethod.Post, "/api/interview-templates",
            Template(first.Id, "Second")), HttpStatusCode.Created);
        var client = f.Browser();
        client.AccessToken = owner.Client.AccessToken;
        var jobs = await Task.WhenAll(
            owner.Client.Send(HttpMethod.Put, $"/api/job-positions/{first.Id}", new UpdateJobRequest(Job("Shared"), first.RowVersion)),
            client.Send(HttpMethod.Put, $"/api/panel-wizard/positions/{second.Id}",
                new { position = new WizardPositionRequest(" shared ", "Synthetic description"), rowVersion = second.RowVersion }));
        Assert.Single(jobs, x => x.IsSuccessStatusCode);
        await Duplicate(Assert.Single(jobs, x => x.StatusCode == HttpStatusCode.Conflict), "job_title_duplicate");
        var templates = await Task.WhenAll(
            owner.Client.Send(HttpMethod.Put, $"/api/interview-templates/{firstTemplate.Id}",
                new UpdateTemplateRequest(Template(first.Id, "Shared"), firstTemplate.RowVersion)),
            client.Send(HttpMethod.Put, $"/api/panel-wizard/templates/{secondTemplate.Id}",
                new UpdateWizardTemplateRequest(WizardTemplate(first.Id, " shared "), secondTemplate.RowVersion)));
        Assert.Single(templates, x => x.IsSuccessStatusCode);
        await Duplicate(Assert.Single(templates, x => x.StatusCode == HttpStatusCode.Conflict), "template_title_duplicate");
    }
}
