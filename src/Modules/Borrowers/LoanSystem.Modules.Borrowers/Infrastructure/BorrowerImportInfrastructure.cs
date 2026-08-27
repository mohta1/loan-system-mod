using System.Data;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using LoanSystem.Modules.Borrowers.Application;
using LoanSystem.Modules.Borrowers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace LoanSystem.Modules.Borrowers.Infrastructure;

public sealed class BorrowerImportBatch
{
    public Guid BatchId { get; set; }
    public Guid SourceDocumentId { get; set; }
    public string Status { get; set; } = "Validated";
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public int ImportedRows { get; set; }
    public int FailedRows { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public byte[] RowVersion { get; set; } = []; public List<BorrowerImportRow> Rows { get; set; } = [];
}
public sealed class BorrowerImportRow
{
    public Guid BatchId { get; set; }
    public int RowNumber { get; set; }
    public string RawPayload { get; set; } = ""; public string Status { get; set; } = "Valid";
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? BorrowerId { get; set; }
    public BorrowerImportBatch Batch { get; set; } = null!;
}
public sealed class OpenXmlBorrowerWorkbookParser : IBorrowerWorkbookParser
{
    public IReadOnlyList<ParsedBorrowerRow> Parse(Stream workbook, int maximumRows)
    {
        using var document = SpreadsheetDocument.Open(workbook, false);
        var part = document.WorkbookPart ?? throw new BorrowerImportException("borrowerImports.invalidFile");
        var sheet = part.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault() ?? throw new BorrowerImportException("borrowerImports.invalidTemplate");
        var worksheet = (WorksheetPart)part.GetPartById(sheet.Id!);
        var rows = worksheet.Worksheet.GetFirstChild<SheetData>()?.Elements<Row>().ToList() ?? [];
        if (rows.Count == 0) throw new BorrowerImportException("borrowerImports.invalidTemplate");
        var headers = Values(rows[0], part).Select(x => x.Value).ToArray();
        if (headers.Any(string.IsNullOrWhiteSpace) || headers.Distinct(StringComparer.Ordinal).Count() != headers.Length || headers.Any(x => !BorrowerImportTemplate.Columns.Contains(x, StringComparer.Ordinal)))
            throw new BorrowerImportException("borrowerImports.invalidTemplate");
        var columns = headers.Select((header, index) => (header, index)).ToDictionary(x => x.header, x => x.index, StringComparer.Ordinal);
        if (BorrowerImportTemplate.Columns.Take(BorrowerImportTemplate.RequiredColumns).Any(required => !columns.ContainsKey(required)))
            throw new BorrowerImportException("borrowerImports.invalidTemplate");
        var result = new List<ParsedBorrowerRow>();
        foreach (var row in rows.Skip(1))
        {
            var cells = Values(row, part); if (cells.All(x => string.IsNullOrWhiteSpace(x.Value))) continue;
            if (result.Count >= maximumRows) throw new BorrowerImportException("borrowerImports.tooManyRows");
            CellText Field(string name) => columns.TryGetValue(name, out var index) && index < cells.Count ? cells[index] : new("", false, false);
            var civil = Field("Civil Number"); var employee = Field("Employee Number");
            var input = new BorrowerInput(civil.Value, Null(employee.Value), Field("Full Name").Value, Null(Field("Phone Number").Value), Field("Nationality").Value, Field("Organization").Value, Null(Field("Rank / Grade").Value), Null(Field("Employment Information").Value));
            var errors = new List<string>(); if (cells.Any(x => x.Formula)) errors.Add("borrowerImports.formulaNotSupported");
            if (civil.Numeric || employee.Numeric) errors.Add("borrowerImports.numericIdentifierNotSupported");
            try { _ = Borrower.Register(input.CivilNumber, input.EmployeeNumber, input.FullName, input.PhoneNumber, input.Nationality, input.Organization, input.RankGrade, input.EmploymentInformation); }
            catch (BorrowerValidationException) { errors.Add("borrowers.validation"); }
            result.Add(new((int)(row.RowIndex?.Value ?? (uint)(result.Count + 2)), input with { CivilNumber = input.CivilNumber.Trim(), EmployeeNumber = Null(input.EmployeeNumber), FullName = input.FullName.Trim(), PhoneNumber = Null(input.PhoneNumber), Nationality = input.Nationality.Trim(), Organization = input.Organization.Trim(), RankGrade = Null(input.RankGrade), EmploymentInformation = Null(input.EmploymentInformation) }, errors));
        }
        if (result.Count == 0) throw new BorrowerImportException("borrowerImports.invalidTemplate"); return result;
    }
    private static string? Null(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static List<CellText> Values(Row row, WorkbookPart part)
    {
        var result = new List<CellText>(); var column = 0;
        foreach (var cell in row.Elements<Cell>())
        {
            var target = Column(cell.CellReference?.Value); while (column++ < target) result.Add(new("", false, false));
            var numeric = cell.DataType is null || cell.DataType.Value == CellValues.Number;
            result.Add(new(Text(cell, part).Trim(), cell.CellFormula is not null, numeric));
        }
        return result;
    }
    private sealed record CellText(string Value, bool Formula, bool Numeric);
    private static int Column(string? reference) { if (string.IsNullOrEmpty(reference)) return 0; var n = 0; foreach (var c in reference.TakeWhile(char.IsLetter)) n = n * 26 + char.ToUpperInvariant(c) - 'A' + 1; return Math.Max(0, n - 1); }
    private static string Text(Cell cell, WorkbookPart part)
    {
        if (cell.DataType?.Value == CellValues.InlineString) return cell.InlineString?.InnerText ?? "";
        var value = cell.CellValue?.InnerText ?? "";
        if (cell.DataType?.Value == CellValues.SharedString && int.TryParse(value, out var index)) return part.SharedStringTablePart?.SharedStringTable.Elements<SharedStringItem>().ElementAtOrDefault(index)?.InnerText ?? "";
        return value;
    }
}
public sealed class BorrowerImportStore(BorrowersDbContext db, IOptions<BorrowerImportOptions> options) : IBorrowerImportStore
{
    public async Task<IReadOnlySet<string>> ExistingCivilNumbersAsync(IEnumerable<string> values, CancellationToken ct) { var items = values.Distinct().ToArray(); return (await db.Borrowers.Where(x => items.Contains(x.CivilNumber)).Select(x => x.CivilNumber).ToListAsync(ct)).ToHashSet(); }
    public async Task<IReadOnlySet<string>> ExistingEmployeeNumbersAsync(IEnumerable<string> values, CancellationToken ct) { var items = values.Distinct().ToArray(); return (await db.Borrowers.Where(x => x.EmployeeNumber != null && items.Contains(x.EmployeeNumber)).Select(x => x.EmployeeNumber!).ToListAsync(ct)).ToHashSet(); }
    public async Task<BorrowerImportDto> CreateAsync(Guid sourceDocumentId, Guid createdBy, IReadOnlyList<ParsedBorrowerRow> rows, CancellationToken ct)
    {
        var batch = new BorrowerImportBatch { BatchId = Guid.NewGuid(), SourceDocumentId = sourceDocumentId, CreatedBy = createdBy, CreatedAtUtc = DateTimeOffset.UtcNow };
        batch.IdempotencyKey = batch.BatchId.ToString("N"); batch.TotalRows = rows.Count; batch.ValidRows = rows.Count(x => x.Errors.Count == 0); batch.InvalidRows = rows.Count - batch.ValidRows;
        batch.Rows = rows.Select(x => new BorrowerImportRow { BatchId = batch.BatchId, RowNumber = x.RowNumber, RawPayload = JsonSerializer.Serialize(x.Input), Status = x.Errors.Count == 0 ? "Valid" : "Invalid", ErrorCode = x.Errors.Count == 0 ? null : string.Join('|', x.Errors) }).ToList();
        db.ImportBatches.Add(batch); await db.SaveChangesAsync(ct); return Map(batch);
    }
    public async Task<BorrowerImportDto?> GetAsync(Guid batchId, CancellationToken ct) { var batch = await db.ImportBatches.AsNoTracking().Include(x => x.Rows).SingleOrDefaultAsync(x => x.BatchId == batchId, ct); return batch is null ? null : Map(batch); }
    public async Task<BorrowerImportDto?> ExecuteAsync(Guid batchId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        if (await AcquireExecutionLockAsync(batchId, transaction, ct) < 0) throw new BorrowerImportExecutionBusyException();
        var batch = await db.ImportBatches.Include(x => x.Rows).SingleOrDefaultAsync(x => x.BatchId == batchId, ct);
        if (batch is null) { await transaction.CommitAsync(ct); return null; }
        if (batch.Status == "Completed") { var completed = Map(batch); await transaction.CommitAsync(ct); return completed; }
        if (batch.Status != "Validated") throw new BorrowerImportException("borrowerImports.batchNotExecutable"); batch.Status = "Executing"; await db.SaveChangesAsync(ct);
        foreach (var row in batch.Rows.Where(x => x.Status == "Valid"))
        {
            var input = JsonSerializer.Deserialize<BorrowerInput>(row.RawPayload)!;
            Borrower? borrower = null;
            try
            {
                borrower = Borrower.Register(input.CivilNumber, input.EmployeeNumber, input.FullName, input.PhoneNumber, input.Nationality, input.Organization, input.RankGrade, input.EmploymentInformation);
                if (await db.CivilExistsAsync(borrower.CivilNumber, null, ct)) throw new BorrowerConflictException("borrowers.civilNumberConflict");
                if (borrower.EmployeeNumber is not null && await db.EmployeeExistsAsync(borrower.EmployeeNumber, null, ct)) throw new BorrowerConflictException("borrowers.employeeNumberConflict");
                await db.AddAsync(borrower, ct); await db.SaveAsync(ct); row.BorrowerId = borrower.Id; row.Status = "Imported"; batch.ImportedRows++;
            }
            catch (BorrowerConflictException ex) { if (borrower is not null) db.Entry(borrower).State = EntityState.Detached; row.Status = "Failed"; row.ErrorCode = ex.Code; batch.FailedRows++; }
            catch (BorrowerValidationException) { row.Status = "Failed"; row.ErrorCode = "borrowers.validation"; batch.FailedRows++; }
        }
        batch.FailedRows += batch.InvalidRows; batch.Status = "Completed"; batch.CompletedAtUtc = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Map(batch);
    }
    private async Task<int> AcquireExecutionLockAsync(Guid batchId, IDbContextTransaction transaction, CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @timeout; SELECT @result;";
        var resource = command.CreateParameter(); resource.ParameterName = "@resource"; resource.Value = $"borrower-import:{batchId}"; command.Parameters.Add(resource);
        var timeout = command.CreateParameter(); timeout.ParameterName = "@timeout"; timeout.Value = options.Value.ExecutionLockTimeoutMilliseconds; command.Parameters.Add(timeout);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
    }
    private static BorrowerImportDto Map(BorrowerImportBatch batch) => new(batch.BatchId, batch.SourceDocumentId, batch.Status, batch.TotalRows, batch.ValidRows, batch.InvalidRows, batch.ImportedRows, batch.FailedRows, batch.CreatedAtUtc, batch.CompletedAtUtc, batch.Rows.OrderBy(x => x.RowNumber).Select(x => new BorrowerImportRowDto(x.RowNumber, x.Status, JsonSerializer.Deserialize<BorrowerInput>(x.RawPayload)!.CivilNumber, JsonSerializer.Deserialize<BorrowerInput>(x.RawPayload)!.EmployeeNumber, x.ErrorCode?.Split('|') ?? [], x.BorrowerId)).ToArray());
}
