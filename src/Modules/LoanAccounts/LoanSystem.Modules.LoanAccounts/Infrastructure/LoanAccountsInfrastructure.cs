using LoanSystem.Modules.LoanAccounts.Application;
using LoanSystem.Modules.LoanAccounts.Domain;
using Microsoft.EntityFrameworkCore;
using LoanSystem.Contracts;
using System.Data;
using System.Text.Json;

namespace LoanSystem.Modules.LoanAccounts.Infrastructure;

public sealed class LoanAccountInboxMessage { public Guid EventId { get; set; } public DateTimeOffset OccurredAtUtc { get; set; } public DateTimeOffset ProcessedAtUtc { get; set; } }
public sealed class LoanAccountOutboxMessage { public Guid EventId { get; set; } public string EventType { get; set; } = ""; public string Payload { get; set; } = ""; public DateTimeOffset OccurredAtUtc { get; set; } public DateTimeOffset? ProcessedAtUtc { get; set; } public int Attempts { get; set; } public string? LastError { get; set; } }
public sealed class LoanAccountsDbContext(DbContextOptions<LoanAccountsDbContext> options) : DbContext(options), ILoanAccountStore
{
    public DbSet<LoanAccount> LoanAccounts => Set<LoanAccount>(); public DbSet<DisbursementReservation> Reservations => Set<DisbursementReservation>(); public DbSet<FinancingTypeChange> FinancingTypeChanges => Set<FinancingTypeChange>(); public DbSet<LoanAccountInboxMessage> InboxMessages => Set<LoanAccountInboxMessage>(); public DbSet<LoanAccountOutboxMessage> OutboxMessages => Set<LoanAccountOutboxMessage>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var b = modelBuilder.Entity<LoanAccount>(); b.ToTable("loan_accounts", "loan_accounts"); b.HasKey(x => x.LoanId); b.Ignore(x => x.DomainEvents); b.Ignore(x => x.AvailableToDisburse); b.Ignore(x => x.OutstandingBalance); b.Ignore(x => x.Reservations); b.Ignore(x => x.FinancingTypeChanges); b.Property(x => x.LoanId).HasColumnName("loan_id"); b.Property(x => x.SourceApplicationId).HasColumnName("source_application_id"); b.Property(x => x.BorrowerId).HasColumnName("borrower_id"); b.Property(x => x.LoanProductId).HasColumnName("loan_product_id"); b.Property(x => x.LoanProductVersionId).HasColumnName("loan_product_version_id"); b.Property(x => x.ApprovedAmount).HasColumnName("approved_amount").HasPrecision(19, 4); b.Property(x => x.Currency).HasColumnName("currency").HasColumnType("char(3)"); b.Property(x => x.FinancingType).HasColumnName("financing_type").HasMaxLength(100); b.Property(x => x.ReservedDisbursementAmount).HasColumnName("reserved_disbursement_amount").HasPrecision(19, 4); b.Property(x => x.TotalDisbursed).HasColumnName("total_disbursed").HasPrecision(19, 4); b.Property(x => x.TotalRepaid).HasColumnName("total_repaid").HasPrecision(19, 4); b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30); b.Property(x => x.OpenedAtUtc).HasColumnName("opened_at_utc"); b.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion(); b.HasIndex(x => x.SourceApplicationId).IsUnique().HasDatabaseName("UX_loan_accounts_source_application_id"); b.HasIndex(x => x.BorrowerId).HasDatabaseName("IX_loan_accounts_borrower_id"); b.HasIndex(x => x.Status).HasDatabaseName("IX_loan_accounts_status");
        var r = modelBuilder.Entity<DisbursementReservation>(); r.ToTable("disbursement_reservations", "loan_accounts"); r.HasKey(x => new { x.LoanId, x.DisbursementId }); r.Property(x => x.LoanId).HasColumnName("loan_id"); r.Property(x => x.DisbursementId).HasColumnName("disbursement_id"); r.Property(x => x.Amount).HasColumnName("amount").HasPrecision(19, 4); r.Property(x => x.Currency).HasColumnName("currency").HasColumnType("char(3)"); r.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30); r.Property(x => x.ReservedAtUtc).HasColumnName("reserved_at_utc"); r.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc"); r.HasIndex(x => x.DisbursementId).IsUnique().HasDatabaseName("UX_reservations_disbursement_id"); b.HasMany(typeof(DisbursementReservation), "_reservations").WithOne().HasForeignKey(nameof(DisbursementReservation.LoanId)).OnDelete(DeleteBehavior.Restrict);
        var h = modelBuilder.Entity<FinancingTypeChange>(); h.ToTable("financing_type_changes", "loan_accounts"); h.HasKey(x => x.Id); h.Property(x => x.Id).HasColumnName("change_id"); h.Property(x => x.LoanId).HasColumnName("loan_id"); h.Property(x => x.ActorUserId).HasColumnName("actor_user_id"); h.Property(x => x.PreviousFinancingType).HasColumnName("previous_financing_type").HasMaxLength(100); h.Property(x => x.NewFinancingType).HasColumnName("new_financing_type").HasMaxLength(100); h.Property(x => x.ChangedAtUtc).HasColumnName("changed_at_utc"); h.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100); b.HasMany(typeof(FinancingTypeChange), "_financingTypeChanges").WithOne().HasForeignKey(nameof(FinancingTypeChange.LoanId)).OnDelete(DeleteBehavior.Restrict);
        var i = modelBuilder.Entity<LoanAccountInboxMessage>(); i.ToTable("inbox_messages", "loan_accounts"); i.HasKey(x => x.EventId); i.Property(x => x.EventId).HasColumnName("event_id"); i.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc"); i.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc");
        var o = modelBuilder.Entity<LoanAccountOutboxMessage>(); o.ToTable("outbox_messages", "loan_accounts"); o.HasKey(x => x.EventId); o.Property(x => x.EventId).HasColumnName("event_id"); o.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(200); o.Property(x => x.Payload).HasColumnName("payload"); o.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc"); o.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc"); o.Property(x => x.Attempts).HasColumnName("attempts"); o.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(2000); o.HasIndex(x => new { x.ProcessedAtUtc, x.OccurredAtUtc }).HasDatabaseName("IX_loan_account_outbox_pending");
    }
    public Task<LoanAccount?> FindAsync(Guid id, CancellationToken ct) => LoanAccounts.Include("_reservations").Include("_financingTypeChanges").SingleOrDefaultAsync(x => x.LoanId == id, ct);
    public Task<LoanAccount?> FindBySourceAsync(Guid sourceId, CancellationToken ct) => LoanAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.SourceApplicationId == sourceId, ct);
    public async Task OpenOnceAsync(LoanAccount account, Guid eventId, DateTimeOffset occurredAt, CancellationToken ct)
    {
        await using var transaction = await Database.BeginTransactionAsync(ct);
        if (await InboxMessages.AnyAsync(x => x.EventId == eventId, ct) || await LoanAccounts.AnyAsync(x => x.SourceApplicationId == account.SourceApplicationId, ct)) { await transaction.CommitAsync(ct); return; }
        LoanAccounts.Add(account); InboxMessages.Add(new() { EventId = eventId, OccurredAtUtc = occurredAt, ProcessedAtUtc = DateTimeOffset.UtcNow });
        try { await SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException ex) when (HasSqlNumber(ex, 2601, 2627)) { await transaction.RollbackAsync(ct); ChangeTracker.Clear(); }
    }
    public async Task<LoanAccountPage> SearchAsync(LoanAccountSearch search, CancellationToken ct) { var q = LoanAccounts.AsNoTracking(); if (search.LoanId.HasValue) q = q.Where(x => x.LoanId == search.LoanId); if (search.SourceApplicationId.HasValue) q = q.Where(x => x.SourceApplicationId == search.SourceApplicationId); if (search.BorrowerId.HasValue) q = q.Where(x => x.BorrowerId == search.BorrowerId); if (search.Status.HasValue) q = q.Where(x => x.Status == search.Status); var count = await q.CountAsync(ct); var values = await q.OrderByDescending(x => x.OpenedAtUtc).ThenBy(x => x.LoanId).Skip((search.PageNumber - 1) * search.PageSize).Take(search.PageSize).ToListAsync(ct); return new(values.Select(LoanAccountService.Map).ToArray(), search.PageNumber, search.PageSize, count); }
    public void Expect(LoanAccount account, byte[] expected) { if (!account.RowVersion.AsSpan().SequenceEqual(expected)) throw new LoanAccountConcurrencyException(); Entry(account).Property(x => x.RowVersion).OriginalValue = expected; }
    public async Task SaveAsync(CancellationToken ct) { try { await SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new LoanAccountConcurrencyException(); } }
    public async Task ReserveAsync(DisbursementCapacityRequestedV1 message, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            ChangeTracker.Clear(); await using var transaction = await Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                if (await InboxMessages.AnyAsync(x => x.EventId == message.EventId, ct)) { await transaction.CommitAsync(ct); return; }
                var account = await LoanAccounts.Include("_reservations").SingleOrDefaultAsync(x => x.LoanId == message.LoanId, ct);
                var at = DateTimeOffset.UtcNow; ReservationResult result = account?.ReserveDisbursementCapacity(message.DisbursementId, message.Amount, message.Currency, at) ?? new(ReservationDecision.Rejected, "capacity.loanNotFound", "The loan account does not exist.");
                InboxMessages.Add(new() { EventId = message.EventId, OccurredAtUtc = message.OccurredAtUtc, ProcessedAtUtc = at });
                if (result.Decision != ReservationDecision.Duplicate)
                {
                    object response = result.Decision == ReservationDecision.Reserved
                        ? new DisbursementCapacityReservedV1(Guid.NewGuid(), at, message.CorrelationId, message.EventId, message.DisbursementId, message.LoanId, message.Amount, at, account is null ? "" : Convert.ToBase64String(account.RowVersion))
                        : new DisbursementCapacityRejectedV1(Guid.NewGuid(), at, message.CorrelationId, message.EventId, message.DisbursementId, message.LoanId, message.Amount, result.ReasonCode!, result.Reason!, at);
                    OutboxMessages.Add(new() { EventId = response is DisbursementCapacityReservedV1 yes ? yes.EventId : ((DisbursementCapacityRejectedV1)response).EventId, EventType = response.GetType().Name, Payload = JsonSerializer.Serialize(response, response.GetType()), OccurredAtUtc = at });
                }
                await SaveChangesAsync(ct); await transaction.CommitAsync(ct); return;
            }
            catch (Exception ex) when (attempt < 4 && IsRetryable(ex)) { await transaction.RollbackAsync(CancellationToken.None); await Task.Delay(20 * (attempt + 1), ct); }
        }
    }
    private static bool IsRetryable(Exception ex) => ex is DbUpdateConcurrencyException || HasSqlNumber(ex, 1205);
    private static bool HasSqlNumber(Exception? ex, params int[] numbers)
    {
        for (var current = ex; current is not null; current = current.InnerException)
            if (current is Microsoft.Data.SqlClient.SqlException sql && numbers.Contains(sql.Number)) return true;
        return false;
    }
}
