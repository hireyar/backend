using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Auth;
using HireYar.Application.Common;
using HireYar.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class AdminManagementTests(ApiFixture f)
{
    private const string Route = "/api/admin/users/super-admins";

    [PostgresFact]
    public async Task List_search_and_revoke_preserve_owner_and_invalidate_admin_sessions()
    {
        var actor = await f.RegisterAdmin(); var target = await f.RegisterAdmin(); var owner = await f.RegisterOwner();
        foreach (var browser in new[] { f.Browser(), owner.Client })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Send(HttpMethod.Get, Route)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Send(HttpMethod.Delete, Route + "/" + target.Id)).StatusCode);
        }
        var response = await actor.Client.Send(HttpMethod.Get, Route + "?page=1&pageSize=1&search=" + target.Mobile);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content.ReadFromJsonAsync<Page<AdminUserDto>>(BillingJson.Options))!;
        Assert.Equal(1, page.Total); Assert.Equal(target.Id, Assert.Single(page.Items).Id);
        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stamp", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await actor.Client.Send(HttpMethod.Get, Route + "?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await actor.Client.Send(HttpMethod.Get, Route + "?search=" + new string('a', 257))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await actor.Client.Send(HttpMethod.Delete, Route + "/" + actor.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await actor.Client.Send(HttpMethod.Delete, Route + "/" + owner.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await actor.Client.Send(HttpMethod.Delete, Route + "/" + target.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await target.Client.Send(HttpMethod.Get, "/api/admin/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await target.Client.Send(HttpMethod.Get, Route)).StatusCode);
        await f.SignInAsync(target.Client, target.Mobile);
        Assert.Equal(HttpStatusCode.OK, (await target.Client.Send(HttpMethod.Get, "/api/auth/me")).StatusCode);
        await f.Db(async db => { var u = await db.Users.SingleAsync(x => x.Id == target.Id); Assert.False(u.IsSystemAdmin); Assert.True(u.IsActive); });
        Assert.Equal(HttpStatusCode.NotFound, (await actor.Client.Send(HttpMethod.Delete, Route + "/" + target.Id)).StatusCode);
    }

    [PostgresFact]
    public async Task Reciprocal_revocations_leave_one_administrator()
    {
        var first = await f.RegisterAdmin(); var second = await f.RegisterAdmin();
        var responses = await Task.WhenAll(first.Client.Send(HttpMethod.Delete, Route + "/" + second.Id),
            second.Client.Send(HttpMethod.Delete, Route + "/" + first.Id));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Unauthorized);
        await f.Db(async db => Assert.Equal(1, await db.Users.CountAsync(x => (x.Id == first.Id || x.Id == second.Id) && x.IsSystemAdmin)));
    }

    [PostgresFact]
    public async Task Recreated_browser_can_restore_unexpired_admin_token_with_entry_header()
    {
        var admin = await f.RegisterAdmin();
        var reloaded = f.Browser(); reloaded.AccessToken = admin.Client.AccessToken; reloaded.AdminEntryKey = ApiFixture.AdminEntry;
        Assert.Equal(HttpStatusCode.OK, (await reloaded.Send(HttpMethod.Get, "/api/admin/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reloaded.Send(HttpMethod.Get, "/api/admin/pricing/packages")).StatusCode);
        reloaded.AdminEntryKey = null;
        Assert.Equal(HttpStatusCode.NotFound, (await reloaded.Send(HttpMethod.Get, "/api/admin/auth/me")).StatusCode);
    }

    [PostgresFact]
    public async Task Catalog_search_filters_before_count_and_pagination_and_treats_symbols_literally()
    {
        var admin = await f.RegisterAdmin();
        var marker = "Search" + Guid.NewGuid().ToString("N");
        await f.Db(async db => {
            var now = DateTimeOffset.UtcNow;
            for (var i = 0; i < 3; i++) {
                db.PricingPackages.Add(new PricingPackage { Code = "search_" + Guid.NewGuid().ToString("N"), Name = i == 0 ? marker : "سازمانی",
                    Description = i == 0 ? "" : marker, AiPricePerMinuteUsd = .03m, InterviewRetentionDays = 30, CreatedAtUtc = now, UpdatedAtUtc = now });
                db.AiCreditPackages.Add(new AiCreditPackage { Name = marker + " اعتبار " + i, CreditAmountRial = 1,
                    SellPriceAmount = 100, CreatedAtUtc = now, UpdatedAtUtc = now });
            }
            await db.SaveChangesAsync();
        });
        foreach (var route in new[] { "/api/admin/pricing/packages", "/api/admin/pricing/ai-credit-packages" }) {
            var response = await admin.Client.Send(HttpMethod.Get, route + "?page=2&pageSize=2&search=" + Uri.EscapeDataString(" " + marker.ToUpperInvariant() + " "));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var page = (await response.Content.ReadFromJsonAsync<Page<System.Text.Json.JsonElement>>(BillingJson.Options))!;
            Assert.Equal(3, page.Total); Assert.Equal(2, page.PageNumber); Assert.Single(page.Items);
            var none = await admin.Client.Send(HttpMethod.Get, route + "?search=%25");
            Assert.Equal(0, (await none.Content.ReadFromJsonAsync<Page<System.Text.Json.JsonElement>>(BillingJson.Options))!.Total);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.Send(HttpMethod.Get, route + "?search=" + new string('a', 257))).StatusCode);
        }
    }
}
