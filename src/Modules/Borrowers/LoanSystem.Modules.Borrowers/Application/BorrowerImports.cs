using LoanSystem.Contracts;
using LoanSystem.Modules.Borrowers.Domain;

namespace LoanSystem.Modules.Borrowers.Application;

public static class BorrowerImportPermissions { public const string Import = "borrowers.import"; }
public static class BorrowerImportTemplate
{
    public const string Version = "mvp-v1";
    public static readonly string[] Columns = ["Civil Number", "Full Name", "Nationality", "Organization", "Employee Number", "Phone Number", "Rank / Grade", "Employment Information"];
    public const int RequiredColumns = 4;
}
public sealed class BorrowerImportOptions { public long MaximumFileSizeBytes { get; set; } = 5 * 1024 * 1024; public int MaximumRows { get; set; } = 5000; public int ExecutionLockTimeoutMilliseconds { get; set; } = 15000; }
public sealed record ParsedBorrowerRow(int RowNumber, BorrowerInput Input, IReadOnlyList<string> Errors);
public sealed record BorrowerImportRowDto(int RowNumber, string Status, string CivilNumber, string? EmployeeNumber, IReadOnlyList<string> ErrorCodes, Guid? BorrowerId);
public sealed record BorrowerImportDto(Guid BatchId, Guid SourceDocumentId, string Status, int TotalRows, int ValidRows, int InvalidRows, int ImportedRows, int FailedRows, DateTimeOffset CreatedAtUtc, DateTimeOffset? CompletedAtUtc, IReadOnlyList<BorrowerImportRowDto> Rows);
public sealed class BorrowerImportException(string code) : Exception(code) { public string Code { get; } = code; }
public sealed class BorrowerImportExecutionBusyException : Exception;
public interface IBorrowerWorkbookParser { IReadOnlyList<ParsedBorrowerRow> Parse(Stream workbook, int maximumRows); }
public interface IBorrowerImportStore
{
    Task<IReadOnlySet<string>> ExistingCivilNumbersAsync(IEnumerable<string> values, CancellationToken ct);
    Task<IReadOnlySet<string>> ExistingEmployeeNumbersAsync(IEnumerable<string> values, CancellationToken ct);
    Task<BorrowerImportDto> CreateAsync(Guid sourceDocumentId, Guid createdBy, IReadOnlyList<ParsedBorrowerRow> rows, CancellationToken ct);
    Task<BorrowerImportDto?> GetAsync(Guid batchId, CancellationToken ct);
    Task<BorrowerImportDto?> ExecuteAsync(Guid batchId, CancellationToken ct);
}
public sealed class BorrowerImportService(IBorrowerWorkbookParser parser, IBorrowerImportStore store, IImportSourceDocumentStore documents)
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public async Task<BorrowerImportDto> ValidateAsync(string fileName, string contentType, long length, Stream content, Guid userId, BorrowerImportOptions limits, CancellationToken ct)
    {
        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || !string.Equals(contentType, ContentType, StringComparison.OrdinalIgnoreCase)) throw new BorrowerImportException("borrowerImports.unsupportedFile");
        if (length <= 0 || length > limits.MaximumFileSizeBytes) throw new BorrowerImportException("borrowerImports.invalidFile");
        await using var buffer = new MemoryStream(); await content.CopyToAsync(buffer, ct); buffer.Position = 0;
        IReadOnlyList<ParsedBorrowerRow> rows;
        try { rows = parser.Parse(buffer, limits.MaximumRows); }
        catch (BorrowerImportException) { throw; }
        catch { throw new BorrowerImportException("borrowerImports.invalidFile"); }
        var civilDuplicates = rows.GroupBy(x => x.Input.CivilNumber, StringComparer.Ordinal).Where(x => !string.IsNullOrEmpty(x.Key) && x.Count() > 1).Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        var employeeDuplicates = rows.Where(x => x.Input.EmployeeNumber is not null).GroupBy(x => x.Input.EmployeeNumber!, StringComparer.Ordinal).Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        var existingCivil = await store.ExistingCivilNumbersAsync(rows.Select(x => x.Input.CivilNumber), ct);
        var existingEmployee = await store.ExistingEmployeeNumbersAsync(rows.Where(x => x.Input.EmployeeNumber is not null).Select(x => x.Input.EmployeeNumber!), ct);
        rows = rows.Select(row => AddErrors(row, civilDuplicates, employeeDuplicates, existingCivil, existingEmployee)).ToArray();
        buffer.Position = 0; var documentId = await documents.StoreAsync(Path.GetFileName(fileName), ContentType, length, buffer, userId, ct);
        try { return await store.CreateAsync(documentId, userId, rows, ct); }
        catch
        {
            await documents.DiscardAsync(documentId, userId, CancellationToken.None);
            throw;
        }
    }
    public Task<BorrowerImportDto?> GetAsync(Guid id, CancellationToken ct) => store.GetAsync(id, ct);
    public Task<BorrowerImportDto?> ExecuteAsync(Guid id, CancellationToken ct) => store.ExecuteAsync(id, ct);
    private static ParsedBorrowerRow AddErrors(ParsedBorrowerRow row, HashSet<string> civilDuplicates, HashSet<string> employeeDuplicates, IReadOnlySet<string> existingCivil, IReadOnlySet<string> existingEmployee)
    {
        var errors = row.Errors.ToList();
        if (civilDuplicates.Contains(row.Input.CivilNumber)) errors.Add("borrowerImports.duplicateCivilNumberInFile");
        if (row.Input.EmployeeNumber is not null && employeeDuplicates.Contains(row.Input.EmployeeNumber)) errors.Add("borrowerImports.duplicateEmployeeNumberInFile");
        if (existingCivil.Contains(row.Input.CivilNumber)) errors.Add("borrowers.civilNumberConflict");
        if (row.Input.EmployeeNumber is not null && existingEmployee.Contains(row.Input.EmployeeNumber)) errors.Add("borrowers.employeeNumberConflict");
        return row with { Errors = errors.Distinct().ToArray() };
    }
}
