using HireYar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;

namespace HireYar.Tests;

public sealed class RialCreditCutoverTests
{
    [PostgresFact]
    public async Task Legacy_usd_ledger_requires_explicit_rate_and_converts_atomically_with_private_usage_audit()
    {
        var raw = Environment.GetEnvironmentVariable("HIREYAR_TEST_POSTGRES")!;
        var settings = new NpgsqlConnectionStringBuilder(raw);
        Assert.Contains(settings.Host, new[] { "127.0.0.1", "localhost", "::1" });
        settings.Database = "hireyar_cutover_" + Guid.NewGuid().ToString("N");
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(settings.ConnectionString).Options);
        try
        {
            var previous = db.Database.GetMigrations().Last(x => !x.EndsWith("_PersistRialAiCredit",StringComparison.Ordinal));
            await db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync(previous);
            var org=Guid.NewGuid(); var wallet=Guid.NewGuid(); var credit=Guid.NewGuid(); var usage=Guid.NewGuid(); var now=DateTimeOffset.UtcNow;
            await using(var tx=await db.Database.BeginTransactionAsync())
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Organizations" ("Id","Name","CreatedAtUtc","UpdatedAtUtc") VALUES ({org},'Synthetic',{now},{now});
                    INSERT INTO "OrganizationAiWallets" ("Id","OrganizationId","Currency","Balance","CreatedAtUtc","UpdatedAtUtc")
                        VALUES ({wallet},{org},'USD',9.75,{now},{now});
                    INSERT INTO "AiCreditTransactions" ("Id","OrganizationId","WalletId","Type","Amount","BalanceBefore","BalanceAfter","ReferenceType","ReferenceId","CreatedAtUtc")
                        VALUES ({credit},{org},{wallet},'AdminAdjustment',10,0,10,'AdminAdjustment',{credit},{now}),
                               ({usage},{org},{wallet},'InterviewUsage',-0.25,10,9.75,'Interview',{usage},{now});
                    """);
                await tx.CommitAsync();
            }
            var failure=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.MigrateAsync());
            Assert.Contains("ai_credit_cutover_rial_per_usd",failure.MessageText);
            await db.Database.OpenConnectionAsync();
            await db.Database.ExecuteSqlRawAsync("SET hireyar.ai_credit_cutover_rial_per_usd = '650000'");
            var script = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().GenerateScript(previous,
                "20261017195309_PersistRialAiCredit", Microsoft.EntityFrameworkCore.Migrations.MigrationsSqlGenerationOptions.Idempotent);
            await db.Database.ExecuteSqlRawAsync(script);
            var converted=await db.OrganizationAiWallets.SingleAsync(); Assert.Equal("IRR",converted.Currency); Assert.Equal(6337500m,converted.Balance);
            var consumed=await db.AiCreditTransactions.SingleAsync(x=>x.Id==usage);
            Assert.Equal(-162500m,consumed.Amount); Assert.Equal(-.25m,consumed.AmountUsd);
            Assert.Equal(converted.Balance,await db.AiCreditTransactions.SumAsync(x=>x.Amount));
            Assert.False(db.Database.HasPendingModelChanges());
            await db.Database.ExecuteSqlRawAsync("SET hireyar.ai_credit_cutover_rial_per_usd = '900000'");
            await db.Database.ExecuteSqlRawAsync(script);
            Assert.Equal(6337500m,(await db.OrganizationAiWallets.AsNoTracking().SingleAsync()).Balance);
            await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlRawAsync("UPDATE \"AiCreditTransactions\" SET \"AmountUsd\"=0"));
        }
        finally { await db.Database.CloseConnectionAsync(); await db.Database.EnsureDeletedAsync(); }
    }
}
