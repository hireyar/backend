using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Common;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using HireYar.Domain.JobPositions;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class PaginationTests
{
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }

    [PostgresFact]
    public async Task Question_bank_pages_are_complete_scoped_and_stably_ordered()
    {
        var f = new ApiFixture();
        await ((Xunit.IAsyncLifetime)f).InitializeAsync();
        try
        {
            var owner = await f.RegisterOwner(); var other = await f.RegisterOwner();
            var job = new JobPosition();
            Guid[] expected = [];
            await f.Db(async db =>
            {
                var org = await db.Users.Where(x => x.Id == owner.Id).Select(x => x.OrganizationId).SingleAsync();
                var now = DateTimeOffset.UtcNow;
                job.OrganizationId = org; job.Title = "Synthetic pagination role"; job.CreatedAtUtc = now; job.UpdatedAtUtc = now;
                var skill = new Skill { OrganizationId = org, Name = "Synthetic pagination" };
                db.JobPositions.Add(job); db.Skills.Add(skill);
                db.PositionSkills.Add(new PositionSkill { OrganizationId = org, JobPositionId = job.Id, SkillId = skill.Id });
                for (var i = 0; i < 105; i++) db.BankQuestions.Add(new BankQuestion { OrganizationId = org, JobPositionId = job.Id,
                    SkillId = skill.Id, Text = $"Synthetic {i}", Topic = "Synthetic", CreatedAtUtc = now, UpdatedAtUtc = now, IsActive = i % 2 == 0 });
                await db.SaveChangesAsync();
                expected = await db.BankQuestions.Where(x => x.JobPositionId == job.Id).OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Select(x => x.Id).ToArrayAsync();
            });
            var url = $"/api/job-positions/{job.Id}/question-bank";
            var first = await Read<Page<BankQuestionDto>>(await owner.Client.Send(HttpMethod.Get, url + "?pageSize=100"));
            var last = await Read<Page<BankQuestionDto>>(await owner.Client.Send(HttpMethod.Get, url + "?page=2&pageSize=100"));
            Assert.Equal(105, first.Total); Assert.Equal(105, last.Total); Assert.Equal(2, last.PageNumber);
            Assert.Equal(expected, first.Items.Concat(last.Items).Select(x => x.Id));
            var defaults = await Read<Page<BankQuestionDto>>(await owner.Client.Send(HttpMethod.Get, url));
            Assert.Equal(20, defaults.Items.Count); Assert.Equal(20, defaults.PageSize); Assert.Equal(1, defaults.PageNumber);
            var empty = await Read<Page<BankQuestionDto>>(await owner.Client.Send(HttpMethod.Get, url + "?page=2147483647&pageSize=100"));
            Assert.Empty(empty.Items); Assert.Equal(105, empty.Total);
            foreach (var query in new[] { "page=0", "page=-1", "pageSize=0", "pageSize=101" })
                Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.Send(HttpMethod.Get, url + "?" + query)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.Client.Send(HttpMethod.Get, url)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.Send(HttpMethod.Get, $"/api/job-positions/{Guid.NewGuid()}/question-bank")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await f.Browser().Send(HttpMethod.Get, url)).StatusCode);
            foreach (var existing in new[] { "/api/job-positions", "/api/interview-templates", "/api/interviews",
                "/api/team/members", "/api/team/invitations" })
            {
                var page = await Read<Page<System.Text.Json.JsonElement>>(await owner.Client.Send(HttpMethod.Get, existing + "?page=2&pageSize=1"));
                Assert.Equal(2, page.PageNumber); Assert.Equal(1, page.PageSize);
                Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.Send(HttpMethod.Get, existing + "?page=0&pageSize=1")).StatusCode);
            }
        }
        finally { await ((Xunit.IAsyncLifetime)f).DisposeAsync(); }
    }
}
