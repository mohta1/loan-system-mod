using LoanSystem.Modules.Borrowers.Domain;
namespace LoanSystem.Modules.Borrowers.Application;

public static class BorrowerPermissions { public const string Read = "borrowers.read", Create = "borrowers.create", Update = "borrowers.update", ManageStatus = "borrowers.manageStatus"; }
public sealed record BorrowerInput(string CivilNumber, string? EmployeeNumber, string FullName, string? PhoneNumber, string Nationality, string Organization, string? RankGrade, string? EmploymentInformation);
public sealed record BorrowerDto(Guid BorrowerId, string CivilNumber, string? EmployeeNumber, string FullName, string? PhoneNumber, string Nationality, string Organization, string? RankGrade, string? EmploymentInformation, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string ETag);
public sealed record BorrowerListItemDto(Guid BorrowerId, string CivilNumber, string? EmployeeNumber, string FullName, string Nationality, string Organization, bool IsActive);
public sealed record BorrowerPage(IReadOnlyList<BorrowerListItemDto> Items, int PageNumber, int PageSize, int TotalCount);
public sealed record BorrowerSearch(string? CivilNumber, string? EmployeeNumber, string? Name, string? Organization, BorrowerStatus? Status, int PageNumber = 1, int PageSize = 25);
public interface IBorrowerStore { Task<bool> CivilExistsAsync(string value, Guid? except, CancellationToken ct); Task<bool> EmployeeExistsAsync(string value, Guid? except, CancellationToken ct); Task AddAsync(Borrower borrower, CancellationToken ct); Task<Borrower?> FindAsync(Guid id, CancellationToken ct); Task<BorrowerPage> SearchAsync(BorrowerSearch search, CancellationToken ct); void ExpectVersion(Borrower borrower, byte[] version); Task SaveAsync(CancellationToken ct); }
public sealed class BorrowerConflictException(string code) : Exception(code) { public string Code { get; } = code; }
public sealed class BorrowerConcurrencyException : Exception;
public sealed class BorrowerService(IBorrowerStore store)
{
    public async Task<BorrowerDto> RegisterAsync(BorrowerInput input, CancellationToken ct) { var b = Borrower.Register(input.CivilNumber, input.EmployeeNumber, input.FullName, input.PhoneNumber, input.Nationality, input.Organization, input.RankGrade, input.EmploymentInformation); await Unique(b, null, ct); await store.AddAsync(b, ct); await store.SaveAsync(ct); return Map(b); }
    public async Task<BorrowerDto?> GetAsync(Guid id, CancellationToken ct) { var b = await store.FindAsync(id, ct); return b is null ? null : Map(b); }
    public Task<BorrowerPage> SearchAsync(BorrowerSearch s, CancellationToken ct) => store.SearchAsync(s with { PageNumber = Math.Max(1, s.PageNumber), PageSize = Math.Clamp(s.PageSize, 1, 100) }, ct);
    public async Task<BorrowerDto?> UpdateAsync(Guid id, BorrowerInput input, byte[] version, CancellationToken ct) { var b = await store.FindAsync(id, ct); if (b is null) return null; b.Update(input.CivilNumber, input.EmployeeNumber, input.FullName, input.PhoneNumber, input.Nationality, input.Organization, input.RankGrade, input.EmploymentInformation); await Unique(b, id, ct); store.ExpectVersion(b, version); await store.SaveAsync(ct); return Map(b); }
    public async Task<BorrowerDto?> StatusAsync(Guid id, bool active, byte[] version, CancellationToken ct) { var b = await store.FindAsync(id, ct); if (b is null) return null; if (active) b.Activate(); else b.Deactivate(); store.ExpectVersion(b, version); await store.SaveAsync(ct); return Map(b); }
    async Task Unique(Borrower b, Guid? except, CancellationToken ct) { if (await store.CivilExistsAsync(b.CivilNumber, except, ct)) throw new BorrowerConflictException("borrowers.civilNumberConflict"); if (b.EmployeeNumber is not null && await store.EmployeeExistsAsync(b.EmployeeNumber, except, ct)) throw new BorrowerConflictException("borrowers.employeeNumberConflict"); }
    public static BorrowerDto Map(Borrower b) => new(b.Id, b.CivilNumber, b.EmployeeNumber, b.FullName, b.PhoneNumber, b.Nationality, b.Organization, b.RankGrade, b.EmploymentInformation, b.Status.ToString(), b.CreatedAt, b.UpdatedAt, Convert.ToBase64String(b.RowVersion));
}
