using LoanSystem.Contracts;
using LoanSystem.Modules.Disbursements.Domain;
using LoanSystem.Modules.Disbursements.Infrastructure;
using LoanSystem.Modules.LoanAccounts.Application;
using LoanSystem.Modules.LoanAccounts.Domain;
using LoanSystem.Modules.LoanAccounts.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace LoanSystem.IntegrationTests;

public sealed class Task12CapacityIntegrationTests
{
    [Fact]
    public void Ef_models_include_owned_financial_entities_and_unique_indexes()
    {
        using var loanDb = new LoanAccountsDbContext(new DbContextOptionsBuilder<LoanAccountsDbContext>().UseSqlServer("Server=localhost;Database=x;User Id=x;Password=x;TrustServerCertificate=True").Options); using var disbursementDb = new DisbursementsDbContext(new DbContextOptionsBuilder<DisbursementsDbContext>().UseSqlServer("Server=localhost;Database=x;User Id=x;Password=x;TrustServerCertificate=True").Options);
        Assert.True(loanDb.Model.FindEntityType(typeof(DisbursementReservation))!.GetIndexes().Single(x => x.Properties.Single().Name == nameof(DisbursementReservation.DisbursementId)).IsUnique); Assert.True(disbursementDb.Model.FindEntityType(typeof(IdempotencyRecord))!.GetIndexes().Single(x => x.Properties.Count == 3).IsUnique);
    }
    [Fact]
    public async Task Concurrent_70000_and_50000_never_overcommit_100000_real_sql()
    {
        if (!DockerAvailable()) return; await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build(); await sql.StartAsync(); var options = new DbContextOptionsBuilder<LoanAccountsDbContext>().UseSqlServer(sql.GetConnectionString()).Options;
        Guid loanId; await using (var seed = new LoanAccountsDbContext(options)) { await seed.Database.MigrateAsync(); var loan = LoanAccount.Open(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100000, "OMR", "Build"); loanId = loan.LoanId; seed.LoanAccounts.Add(loan); await seed.SaveChangesAsync(); }
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); async Task Reserve(decimal amount) { await using var db = new LoanAccountsDbContext(options); await start.Task; await db.ReserveAsync(new(Guid.NewGuid(), DateTimeOffset.UtcNow, "corr", null, Guid.NewGuid(), loanId, amount, "OMR", DateTimeOffset.UtcNow), default); }
        var a = Reserve(70000); var b = Reserve(50000); start.SetResult(); await Task.WhenAll(a, b);
        await using var verify = new LoanAccountsDbContext(options); var loanAccount = await verify.LoanAccounts.AsNoTracking().SingleAsync(); Assert.True(loanAccount.ReservedDisbursementAmount is 70000 or 50000); Assert.True(loanAccount.TotalDisbursed + loanAccount.ReservedDisbursementAmount <= loanAccount.ApprovedAmount); Assert.Single(await verify.Reservations.ToListAsync()); var responses = await verify.OutboxMessages.AsNoTracking().ToListAsync(); Assert.Equal(2, responses.Count); Assert.Single(responses, x => x.EventType == nameof(DisbursementCapacityReservedV1)); Assert.Single(responses, x => x.EventType == nameof(DisbursementCapacityRejectedV1));
    }
    [Fact]
    public async Task Duplicate_event_and_different_event_same_disbursement_reserve_once_and_constraints_exist()
    {
        if (!DockerAvailable()) return; await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build(); await sql.StartAsync(); var options = new DbContextOptionsBuilder<LoanAccountsDbContext>().UseSqlServer(sql.GetConnectionString()).Options; await using var db = new LoanAccountsDbContext(options); await db.Database.MigrateAsync(); var loan = LoanAccount.Open(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100, "OMR", "Build"); db.LoanAccounts.Add(loan); await db.SaveChangesAsync(); var message = new DisbursementCapacityRequestedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, "corr", null, Guid.NewGuid(), loan.LoanId, 40, "OMR", DateTimeOffset.UtcNow); await db.ReserveAsync(message, default); await db.ReserveAsync(message, default); await db.ReserveAsync(message with { EventId = Guid.NewGuid() }, default); Assert.Equal(40, (await db.LoanAccounts.AsNoTracking().SingleAsync()).ReservedDisbursementAmount); Assert.Single(await db.Reservations.AsNoTracking().ToListAsync()); Assert.Equal(2, await db.InboxMessages.CountAsync()); var unique = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.indexes WHERE object_id=OBJECT_ID('loan_accounts.disbursement_reservations') AND name='UX_reservations_disbursement_id' AND is_unique=1").SingleAsync(); Assert.Equal(1, unique);
    }
    [Fact]
    public async Task Disbursement_response_inbox_is_idempotent_and_mismatch_rolls_back()
    {
        if (!DockerAvailable()) return; await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build(); await sql.StartAsync(); var options = new DbContextOptionsBuilder<DisbursementsDbContext>().UseSqlServer(sql.GetConnectionString()).Options; await using var db = new DisbursementsDbContext(options); await db.Database.MigrateAsync(); var loan = Guid.NewGuid(); var value = Disbursement.Create(Guid.NewGuid(), loan, 10, "OMR", new(BeneficiaryType.Contractor, "A", "A", "B", "C"), [], Guid.NewGuid(), "corr", "token", DateTimeOffset.UtcNow); db.Disbursements.Add(value); await db.SaveChangesAsync(); var response = new DisbursementCapacityReservedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, "corr", null, value.DisbursementId, loan, 10, DateTimeOffset.UtcNow, "v"); await db.ApplyReservedAsync(response, default); await db.ApplyReservedAsync(response, default); Assert.Equal(DisbursementStatus.Requested, (await db.Disbursements.AsNoTracking().SingleAsync()).Status); Assert.Single(await db.InboxMessages.ToListAsync()); await Assert.ThrowsAsync<DisbursementResponseConflictException>(() => db.ApplyRejectedAsync(new(Guid.NewGuid(), DateTimeOffset.UtcNow, "corr", null, value.DisbursementId, Guid.NewGuid(), 10, "x", "x", DateTimeOffset.UtcNow), default)); Assert.Single(await db.InboxMessages.ToListAsync());
    }
    [Fact]
    public async Task Financing_type_stale_version_is_rejected_after_reservation_wins()
    {
        if (!DockerAvailable()) return; await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build(); await sql.StartAsync(); var options = new DbContextOptionsBuilder<LoanAccountsDbContext>().UseSqlServer(sql.GetConnectionString()).Options; Guid loanId; byte[] stale;
        await using (var seed = new LoanAccountsDbContext(options)) { await seed.Database.MigrateAsync(); var loan = LoanAccount.Open(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100, "OMR", "Build"); loanId = loan.LoanId; seed.LoanAccounts.Add(loan); await seed.SaveChangesAsync(); stale = loan.RowVersion.ToArray(); }
        await using (var reservation = new LoanAccountsDbContext(options)) await reservation.ReserveAsync(new(Guid.NewGuid(), DateTimeOffset.UtcNow, "corr", null, Guid.NewGuid(), loanId, 10, "OMR", DateTimeOffset.UtcNow), default);
        await using var change = new LoanAccountsDbContext(options); var service = new LoanAccountService(change, new Versions()); await Assert.ThrowsAsync<LoanAccountConcurrencyException>(() => service.ChangeFinancingTypeAsync(loanId, "Purchase", Guid.NewGuid(), "corr", stale, default)); await using var verify = new LoanAccountsDbContext(options); var persisted = await verify.LoanAccounts.AsNoTracking().SingleAsync(); Assert.Equal("Build", persisted.FinancingType); Assert.Equal(10, persisted.ReservedDisbursementAmount); Assert.Empty(await verify.FinancingTypeChanges.ToListAsync());
    }
    private sealed class Versions : IHistoricalLoanProductVersions { public Task<LoanProductVersionContract?> GetDefinitionAsync(Guid versionId, CancellationToken cancellationToken = default) => Task.FromResult<LoanProductVersionContract?>(new(Guid.NewGuid(), versionId, "Product", 1, "Active", 100, "OMR", 10, ["Build", "Purchase"], new("OM", 1, [], 1, "Monthly"), DateOnly.MinValue, null, "Published", DateTimeOffset.UtcNow)); }
    private static bool DockerAvailable() => File.Exists("/var/run/docker.sock") || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST"));
}
