using HireYar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace HireYar.Api;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Set ConnectionStrings__Default for EF tooling.");
        return new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options);
    }
}
