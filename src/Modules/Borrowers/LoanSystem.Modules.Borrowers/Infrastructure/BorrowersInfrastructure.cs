using LoanSystem.Modules.Borrowers.Application;
using LoanSystem.Modules.Borrowers.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.Borrowers.Infrastructure;

public sealed class BorrowersDbContext(DbContextOptions<BorrowersDbContext> options) : DbContext(options), IBorrowerStore
{
    private const string CivilNumberIndex = "IX_borrowers_CivilNumber";
    private const string EmployeeNumberIndex = "IX_borrowers_EmployeeNumber";

    public DbSet<Borrower> Borrowers => Set<Borrower>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var borrower = modelBuilder.Entity<Borrower>();
        borrower.ToTable("borrowers", "borrowers");
        borrower.HasKey(x => x.Id);
        borrower.Property(x => x.CivilNumber).HasMaxLength(100).IsRequired();
        borrower.HasIndex(x => x.CivilNumber).IsUnique().HasDatabaseName(CivilNumberIndex);
        borrower.Property(x => x.EmployeeNumber).HasMaxLength(100);
        borrower.HasIndex(x => x.EmployeeNumber).IsUnique().HasFilter("[EmployeeNumber] IS NOT NULL").HasDatabaseName(EmployeeNumberIndex);
        borrower.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        borrower.HasIndex(x => x.FullName);
        borrower.Property(x => x.PhoneNumber).HasMaxLength(50);
        borrower.Property(x => x.Nationality).HasMaxLength(100).IsRequired();
        borrower.Property(x => x.Organization).HasMaxLength(200).IsRequired();
        borrower.HasIndex(x => x.Organization);
        borrower.Property(x => x.RankGrade).HasMaxLength(100);
        borrower.Property(x => x.EmploymentInformation).HasMaxLength(1000);
        borrower.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        borrower.HasIndex(x => x.Status);
        borrower.Property(x => x.RowVersion).IsRowVersion();
    }

    public Task<bool> CivilExistsAsync(string value, Guid? except, CancellationToken ct) =>
        Borrowers.AnyAsync(x => x.CivilNumber == value && (!except.HasValue || x.Id != except), ct);

    public Task<bool> EmployeeExistsAsync(string value, Guid? except, CancellationToken ct) =>
        Borrowers.AnyAsync(x => x.EmployeeNumber == value && (!except.HasValue || x.Id != except), ct);

    public Task AddAsync(Borrower borrower, CancellationToken ct) => Borrowers.AddAsync(borrower, ct).AsTask();

    public Task<Borrower?> FindAsync(Guid id, CancellationToken ct) => Borrowers.SingleOrDefaultAsync(x => x.Id == id, ct);

    public void ExpectVersion(Borrower borrower, byte[] version) => Entry(borrower).Property(x => x.RowVersion).OriginalValue = version;

    public async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BorrowerConcurrencyException();
        }
        catch (DbUpdateException exception)
        {
            var code = DuplicateConflictCode(exception);
            if (code is null)
                throw;
            throw new BorrowerConflictException(code);
        }
    }

    public async Task<BorrowerPage> SearchAsync(BorrowerSearch search, CancellationToken ct)
    {
        var query = Borrowers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search.CivilNumber))
            query = query.Where(x => x.CivilNumber.Contains(search.CivilNumber.Trim()));
        if (!string.IsNullOrWhiteSpace(search.EmployeeNumber))
            query = query.Where(x => x.EmployeeNumber != null && x.EmployeeNumber.Contains(search.EmployeeNumber.Trim()));
        if (!string.IsNullOrWhiteSpace(search.Name))
            query = query.Where(x => x.FullName.Contains(search.Name.Trim()));
        if (!string.IsNullOrWhiteSpace(search.Organization))
            query = query.Where(x => x.Organization.Contains(search.Organization.Trim()));
        if (search.Status.HasValue)
            query = query.Where(x => x.Status == search.Status);

        var count = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.FullName)
            .ThenBy(x => x.Id)
            .Skip((search.PageNumber - 1) * search.PageSize)
            .Take(search.PageSize)
            .Select(x => new BorrowerListItemDto(
                x.Id,
                x.CivilNumber,
                x.EmployeeNumber,
                x.FullName,
                x.Nationality,
                x.Organization,
                x.Status == BorrowerStatus.Active))
            .ToListAsync(ct);
        return new(rows, search.PageNumber, search.PageSize, count);
    }

    private static string? DuplicateConflictCode(DbUpdateException exception)
    {
        if (exception.InnerException is not SqlException { Number: 2601 or 2627 } sqlException)
            return null;
        if (sqlException.Message.Contains(EmployeeNumberIndex, StringComparison.Ordinal))
            return "borrowers.employeeNumberConflict";
        if (sqlException.Message.Contains(CivilNumberIndex, StringComparison.Ordinal))
            return "borrowers.civilNumberConflict";
        return null;
    }
}
