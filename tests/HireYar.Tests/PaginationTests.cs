using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Billing;
using HireYar.Application.Common;
using HireYar.Application.Interviews;
using HireYar.Domain.Billing;
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
        return (await response.Content.ReadFromJsonAsync<T>(BillingJson.Options))!;
    }

    [PostgresFact]
    public async Task Admin_catalog_pages_reach_beyond_100_with_stable_order_and_validation()
    {
        var f = new ApiFixture();
        await ((Xunit.IAsyncLifetime)f).InitializeAsync();
        try
        {
            var admin = await f.RegisterAdmin();
            await f.Db(async db =>
            {
                var now = DateTimeOffset.UtcNow;
                for (var i = 0; i < 105; i++)
                {
                    db.PricingPackages.Add(new PricingPackage { Code = $"page_{i}", Name = $"Synthetic {i}",
                        AiPricePerMinuteUsd = .03m, InterviewRetentionDays = 30, CreatedAtUtc = now, UpdatedAtUtc = now,
                        SortOrder = 7, IsActive = i % 2 == 0, IsPublic = false });
                    db.AiCreditPackages.Add(new AiCreditPackage { Name = $"Synthetic {i}", CreditAmountUsd = 1,
                        SellPriceAmount = 100, SortOrder = 7, IsActive = i % 2 == 0, IsPublic = false,
                        CreatedAtUtc = now, UpdatedAtUtc = now });
                }
                await db.SaveChangesAsync();
            });
            foreach (var route in new[] { "packages", "ai-credit-packages" })
            {
                var url = "/api/admin/pricing/" + route;
                // Inspect common fields without depending on either catalog's item shape.
                var first = await Read<Page<System.Text.Json.JsonElement>>(await admin.Client.Send(HttpMethod.Get, url + "?page=1&pageSize=100"));
                var second = await Read<Page<System.Text.Json.JsonElement>>(await admin.Client.Send(HttpMethod.Get, url + "?page=2&pageSize=100"));
                Guid[] expected = [];
                await f.Db(async db => expected = route == "packages"
                    ? await db.PricingPackages.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(x => x.Id).ToArrayAsync()
                    : await db.AiCreditPackages.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
                Assert.Equal(expected.Length, first.Total);
                Assert.Equal(first.Total, second.Total);
                Assert.Equal(100, first.Items.Count);
                Assert.Equal(expected, first.Items.Concat(second.Items).Select(x => x.GetProperty("id").GetGuid()));
                Assert.Contains(first.Items.Concat(second.Items), x => !x.GetProperty("isActive").GetBoolean());
                var defaults = await Read<Page<System.Text.Json.JsonElement>>(await admin.Client.Send(HttpMethod.Get, url));
                Assert.Equal(1, defaults.PageNumber); Assert.Equal(20, defaults.PageSize); Assert.Equal(20, defaults.Items.Count);
                var empty = await Read<Page<System.Text.Json.JsonElement>>(await admin.Client.Send(HttpMethod.Get, url + "?page=999&pageSize=100"));
                Assert.Empty(empty.Items); Assert.Equal(first.Total, empty.Total);
                foreach (var query in new[] { "page=0", "page=-1", "pageSize=0", "pageSize=101" })
                    Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.Send(HttpMethod.Get, url + "?" + query)).StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized, (await f.Browser().Send(HttpMethod.Get, url)).StatusCode);
                var owner = await f.RegisterOwner();
                Assert.Equal(HttpStatusCode.Unauthorized, (await owner.Client.Send(HttpMethod.Get, url)).StatusCode);
            }
            // Public catalogs retain their original envelope and visibility rules.
            var catalog = await Read<PricingResponse>(await f.Browser().Send(HttpMethod.Get, "/api/pricing/packages"));
            Assert.DoesNotContain(catalog.Packages, x => x.Code.StartsWith("page_"));
            var credits = await Read<System.Text.Json.JsonElement>(await f.Browser().Send(HttpMethod.Get, "/api/pricing/ai-credit-packages"));
            Assert.Equal(System.Text.Json.JsonValueKind.Array, credits.GetProperty("packages").ValueKind);
        }
        finally { await ((Xunit.IAsyncLifetime)f).DisposeAsync(); }
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
                "/api/team/members", "/api/team/invitations", "/api/billing/payments", "/api/billing/purchases",
                "/api/billing/ai-credit/transactions", "/api/billing/ai-usage" })
            {
                var page = await Read<Page<System.Text.Json.JsonElement>>(await owner.Client.Send(HttpMethod.Get, existing + "?page=2&pageSize=1"));
                Assert.Equal(2, page.PageNumber); Assert.Equal(1, page.PageSize);
                Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.Send(HttpMethod.Get, existing + "?page=0&pageSize=1")).StatusCode);
            }
        }
        finally { await ((Xunit.IAsyncLifetime)f).DisposeAsync(); }
    }
}
