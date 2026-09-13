using System.Text.Json;
using LoanSystem.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LoanSystem.Modules.LoanOrigination.Infrastructure;

public sealed class LoanApprovalOutboxDispatcher(IServiceScopeFactory scopes, ILogger<LoanApprovalOutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DispatchAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException) { LogDispatchFailure(logger, ex); }
            await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken);
        }
    }

    private static readonly Action<ILogger, Exception?> LogDispatchFailure = LoggerMessage.Define(LogLevel.Error, new EventId(1101), "Loan approval outbox dispatch failed");
    private static readonly Action<ILogger, Guid, Exception?> LogRetry = LoggerMessage.Define<Guid>(LogLevel.Warning, new EventId(1102), "Approval event {EventId} will be retried");

    public async Task DispatchAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LoanOriginationDbContext>();
        var consumer = scope.ServiceProvider.GetRequiredService<ILoanApplicationApprovedConsumer>();
        var pending = await db.OutboxMessages.Where(x => x.ProcessedAtUtc == null && x.EventType == nameof(LoanApplicationApprovedV1)).OrderBy(x => x.OccurredAtUtc).Take(20).ToListAsync(ct);
        foreach (var item in pending)
        {
            try
            {
                var message = JsonSerializer.Deserialize<LoanApplicationApprovedV1>(item.Payload) ?? throw new InvalidOperationException("Invalid approval outbox payload.");
                await consumer.ConsumeAsync(message, ct); item.ProcessedAtUtc = DateTimeOffset.UtcNow; item.LastError = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { item.Attempts++; item.LastError = ex.Message[..Math.Min(ex.Message.Length, 2000)]; LogRetry(logger, item.EventId, ex); }
            await db.SaveChangesAsync(ct);
        }
    }
}
