using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Organizations;
using HireYar.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Tests;

public sealed partial class OrganizationAccessTests
{
    private async Task<AccessRoleDto> SaveRole(Account system, string code, params SectionPermission[] grants)
    {
        var result = await system.Client.Send(HttpMethod.Put, "/api/admin/access/roles",
            new SaveAccessRoleRequest(code, "نقش آزمایشی", "تنظیم آزاد مجوزها", true, false, null, grants));
        Assert.True(result.IsSuccessStatusCode, await result.Content.ReadAsStringAsync());
        return (await result.Content.ReadFromJsonAsync<AccessRoleDto>(TestJson.Options))!;
    }
    [PostgresTheory]
    [InlineData("admin", SectionAccess.None)]
    [InlineData("admin", SectionAccess.Read)]
    [InlineData("admin", SectionAccess.Write)]
    [InlineData("viewer", SectionAccess.None)]
    [InlineData("viewer", SectionAccess.Read)]
    [InlineData("viewer", SectionAccess.Write)]
    [InlineData("interview_manager", SectionAccess.None)]
    [InlineData("interview_manager", SectionAccess.Read)]
    [InlineData("interview_manager", SectionAccess.Write)]
    public async Task Superadmin_can_freely_configure_all_sections_of_every_builtin_role(string code, SectionAccess level)
    {
        var system = await f.RegisterAdmin(); var owner = await f.RegisterOwner(); var (member, memberId) = await Member(owner, code);
        var all = (await (await system.Client.Send(HttpMethod.Get, "/api/admin/access/roles")).Content.ReadFromJsonAsync<List<AccessRoleDto>>(TestJson.Options))!;
        var before = all.Single(x => x.Code == code);
        var request = new SaveAccessRoleRequest(code, before.Name, before.Description, true, false, before.Revision,
            AccessSections.Codes.Select(x => new SectionPermission(x, level)).ToArray());
        var response = await system.Client.Send(HttpMethod.Put, "/api/admin/access/roles", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = (await response.Content.ReadFromJsonAsync<AccessRoleDto>(TestJson.Options))!;
        Assert.False(after.IsAdministrator); Assert.Equal(level != SectionAccess.None, after.RequiresOwnerAssignment);
        Assert.Equal(10, after.Sections.Count); Assert.All(after.Sections, x => Assert.Equal(level, x.Access));
        foreach (var path in new[] { "/api/organization", "/api/subscriptions/current", "/api/team/members" })
            Assert.Equal(level == SectionAccess.None ? HttpStatusCode.Forbidden : HttpStatusCode.OK,
                (await member.Send(HttpMethod.Get, path)).StatusCode);
        Assert.Equal(level == SectionAccess.Write ? HttpStatusCode.OK : HttpStatusCode.Forbidden,
            (await member.Send(HttpMethod.Put, "/api/organization", new { name = "سازمان آزمایشی" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.Send(HttpMethod.Get, "/api/admin/access/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.Send(HttpMethod.Get, "/api/sales/leads")).StatusCode);
        await f.Db(async db => {
            var u = await db.Users.SingleAsync(x => x.Id == memberId);
            Assert.False(u.IsSystemAdmin); Assert.Equal(OrganizationRole.Recruiter, u.Role);
            Assert.True(await db.AccessAudits.AnyAsync(x => x.ActorId == system.Id && x.Action == "RoleSaved"));
        });
        // Ownership stays independent of the editable admin role and cannot be demoted through the team API.
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.Send(HttpMethod.Get, "/api/team/members")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.Send(HttpMethod.Put, $"/api/team/members/{owner.Id}/access", new ChangeMemberAccessRequest("viewer"))).StatusCode);
    }
    [PostgresFact]
    public async Task Delegation_uses_real_permissions_and_cannot_escalate_via_invite_role_change_or_deactivation()
    {
        var system = await f.RegisterAdmin(); var owner = await f.RegisterOwner(); var other = await f.RegisterOwner();
        var delegated = await SaveRole(system, "delegated_team", new("team", SectionAccess.Write), new("jobs", SectionAccess.Read));
        var stronger = await SaveRole(system, "stronger_jobs", new SectionPermission("jobs", SectionAccess.Write));
        var weaker = await SaveRole(system, "weaker_jobs", new SectionPermission("jobs", SectionAccess.Read));
        var sensitive = await SaveRole(system, "sensitive_read", new SectionPermission("settings", SectionAccess.Read));
        Assert.True(delegated.RequiresOwnerAssignment); Assert.True(sensitive.RequiresOwnerAssignment);
        Assert.False(delegated.IsAdministrator); Assert.False(sensitive.IsAdministrator);
        var (actor, actorId) = await Member(owner, delegated.Code);
        var (_, lowId) = await Member(owner, weaker.Code); var (_, highId) = await Member(owner, stronger.Code);
        var (_, protectedId) = await Member(owner, sensitive.Code); var (_, foreignId) = await Member(other, weaker.Code);
        Assert.Equal(HttpStatusCode.Created, (await actor.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(Mobile(), "عضو", "تست", weaker.Code, Email: "invite@example.test"))).StatusCode);
        foreach (var role in new[] { stronger.Code, delegated.Code, sensitive.Code }) {
            Assert.Equal(HttpStatusCode.Forbidden, (await actor.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(Mobile(), "عضو", "تست", role, Email: "invite@example.test"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await actor.Send(HttpMethod.Put, $"/api/team/members/{lowId}/access", new ChangeMemberAccessRequest(role))).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await actor.Send(HttpMethod.Put, $"/api/team/members/{lowId}/access", new ChangeMemberAccessRequest(weaker.Code))).StatusCode);
        foreach (var id in new[] { highId, protectedId, actorId }) {
            Assert.Equal(HttpStatusCode.Forbidden, (await actor.Send(HttpMethod.Post, $"/api/team/members/{id}/deactivate")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await actor.Send(HttpMethod.Put, $"/api/team/members/{id}/access", new ChangeMemberAccessRequest(weaker.Code))).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await actor.Send(HttpMethod.Put, $"/api/team/members/{foreignId}/access", new ChangeMemberAccessRequest(weaker.Code))).StatusCode);
        var protectedInviteResponse = await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(Mobile(), "عضو", "تست", sensitive.Code, "synthetic@example.test"));
        var pending = (await protectedInviteResponse.Content.ReadFromJsonAsync<TeamInvitationDto>(TestJson.Options))!;
        Assert.Equal(HttpStatusCode.Forbidden, (await actor.Send(HttpMethod.Delete, $"/api/team/invitations/{pending.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await actor.Send(HttpMethod.Post, $"/api/team/invitations/{pending.Id}/resend")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await actor.Send(HttpMethod.Post, "/api/panel-wizard/positions", new { title = "Denied", description = "Denied" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await actor.Send(HttpMethod.Post, $"/api/team/members/{lowId}/deactivate")).StatusCode);
        // Removing the actor's team permission takes effect on the very same access token.
        Assert.Equal(HttpStatusCode.OK, (await system.Client.Send(HttpMethod.Put, "/api/admin/access/roles",
            new SaveAccessRoleRequest(delegated.Code, delegated.Name, delegated.Description, true, false, delegated.Revision, [new("jobs", SectionAccess.Read)]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await actor.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(Mobile(), "عضو", "تست", weaker.Code, Email: "invite@example.test"))).StatusCode);
        await f.Db(async db => {
            Assert.Equal(weaker.Code, (await db.Users.SingleAsync(x => x.Id == lowId)).AccessRoleCode);
            Assert.Equal(stronger.Code, (await db.Users.SingleAsync(x => x.Id == highId)).AccessRoleCode);
            Assert.True((await db.Users.SingleAsync(x => x.Id == highId)).IsActive);
            Assert.Null((await db.MobileTeamInvitations.SingleAsync(x => x.Id == pending.Id)).CancelledAtUtc);
        });
    }
    [PostgresFact]
    public async Task Clearing_administrator_flag_cannot_bypass_owner_protection_even_with_a_full_permission_ceiling()
    {
        var system = await f.RegisterAdmin(); var owner = await f.RegisterOwner(); var (actor, actorId) = await Member(owner, "viewer");
        var role = (await (await system.Client.Send(HttpMethod.Get, "/api/admin/access/roles")).Content.ReadFromJsonAsync<List<AccessRoleDto>>(TestJson.Options))!.Single(x => x.Code == "viewer");
        var response = await system.Client.Send(HttpMethod.Put, "/api/admin/access/roles",
            new SaveAccessRoleRequest(role.Code, role.Name, role.Description, true, false, role.Revision,
                AccessSections.Codes.Select(x => new SectionPermission(x, SectionAccess.Write)).ToArray()));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<AccessRoleDto>(TestJson.Options))!;
        Assert.False(saved.IsAdministrator); Assert.True(saved.RequiresOwnerAssignment);
        var (_, peerId) = await Member(owner, "viewer");
        var weaker = await SaveRole(system, "weaker_jobs", new SectionPermission("jobs", SectionAccess.Read));
        Assert.Equal(HttpStatusCode.Forbidden, (await actor.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(Mobile(), "عضو", "تست", "viewer", Email: "invite@example.test"))).StatusCode);
        foreach (var id in new[] { actorId, peerId }) {
            Assert.Equal(HttpStatusCode.Forbidden, (await actor.Send(HttpMethod.Put, $"/api/team/members/{id}/access", new ChangeMemberAccessRequest(weaker.Code))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await actor.Send(HttpMethod.Post, $"/api/team/members/{id}/deactivate")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await actor.Send(HttpMethod.Put, "/api/organization", new { name = "اختیار مجاز" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await actor.Send(HttpMethod.Get, "/api/admin/access/roles")).StatusCode);
        // Replacing an expired protected invitation must not implicitly cancel it through the invite endpoint.
        var mobile = Mobile();
        var invitation = (await (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(mobile, "عضو", "تست", "viewer", Email: "invite@example.test"))).Content.ReadFromJsonAsync<TeamInvitationDto>(TestJson.Options))!;
        await f.Db(async db => {
            var row = await db.MobileTeamInvitations.SingleAsync(x => x.Id == invitation.Id);
            row.CreatedAtUtc = f.Clock.GetUtcNow().AddDays(-4); row.ExpiresAtUtc = f.Clock.GetUtcNow().AddDays(-1); await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Forbidden, (await actor.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(mobile, "عضو", "تست", weaker.Code, Email: "invite@example.test"))).StatusCode);
        await f.Db(async db => Assert.Null((await db.MobileTeamInvitations.SingleAsync(x => x.Id == invitation.Id)).CancelledAtUtc));
        Assert.Equal(HttpStatusCode.Created, (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(mobile, "عضو", "تست", weaker.Code, Email: "invite@example.test"))).StatusCode);
    }
    [PostgresFact]
    public async Task Concurrent_actor_demotion_cannot_use_earlier_request_claims_to_invite()
    {
        var system = await f.RegisterAdmin(); var owner = await f.RegisterOwner();
        var delegated = await SaveRole(system, "delegated_team", new("team", SectionAccess.Write), new("jobs", SectionAccess.Read));
        var low = await SaveRole(system, "low_jobs", new SectionPermission("jobs", SectionAccess.Read));
        var (actor, actorId) = await Member(owner, delegated.Code); var mobile = Mobile();
        await f.Db(async db => {
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock_shared(1213813313, 1)");
            var org = await db.Users.Where(x => x.Id == actorId).Select(x => x.OrganizationId).SingleAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Organizations\" WHERE \"Id\"={org} FOR UPDATE");
            var invitation = actor.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(mobile, "عضو", "تست", low.Code, Email: "invite@example.test"));
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (true) {
                await db.Database.ExecuteSqlRawAsync("SELECT pg_stat_clear_snapshot()", deadline.Token);
                if (await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND query LIKE '%Organizations%'").SingleAsync(deadline.Token) > 0) break;
                await Task.Delay(20, deadline.Token);
            }
            // Simulate the owner committing a role downgrade while the already authenticated request waits on the org.
            await db.Users.Where(x => x.Id == actorId).ExecuteUpdateAsync(x => x.SetProperty(u => u.AccessRoleCode, low.Code));
            await tx.CommitAsync();
            Assert.Equal(HttpStatusCode.Forbidden, (await invitation).StatusCode);
        });
        await f.Db(async db => Assert.False(await db.MobileTeamInvitations.AnyAsync(x => x.Mobile == mobile)));
    }
    [PostgresFact]
    public async Task Catalog_edits_wait_for_inflight_team_catalog_snapshot_then_apply_to_existing_session()
    {
        var system = await f.RegisterAdmin(); var owner = await f.RegisterOwner(); var (member, _) = await Member(owner, "viewer");
        var role = (await (await system.Client.Send(HttpMethod.Get, "/api/admin/access/roles")).Content.ReadFromJsonAsync<List<AccessRoleDto>>(TestJson.Options))!.Single(x => x.Code == "viewer");
        var section = (await (await system.Client.Send(HttpMethod.Get, "/api/admin/access/sections")).Content.ReadFromJsonAsync<List<AccessSectionDto>>(TestJson.Options))!.Single(x => x.Code == "jobs");
        await f.Db(async db => {
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock_shared(1213813313, 1)");
            var roleEdit = system.Client.Send(HttpMethod.Put, "/api/admin/access/roles",
                new SaveAccessRoleRequest(role.Code, role.Name, role.Description, true, false, role.Revision, [new("settings", SectionAccess.Write), new("jobs", SectionAccess.Read)]));
            var sectionEdit = system.Client.Send(HttpMethod.Put, "/api/admin/access/sections/jobs",
                new UpdateAccessSectionRequest(section.Name, section.Description, false, section.Revision));
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_locks WHERE locktype='advisory' AND classid=1213813313 AND objid=1 AND objsubid=2 AND NOT granted AND database=(SELECT oid FROM pg_database WHERE datname=current_database())").SingleAsync(deadline.Token) < 2)
                await Task.Delay(20, deadline.Token);
            Assert.False(roleEdit.IsCompleted); Assert.False(sectionEdit.IsCompleted);
            await tx.CommitAsync();
            Assert.Equal(HttpStatusCode.OK, (await roleEdit).StatusCode); Assert.Equal(HttpStatusCode.OK, (await sectionEdit).StatusCode);
        });
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Send(HttpMethod.Get, "/api/job-positions")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await member.Send(HttpMethod.Put, "/api/organization", new { name = "تنظیم آزاد" })).StatusCode);
    }
}

public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute() { if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HIREYAR_TEST_POSTGRES"))) Skip = "Requires a local PostgreSQL test connection."; }
}
