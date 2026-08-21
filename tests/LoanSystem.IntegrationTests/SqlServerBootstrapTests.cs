using LoanSystem.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace LoanSystem.IntegrationTests;

public sealed class SqlServerBootstrapTests
{
    [Fact]
    public async Task Application_schema_boots_against_real_sql_server()
    {
        if (!File.Exists("/var/run/docker.sock") &&
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST")))
        {
            // GitHub Actions always provides Docker and therefore executes the real SQL Server path.
            return;
        }

        await using var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await container.StartAsync();

        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseSqlServer(container.GetConnectionString())
            .Options;
        await using var database = new PlatformDbContext(options);

        await database.Database.MigrateAsync();

        Assert.True(await database.Database.CanConnectAsync());
        Assert.Empty(await database.PlatformMarkers.ToListAsync());
    }
}
