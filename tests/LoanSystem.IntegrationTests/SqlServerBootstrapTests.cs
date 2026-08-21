using Microsoft.EntityFrameworkCore;
using LoanSystem.Api.Infrastructure;
using Testcontainers.MsSql;
namespace LoanSystem.IntegrationTests;
public sealed class SqlServerBootstrapTests
{
    [Fact] public async Task Application_schema_boots_against_real_sql_server() { if (Environment.GetEnvironmentVariable("CI") is null) return; await using var container = new MsSqlBuilder().Build(); await container.StartAsync(); var options = new DbContextOptionsBuilder<PlatformDbContext>().UseSqlServer(container.GetConnectionString()).Options; await using var db = new PlatformDbContext(options); await db.Database.MigrateAsync(); Assert.True(await db.Database.CanConnectAsync()); Assert.Empty(await db.PlatformMarkers.ToListAsync()); }
}
