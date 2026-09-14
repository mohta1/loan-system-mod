using System.Text.Json;
using LoanSystem.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LoanSystem.Modules.LoanAccounts.Infrastructure;

public sealed class LoanAccountOutboxDispatcher(IServiceScopeFactory scopes, ILogger<LoanAccountOutboxDispatcher> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogFailure = LoggerMessage.Define(LogLevel.Error, new EventId(1201), "Loan account outbox dispatch failed");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested) { try { await DispatchAsync(stoppingToken); } catch (Exception ex) when (ex is not OperationCanceledException) { LogFailure(logger, ex); } await Task.Delay(250, stoppingToken); }
    }
    public async Task DispatchAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LoanAccountsDbContext>();
        var pending = await db.OutboxMessages.Where(x => x.ProcessedAtUtc == null).OrderBy(x => x.OccurredAtUtc).Take(20).ToListAsync(ct);
        foreach (var item in pending)
        {
            try
            {
                if (item.EventType == nameof(DisbursementCapacityReservedV1)) await scope.ServiceProvider.GetRequiredService<IDisbursementCapacityReservedConsumer>().ConsumeAsync(JsonSerializer.Deserialize<DisbursementCapacityReservedV1>(item.Payload)!, ct);
                else if (item.EventType == nameof(DisbursementCapacityRejectedV1)) await scope.ServiceProvider.GetRequiredService<IDisbursementCapacityRejectedConsumer>().ConsumeAsync(JsonSerializer.Deserialize<DisbursementCapacityRejectedV1>(item.Payload)!, ct);
                item.ProcessedAtUtc = DateTimeOffset.UtcNow; item.LastError = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { item.Attempts++; item.LastError = ex.Message[..Math.Min(2000, ex.Message.Length)]; }
            await db.SaveChangesAsync(ct);
        }
    }
}
