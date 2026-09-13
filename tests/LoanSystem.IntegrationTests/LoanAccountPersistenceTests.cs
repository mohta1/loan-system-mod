using LoanSystem.Contracts;
using LoanSystem.Modules.LoanAccounts.Application;
using LoanSystem.Modules.LoanAccounts.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace LoanSystem.IntegrationTests;

public sealed class LoanAccountPersistenceTests
{
    [Fact]
    public async Task Open_once_is_idempotent_by_event_and_source_application_and_database_enforces_uniqueness()
    {
        if (!File.Exists("/var/run/docker.sock") && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST")))
            return;

        await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await sql.StartAsync();

        var options = new DbContextOptionsBuilder<LoanAccountsDbContext>()
            .UseSqlServer(sql.GetConnectionString())
            .Options;

        await using var db = new LoanAccountsDbContext(options);
        await db.Database.MigrateAsync();

        var sourceApplicationId = Guid.NewGuid();
        var borrowerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        var service = new LoanAccountService(db);

        var first = new LoanApplicationApprovedV1(
            eventId,
            occurredAt,
            "corr-1",
            sourceApplicationId,
            borrowerId,
            productId,
            productVersionId,
            50000m,
            "OMR",
            "Build",
            occurredAt);

        await service.ConsumeAsync(first);
        await service.ConsumeAsync(first);

        var differentEventSameApplication = first with { EventId = Guid.NewGuid(), CorrelationId = "corr-2" };
        await service.ConsumeAsync(differentEventSameApplication);

        var accounts = await db.LoanAccounts.AsNoTracking().Where(x => x.SourceApplicationId == sourceApplicationId).ToListAsync();
        Assert.Single(accounts);
        Assert.Equal(50000m, accounts[0].ApprovedAmount);
        Assert.Equal(50000m, accounts[0].AvailableToDisburse);
        Assert.Equal(0m, accounts[0].OutstandingBalance);

        var inbox = await db.InboxMessages.AsNoTracking().ToListAsync();
        Assert.Single(inbox);
        Assert.Equal(eventId, inbox[0].EventId);

        var uniqueIndex = await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS [Value] FROM sys.indexes WHERE object_id = OBJECT_ID('loan_accounts.loan_accounts') AND name = 'UX_loan_accounts_source_application_id' AND is_unique = 1")
            .SingleAsync();
        Assert.Equal(1, uniqueIndex);
    }
}
