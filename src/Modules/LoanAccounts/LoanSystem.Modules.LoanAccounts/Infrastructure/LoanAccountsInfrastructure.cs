using LoanSystem.Modules.LoanAccounts.Application;
using LoanSystem.Modules.LoanAccounts.Domain;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.LoanAccounts.Infrastructure;

public sealed class LoanAccountInboxMessage { public Guid EventId { get; set; } public DateTimeOffset OccurredAtUtc { get; set; } public DateTimeOffset ProcessedAtUtc { get; set; } }
public sealed class LoanAccountsDbContext(DbContextOptions<LoanAccountsDbContext> options) : DbContext(options), ILoanAccountStore
{
    public DbSet<LoanAccount> LoanAccounts => Set<LoanAccount>(); public DbSet<LoanAccountInboxMessage> InboxMessages => Set<LoanAccountInboxMessage>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var b = modelBuilder.Entity<LoanAccount>(); b.ToTable("loan_accounts", "loan_accounts"); b.HasKey(x => x.LoanId); b.Ignore(x => x.DomainEvents); b.Ignore(x => x.AvailableToDisburse); b.Ignore(x => x.OutstandingBalance); b.Property(x => x.LoanId).HasColumnName("loan_id"); b.Property(x => x.SourceApplicationId).HasColumnName("source_application_id"); b.Property(x => x.BorrowerId).HasColumnName("borrower_id"); b.Property(x => x.LoanProductId).HasColumnName("loan_product_id"); b.Property(x => x.LoanProductVersionId).HasColumnName("loan_product_version_id"); b.Property(x => x.ApprovedAmount).HasColumnName("approved_amount").HasPrecision(19, 4); b.Property(x => x.Currency).HasColumnName("currency").HasColumnType("char(3)"); b.Property(x => x.FinancingType).HasColumnName("financing_type").HasMaxLength(100); b.Property(x => x.ReservedDisbursementAmount).HasColumnName("reserved_disbursement_amount").HasPrecision(19, 4); b.Property(x => x.TotalDisbursed).HasColumnName("total_disbursed").HasPrecision(19, 4); b.Property(x => x.TotalRepaid).HasColumnName("total_repaid").HasPrecision(19, 4); b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30); b.Property(x => x.OpenedAtUtc).HasColumnName("opened_at_utc"); b.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion(); b.HasIndex(x => x.SourceApplicationId).IsUnique().HasDatabaseName("UX_loan_accounts_source_application_id"); b.HasIndex(x => x.BorrowerId).HasDatabaseName("IX_loan_accounts_borrower_id"); b.HasIndex(x => x.Status).HasDatabaseName("IX_loan_accounts_status");
        var i = modelBuilder.Entity<LoanAccountInboxMessage>(); i.ToTable("inbox_messages", "loan_accounts"); i.HasKey(x => x.EventId); i.Property(x => x.EventId).HasColumnName("event_id"); i.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc"); i.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc");
    }
    public Task<LoanAccount?> FindAsync(Guid id, CancellationToken ct) => LoanAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.LoanId == id, ct);
    public Task<LoanAccount?> FindBySourceAsync(Guid sourceId, CancellationToken ct) => LoanAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.SourceApplicationId == sourceId, ct);
    public async Task OpenOnceAsync(LoanAccount account, Guid eventId, DateTimeOffset occurredAt, CancellationToken ct)
    {
        await using var transaction = await Database.BeginTransactionAsync(ct);
        if (await InboxMessages.AnyAsync(x => x.EventId == eventId, ct) || await LoanAccounts.AnyAsync(x => x.SourceApplicationId == account.SourceApplicationId, ct)) { await transaction.CommitAsync(ct); return; }
        LoanAccounts.Add(account); InboxMessages.Add(new() { EventId = eventId, OccurredAtUtc = occurredAt, ProcessedAtUtc = DateTimeOffset.UtcNow });
        try { await SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 2601 or 2627) { await transaction.RollbackAsync(ct); ChangeTracker.Clear(); }
    }
    public async Task<LoanAccountPage> SearchAsync(LoanAccountSearch search, CancellationToken ct) { var q = LoanAccounts.AsNoTracking(); if (search.LoanId.HasValue) q = q.Where(x => x.LoanId == search.LoanId); if (search.SourceApplicationId.HasValue) q = q.Where(x => x.SourceApplicationId == search.SourceApplicationId); if (search.BorrowerId.HasValue) q = q.Where(x => x.BorrowerId == search.BorrowerId); if (search.Status.HasValue) q = q.Where(x => x.Status == search.Status); var count = await q.CountAsync(ct); var values = await q.OrderByDescending(x => x.OpenedAtUtc).ThenBy(x => x.LoanId).Skip((search.PageNumber - 1) * search.PageSize).Take(search.PageSize).ToListAsync(ct); return new(values.Select(LoanAccountService.Map).ToArray(), search.PageNumber, search.PageSize, count); }
}
