using Microsoft.EntityFrameworkCore;
namespace LoanSystem.Api.Infrastructure;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public DbSet<PlatformMarker> PlatformMarkers => Set<PlatformMarker>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("platform");
        modelBuilder.Entity<PlatformMarker>(entity => { entity.HasKey(x => x.Id); entity.Property(x => x.Name).HasMaxLength(100); });
    }
}
public sealed class PlatformMarker { public int Id { get; set; } public string Name { get; set; } = string.Empty; }
public sealed class SqlServerHealthCheck(IServiceScopeFactory scopeFactory) : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await database.Database.CanConnectAsync(cancellationToken) ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy() : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("SQL Server is unavailable");
    }
}
