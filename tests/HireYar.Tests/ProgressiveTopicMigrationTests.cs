using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class ProgressiveTopicMigrationTests
{
    [PostgresFact]
    public async Task Historical_empty_schema_can_upgrade_without_model_drift()
    {
        var isolated = new ApiFixture { MigrationTarget = "20261010054552_AddInterviewReportReview" }; await isolated.InitializeAsync();
        try
        {
            await isolated.Db(async db => {
                Assert.DoesNotContain("20261010075455_AddProgressiveTopicQuestions", await db.Database.GetAppliedMigrationsAsync());
                await db.Database.MigrateAsync();
                Assert.Contains("20261010075455_AddProgressiveTopicQuestions", await db.Database.GetAppliedMigrationsAsync());
                Assert.False(db.Database.HasPendingModelChanges());
            });
        }
        finally { await ((IAsyncLifetime)isolated).DisposeAsync(); }
    }
}
