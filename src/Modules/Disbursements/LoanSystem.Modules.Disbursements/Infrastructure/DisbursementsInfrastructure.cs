using System.Data;
using System.Text.Json;
using LoanSystem.Contracts;
using LoanSystem.Modules.Disbursements.Application;
using LoanSystem.Modules.Disbursements.Domain;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.Disbursements.Infrastructure;

public sealed class DisbursementOutboxMessage { public Guid EventId { get; set; } public string EventType { get; set; } = ""; public string Payload { get; set; } = ""; public DateTimeOffset OccurredAtUtc { get; set; } public DateTimeOffset? ProcessedAtUtc { get; set; } public int Attempts { get; set; } public string? LastError { get; set; } }
public sealed class DisbursementInboxMessage { public Guid EventId { get; set; } public DateTimeOffset OccurredAtUtc { get; set; } public DateTimeOffset ProcessedAtUtc { get; set; } }
public sealed class IdempotencyRecord { public Guid Id { get; set; } public string Scope { get; set; } = ""; public Guid ActorUserId { get; set; } public string KeyHash { get; set; } = ""; public string RequestHash { get; set; } = ""; public Guid ResourceId { get; set; } public DateTimeOffset CreatedAtUtc { get; set; } }
public sealed class DisbursementsDbContext(DbContextOptions<DisbursementsDbContext> options) : DbContext(options), IDisbursementStore
{
    public DbSet<Disbursement> Disbursements => Set<Disbursement>(); public DbSet<DisbursementDocument> Documents => Set<DisbursementDocument>(); public DbSet<DisbursementOutboxMessage> OutboxMessages => Set<DisbursementOutboxMessage>(); public DbSet<DisbursementInboxMessage> InboxMessages => Set<DisbursementInboxMessage>(); public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var b = modelBuilder.Entity<Disbursement>(); b.ToTable("disbursements", "disbursements"); b.HasKey(x => x.DisbursementId); b.Ignore(x => x.SupportingDocuments); b.Property(x => x.DisbursementId).HasColumnName("disbursement_id"); b.Property(x => x.LoanId).HasColumnName("loan_id"); b.Property(x => x.Amount).HasColumnName("amount").HasPrecision(19, 4); b.Property(x => x.Currency).HasColumnName("currency").HasColumnType("char(3)"); b.Property(x => x.Beneficiary).HasColumnName("beneficiary_snapshot").HasColumnType("nvarchar(max)").HasConversion(x => JsonSerializer.Serialize(x, JsonSerializerOptions.Default), x => JsonSerializer.Deserialize<BeneficiarySnapshot>(x, JsonSerializerOptions.Default)!); b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30); b.Property(x => x.CapacityReservationStatus).HasColumnName("capacity_reservation_status").HasConversion<string>().HasMaxLength(30); b.Property(x => x.CapacityRejectionReasonCode).HasColumnName("capacity_rejection_reason_code").HasMaxLength(100); b.Property(x => x.CapacityRejectionReason).HasColumnName("capacity_rejection_reason").HasMaxLength(500); b.Property(x => x.ActorUserId).HasColumnName("actor_user_id"); b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc"); b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc"); b.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100); b.Property(x => x.CreationTokenHash).HasColumnName("creation_token_hash").HasMaxLength(64); b.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion(); b.HasIndex(x => x.CreationTokenHash).IsUnique().HasDatabaseName("UX_disbursements_creation_token"); b.HasIndex(x => x.LoanId).HasDatabaseName("IX_disbursements_loan_id"); b.HasIndex(x => x.Status).HasDatabaseName("IX_disbursements_status"); b.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("IX_disbursements_created_at");
        var d = modelBuilder.Entity<DisbursementDocument>(); d.ToTable("supporting_documents", "disbursements"); d.HasKey(x => new { x.DisbursementId, x.DocumentId }); d.Property(x => x.DisbursementId).HasColumnName("disbursement_id"); d.Property(x => x.DocumentId).HasColumnName("document_id"); b.HasMany(typeof(DisbursementDocument), "_supportingDocuments").WithOne().HasForeignKey(nameof(DisbursementDocument.DisbursementId)).OnDelete(DeleteBehavior.Cascade);
        var o = modelBuilder.Entity<DisbursementOutboxMessage>(); o.ToTable("outbox_messages", "disbursements"); o.HasKey(x => x.EventId); o.Property(x => x.EventId).HasColumnName("event_id"); o.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(200); o.Property(x => x.Payload).HasColumnName("payload"); o.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc"); o.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc"); o.Property(x => x.Attempts).HasColumnName("attempts"); o.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(2000); o.HasIndex(x => new { x.ProcessedAtUtc, x.OccurredAtUtc }).HasDatabaseName("IX_disbursement_outbox_pending");
        var i = modelBuilder.Entity<DisbursementInboxMessage>(); i.ToTable("inbox_messages", "disbursements"); i.HasKey(x => x.EventId); i.Property(x => x.EventId).HasColumnName("event_id"); i.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc"); i.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc");
        var p = modelBuilder.Entity<IdempotencyRecord>(); p.ToTable("idempotency_records", "platform"); p.HasKey(x => x.Id); p.Property(x => x.Id).HasColumnName("id"); p.Property(x => x.Scope).HasColumnName("scope").HasMaxLength(100); p.Property(x => x.ActorUserId).HasColumnName("actor_user_id"); p.Property(x => x.KeyHash).HasColumnName("key_hash").HasMaxLength(64); p.Property(x => x.RequestHash).HasColumnName("request_hash").HasMaxLength(64); p.Property(x => x.ResourceId).HasColumnName("resource_id"); p.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc"); p.HasIndex(x => new { x.Scope, x.ActorUserId, x.KeyHash }).IsUnique().HasDatabaseName("UX_idempotency_scope_actor_key");
    }
    public Task<Disbursement?> FindAsync(Guid id, CancellationToken ct) => Disbursements.Include("_supportingDocuments").AsNoTracking().SingleOrDefaultAsync(x => x.DisbursementId == id, ct);
    public async Task<DisbursementPage> SearchAsync(DisbursementSearch search, CancellationToken ct) { var q = Disbursements.AsNoTracking(); if (search.LoanId.HasValue) q = q.Where(x => x.LoanId == search.LoanId); if (search.Status.HasValue) q = q.Where(x => x.Status == search.Status); var count = await q.CountAsync(ct); var rows = await q.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.DisbursementId).Skip((search.PageNumber - 1) * search.PageSize).Take(search.PageSize).ToListAsync(ct); return new(rows.Select(x => new DisbursementListItem(x.DisbursementId, x.LoanId, x.Amount, x.Currency, x.Beneficiary.DisplayName, x.Beneficiary.Type.ToString(), x.Status.ToString(), x.CapacityReservationStatus.ToString(), x.CreatedAtUtc)).ToArray(), search.PageNumber, search.PageSize, count); }
    public async Task<IdempotentCreateResult> CreateAsync(Disbursement value, string scope, Guid actor, string keyHash, string requestHash, DisbursementCapacityRequestedV1 message, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            ChangeTracker.Clear();
            await using var tx = await Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var existing = await FindIdempotentResultAsync(scope, actor, keyHash, requestHash, ct);
                if (existing is not null) { await tx.CommitAsync(ct); return existing; }

                Disbursements.Add(value);
                IdempotencyRecords.Add(new() { Id = Guid.NewGuid(), Scope = scope, ActorUserId = actor, KeyHash = keyHash, RequestHash = requestHash, ResourceId = value.DisbursementId, CreatedAtUtc = value.CreatedAtUtc });
                OutboxMessages.Add(new() { EventId = message.EventId, EventType = nameof(DisbursementCapacityRequestedV1), Payload = JsonSerializer.Serialize(message), OccurredAtUtc = message.OccurredAtUtc });
                await SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new(IdempotentCreateOutcome.Created, value);
            }
            catch (Exception ex) when (HasSqlNumber(ex, 2601, 2627))
            {
                await tx.RollbackAsync(CancellationToken.None);
                ChangeTracker.Clear();
                var winner = await FindIdempotentResultAsync(scope, actor, keyHash, requestHash, ct);
                if (winner is not null) return winner;
                if (attempt >= 4) throw;
                await Task.Delay(20 * (attempt + 1), ct);
            }
            catch (Exception ex) when (attempt < 4 && IsRetryable(ex))
            {
                await tx.RollbackAsync(CancellationToken.None);
                await Task.Delay(20 * (attempt + 1), ct);
            }
        }
    }
    private async Task<IdempotentCreateResult?> FindIdempotentResultAsync(string scope, Guid actor, string keyHash, string requestHash, CancellationToken ct)
    {
        var record = await IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Scope == scope && x.ActorUserId == actor && x.KeyHash == keyHash, ct);
        if (record is null) return null;
        var existing = await Disbursements.Include("_supportingDocuments").AsNoTracking().SingleAsync(x => x.DisbursementId == record.ResourceId, ct);
        return new(record.RequestHash == requestHash ? IdempotentCreateOutcome.Replayed : IdempotentCreateOutcome.Conflict, existing);
    }
    private static bool IsRetryable(Exception ex) => ex is DbUpdateConcurrencyException || HasSqlNumber(ex, 1205);
    private static bool HasSqlNumber(Exception? ex, params int[] numbers)
    {
        for (var current = ex; current is not null; current = current.InnerException)
            if (current is Microsoft.Data.SqlClient.SqlException sql && numbers.Contains(sql.Number)) return true;
        return false;
    }
    public Task ApplyReservedAsync(DisbursementCapacityReservedV1 message, CancellationToken ct) => ApplyAsync(message.EventId, message.OccurredAtUtc, message.DisbursementId, x => x.CapacityReserved(message.LoanId, message.ReservedAmount, message.ReservedAtUtc), ct);
    public Task ApplyRejectedAsync(DisbursementCapacityRejectedV1 message, CancellationToken ct) => ApplyAsync(message.EventId, message.OccurredAtUtc, message.DisbursementId, x => x.CapacityRejected(message.LoanId, message.RequestedAmount, message.ReasonCode, message.Reason, message.RejectedAtUtc), ct);
    private async Task ApplyAsync(Guid eventId, DateTimeOffset occurredAt, Guid disbursementId, Action<Disbursement> transition, CancellationToken ct) { await using var tx = await Database.BeginTransactionAsync(ct); if (await InboxMessages.AnyAsync(x => x.EventId == eventId, ct)) { await tx.CommitAsync(ct); return; } var value = await Disbursements.SingleOrDefaultAsync(x => x.DisbursementId == disbursementId, ct) ?? throw new InvalidOperationException("Capacity response references an unknown disbursement."); transition(value); InboxMessages.Add(new() { EventId = eventId, OccurredAtUtc = occurredAt, ProcessedAtUtc = DateTimeOffset.UtcNow }); await SaveChangesAsync(ct); await tx.CommitAsync(ct); }
}
