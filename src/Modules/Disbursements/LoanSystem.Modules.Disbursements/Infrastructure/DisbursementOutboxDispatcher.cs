using System.Text.Json;
using LoanSystem.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LoanSystem.Modules.Disbursements.Infrastructure;

public sealed class DisbursementOutboxDispatcher(IServiceScopeFactory scopes, ILogger<DisbursementOutboxDispatcher> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogFailure = LoggerMessage.Define(LogLevel.Error, new EventId(1202), "Disbursement outbox dispatch failed");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) { while (!stoppingToken.IsCancellationRequested) { try { await DispatchAsync(stoppingToken); } catch (Exception ex) when (ex is not OperationCanceledException) { LogFailure(logger, ex); } await Task.Delay(250, stoppingToken); } }
    public async Task DispatchAsync(CancellationToken ct) { using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DisbursementsDbContext>(); var rows = await db.OutboxMessages.Where(x => x.ProcessedAtUtc == null).OrderBy(x => x.OccurredAtUtc).Take(20).ToListAsync(ct); foreach (var row in rows) { try { if (row.EventType == nameof(DisbursementCapacityRequestedV1)) await scope.ServiceProvider.GetRequiredService<IDisbursementCapacityRequestedConsumer>().ConsumeAsync(JsonSerializer.Deserialize<DisbursementCapacityRequestedV1>(row.Payload)!, ct); row.ProcessedAtUtc = DateTimeOffset.UtcNow; row.LastError = null; } catch (Exception ex) when (ex is not OperationCanceledException) { row.Attempts++; row.LastError = ex.Message[..Math.Min(2000, ex.Message.Length)]; } await db.SaveChangesAsync(ct); } }
}
