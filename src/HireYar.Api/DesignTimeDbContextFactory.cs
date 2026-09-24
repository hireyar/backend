using HireYar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
namespace HireYar.Api;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var cwd = Directory.GetCurrentDirectory();
        var settingsDirectory = File.Exists(Path.Combine(cwd, "HireYar.Api.csproj"))
            ? cwd : Path.Combine(cwd, "src", "HireYar.Api");
        var config = new ConfigurationBuilder()
            .SetBasePath(settingsDirectory)
            .AddJsonFile($"appsettings.{environment}.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connection = config.GetConnectionString("Default") ?? throw new InvalidOperationException("Set ConnectionStrings:Default in local appsettings or ConnectionStrings__Default for EF tooling.");
        return new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    }
}
