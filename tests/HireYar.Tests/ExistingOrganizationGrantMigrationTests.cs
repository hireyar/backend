using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Subscriptions;
using HireYar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class ExistingOrganizationGrantMigrationTests
{
    [PostgresFact]
    public async Task Upgrade_gifts_existing_organizations_once_preserves_paid_and_trial_history_and_excludes_later_signups()
    {
        var f = new ApiFixture { MigrationTarget = "20261019085750_AddSignupTrial" };
        try
        {
            await f.InitializeAsync();
            var empty = await f.RegisterOwner(provisionMembership: false);
            var paid = await f.RegisterOwner();
            var trial = await f.RegisterOwner(provisionMembership: false);
            Guid emptyOrg = default, paidOrg = default, trialOrg = default;
            DateTimeOffset paidEnd = default, trialEnd = default;
            await f.Db(async db => {
                emptyOrg = (await db.Users.SingleAsync(u => u.Id == empty.Id)).OrganizationId;
                paidOrg = (await db.Users.SingleAsync(u => u.Id == paid.Id)).OrganizationId;
                trialOrg = (await db.Users.SingleAsync(u => u.Id == trial.Id)).OrganizationId;
                paidEnd = (await db.OrganizationMemberships.SingleAsync(m => m.OrganizationId == paidOrg)).ExpiresAtUtc;
                var now = f.Clock.GetUtcNow(); trialEnd = now.AddDays(2);
                db.SignupTrials.Add(new() { OrganizationId = trialOrg, StartedAtUtc = now, ExpiresAtUtc = trialEnd, TeamUsers = 5, RetentionDays = 60 });
                await db.SaveChangesAsync();
                trialEnd = (await db.SignupTrials.AsNoTracking().SingleAsync(t => t.OrganizationId == trialOrg)).ExpiresAtUtc;
                var oldVersion = await db.Database.SqlQueryRaw<byte[]>("SELECT \"RowVersion\" AS \"Value\" FROM \"SubscriptionPlans\"").SingleAsync();
                // Keep an existing customized price; migration may only initialize the new discount fields.
                await db.Database.ExecuteSqlRawAsync("UPDATE \"SubscriptionPlans\" SET \"MonthlyPriceRial\" = 91000000");
                await db.Database.OpenConnectionAsync();
                try
                {
                    await db.Database.ExecuteSqlRawAsync("SET TIME ZONE 'Asia/Tehran'");
                    var sql = db.GetService<IMigrator>().GenerateScript(f.MigrationTarget, null, MigrationsSqlGenerationOptions.Idempotent);
                    // Generated SQL can contain literal JSON braces; execute without EF format parsing.
                    await using var command = db.Database.GetDbConnection().CreateCommand();
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync(); await command.ExecuteNonQueryAsync();
                }
                finally { await db.Database.CloseConnectionAsync(); }
                db.ChangeTracker.Clear();
                var grants = await db.ExistingOrganizationGrants.OrderBy(g => g.OrganizationId).ToListAsync();
                Assert.Equal(3, grants.Count); Assert.All(grants, g => { Assert.Equal(100,g.InterviewQuantity); Assert.Equal(g.StartedAtUtc.AddMonths(1),g.ExpiresAtUtc); });
                var emptyGift = grants.Single(g => g.OrganizationId == emptyOrg);
                Assert.Equal(emptyGift.CreatedAtUtc, emptyGift.StartedAtUtc);
                var paidGift = grants.Single(g => g.OrganizationId == paidOrg);
                Assert.Equal(paidEnd,paidGift.StartedAtUtc); Assert.True(paidGift.UnlimitedTeam); Assert.Equal(30,paidGift.TeamUsers);
                Assert.Equal(trialEnd,grants.Single(g => g.OrganizationId == trialOrg).StartedAtUtc);
                Assert.Single(await db.OrganizationMemberships.ToListAsync()); Assert.Single(await db.SignupTrials.ToListAsync());
                Assert.Equal(2,await db.SubscriptionOrders.CountAsync()); Assert.Equal(1000,(await db.InterviewQuotaGrants.SingleAsync()).Quantity);
                var plan = await db.SubscriptionPlans.AsNoTracking().SingleAsync(); Assert.Equal(91_000_000,plan.MonthlyPriceRial);
                Assert.Equal(5,plan.ThreeMonthDiscountPercent); Assert.Equal(10,plan.SixMonthDiscountPercent); Assert.Equal(15,plan.TwelveMonthDiscountPercent);
                Assert.False(oldVersion.SequenceEqual(plan.RowVersion)); Assert.False(db.Database.HasPendingModelChanges());
                await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"ExistingOrganizationGrants\" SET \"InterviewQuantity\" = 200 WHERE \"OrganizationId\" = {emptyOrg}"));
                await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO \"ExistingOrganizationGrants\" SELECT * FROM \"ExistingOrganizationGrants\" WHERE \"OrganizationId\" = {emptyOrg}"));
            });
            async Task<SubscriptionStateDto> State(Account owner)
            {
                var response = await owner.Client.Send(HttpMethod.Get,"/api/subscriptions/current"); Assert.Equal(HttpStatusCode.OK,response.StatusCode);
                return (await response.Content.ReadFromJsonAsync<SubscriptionStateDto>(TestJson.Options))!;
            }
            Assert.Equal(100,(await State(empty)).AvailableInterviews); Assert.False((await State(empty)).IsTrial);
            Assert.Equal(1100,(await State(paid)).AvailableInterviews); Assert.Equal(paidEnd.AddMonths(1),(await State(paid)).ExpiresAtUtc);
            Assert.Equal(102,(await State(trial)).AvailableInterviews); Assert.True((await State(trial)).IsTrial);
            var later = await f.RegisterOwner(explicitSignup: true,provisionMembership: false);
            Assert.Equal(2,(await State(later)).AvailableInterviews);
            await f.Db(async db => {
                Assert.Equal(3,await db.ExistingOrganizationGrants.CountAsync());
                var rollback = db.GetService<IMigrator>().GenerateScript("20261019111848_GrantExistingOrganizationsMonthAndInterviews",f.MigrationTarget);
                await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(rollback));
            });
        }
        finally { await ((IAsyncLifetime)f).DisposeAsync(); }
    }
}
