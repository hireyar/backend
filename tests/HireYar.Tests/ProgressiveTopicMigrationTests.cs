using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class ProgressiveTopicMigrationTests
{
    [PostgresFact]
    public async Task Empty_schema_can_downgrade_and_upgrade_without_model_drift()
    {
        var isolated = new ApiFixture(); await isolated.InitializeAsync();
        try
        {
            await isolated.Db(async db => {
                await db.GetService<IMigrator>().MigrateAsync("20261010054552_AddInterviewReportReview");
                Assert.DoesNotContain("20261010075455_AddProgressiveTopicQuestions", await db.Database.GetAppliedMigrationsAsync());
                await db.Database.MigrateAsync();
                Assert.Contains("20261010075455_AddProgressiveTopicQuestions", await db.Database.GetAppliedMigrationsAsync());
                Assert.False(db.Database.HasPendingModelChanges());
            });
        }
        finally { await ((IAsyncLifetime)isolated).DisposeAsync(); }
    }
}
