using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireYar.Application.Common;
using HireYar.Application.Organizations;
using HireYar.Domain.Organizations;
using HireYar.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
namespace HireYar.Tests;

public sealed partial class MobileTeamTests
{
    [PostgresFact]
    public async Task Invitation_names_are_trimmed_listed_and_applied_only_after_OTP_acceptance()
    {
        var owner = await f.RegisterOwner(); var mobile = Mobile();
        var invitation = await Invite(owner, mobile, " سارا ", " نیک‌نام ");
        Assert.Equal("سارا", invitation.FirstName); Assert.Equal("نیک‌نام", invitation.LastName);
        var list = (await (await owner.Client.Send(HttpMethod.Get, "/api/team/invitations")).Content.ReadFromJsonAsync<Page<TeamInvitationDto>>(TestJson.Options))!;
        var listed = Assert.Single(list.Items);
        Assert.Equal(invitation.Id, listed.Id); Assert.Equal(invitation.Mobile, listed.Mobile);
        Assert.Equal("سارا", listed.FirstName); Assert.Equal("نیک‌نام", listed.LastName); Assert.Equal("Pending", listed.Status);
        await f.Db(async db => { var row = await db.MobileTeamInvitations.SingleAsync(x => x.Id == invitation.Id); Assert.Equal("سارا", row.FirstName); Assert.Equal("نیک‌نام", row.LastName); Assert.False(await db.Users.AnyAsync(x => x.PhoneNumber == mobile)); });
        var member = await Accept(invitation); var me = await Me(member);
        Assert.Equal("سارا نیک‌نام", me.FullName); Assert.Equal(OrganizationRole.Recruiter, me.Role);
        var members = (await (await owner.Client.Send(HttpMethod.Get, "/api/team/members")).Content.ReadFromJsonAsync<Page<TeamMemberDto>>(TestJson.Options))!;
        Assert.Equal("سارا نیک‌نام", members.Items.Single(x => x.Id == me.Id).FullName);
        Assert.Equal(HttpStatusCode.OK, (await member.Send(HttpMethod.Put, "/api/auth/me", new HireYar.Application.Auth.NameRequest("نام ویرایش‌شده"))).StatusCode);
        Assert.Equal("نام ویرایش‌شده", (await Me(member)).FullName);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Post, $"/api/team/members/{me.Id}/deactivate")).StatusCode);
        var next = await Invite(owner, mobile, "لیلا", "نام‌جدید");
        await f.Db(async db => { var user = await db.Users.SingleAsync(x => x.Id == me.Id); Assert.Equal("نام ویرایش‌شده", user.FullName); Assert.False(user.IsActive); var challenge = await db.OtpChallenges.SingleAsync(x => x.Mobile == mobile); challenge.CreatedAtUtc = f.Clock.GetUtcNow().AddMinutes(-2); await db.SaveChangesAsync(); });
        var reactivated = await Accept(next);
        Assert.Equal(me.Id, (await Me(reactivated)).Id); Assert.Equal("لیلا نام‌جدید", (await Me(reactivated)).FullName);
    }

    [PostgresFact]
    public async Task Invitation_requires_both_names_and_rejects_overlong_combined_name_without_creating_rows()
    {
        var owner = await f.RegisterOwner(); var mobile = Mobile();
        var invalid = new object[] {
            new { email = "invite@example.test", mobile }, new { email = "invite@example.test", mobile, firstName = "سارا" }, new { email = "invite@example.test", mobile, lastName = "آزمایشی" },
            new { email = "invite@example.test", mobile, firstName = (string?)null, lastName = "نام" },
            new { email = "invite@example.test", mobile, firstName = "", lastName = "نام" }, new { email = "invite@example.test", mobile, firstName = "نام", lastName = "\t  " },
            new { email = "invite@example.test", mobile, firstName = new string('a', 101), lastName = "نام" },
            new { email = "invite@example.test", mobile, firstName = "نام", lastName = new string('b', 101) },
            new { email = "invite@example.test", mobile, firstName = new string('a', 100), lastName = new string('b', 100) }
        };
        foreach (var request in invalid)
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", request)).StatusCode);
        await f.Db(async db => { Assert.False(await db.MobileTeamInvitations.AnyAsync(x => x.Mobile == mobile)); Assert.False(await db.Users.AnyAsync(x => x.PhoneNumber == mobile)); });
        var valid = await Invite(owner, mobile, new string('a', 100), new string('b', 99));
        Assert.Equal(200, (await Me(await Accept(valid))).FullName.Length);
        var doc = await (await owner.Client.Send(HttpMethod.Get, "/openapi/v1.json")).Content.ReadFromJsonAsync<JsonElement>();
        var schema = doc.GetProperty("paths").GetProperty("/api/team/invitations").GetProperty("post").GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        if (schema.TryGetProperty("$ref", out var reference)) schema = doc.GetProperty("components").GetProperty("schemas").GetProperty(reference.GetString()!.Split('/').Last());
        var required = schema.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("firstName", required); Assert.Contains("lastName", required); Assert.Contains("mobile", required); Assert.Contains("email", required);
    }

    [PostgresFact]
    public async Task Legacy_invites_preserve_existing_names_and_schema_rollback_is_blocked()
    {
        var owner = await f.RegisterOwner(); var organization = (await Me(owner.Client)).OrganizationId;
        var existingMobile = Mobile(); var newMobile = Mobile(); var existingId = Guid.NewGuid();
        var existingInvite = Guid.NewGuid(); var newInvite = Guid.NewGuid(); var now = f.Clock.GetUtcNow();
        await f.Db(async db => {
            db.Users.Add(new ApplicationUser { Id = existingId, UserName = existingMobile, PhoneNumber = existingMobile, FullName = "نام قبلی عضو", OrganizationId = organization,
                Role = OrganizationRole.Recruiter, IsActive = false, PhoneNumberConfirmed = true, CreatedAtUtc = now, UpdatedAtUtc = now });
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"MobileTeamInvitations\" (\"Id\",\"OrganizationId\",\"Mobile\",\"CreatedAtUtc\",\"ExpiresAtUtc\") VALUES ({existingInvite},{organization},{existingMobile},{now},{now.AddDays(3)}), ({newInvite},{organization},{newMobile},{now},{now.AddDays(3)})");
            await db.GetService<IMigrator>().MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            var legacy = await db.MobileTeamInvitations.AsNoTracking().ToListAsync(); Assert.Equal(2, legacy.Count);
            Assert.All(legacy, x => { Assert.Null(x.FirstName); Assert.Null(x.LastName); });
        });
        var list = (await (await owner.Client.Send(HttpMethod.Get, "/api/team/invitations")).Content.ReadFromJsonAsync<Page<TeamInvitationDto>>(TestJson.Options))!;
        var existing = await Me(await Accept(list.Items.Single(x => x.Id == existingInvite)));
        Assert.Equal(existingId, existing.Id); Assert.Equal("نام قبلی عضو", existing.FullName);
        Assert.Equal("کاربر", (await Me(await Accept(list.Items.Single(x => x.Id == newInvite)))).FullName);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Post, $"/api/team/members/{existingId}/deactivate")).StatusCode);
        var named = await Invite(owner, Mobile(), "نام", "دعوت تازه");
        await f.Db(async db => {
            const string previous = "20261013092724_AddMobileTeamInvitations";
            var failure = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(previous));
            Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
            Assert.Equal("دعوت تازه", (await db.MobileTeamInvitations.AsNoTracking().SingleAsync(x => x.Id == named.Id)).LastName);
        });
    }
}
