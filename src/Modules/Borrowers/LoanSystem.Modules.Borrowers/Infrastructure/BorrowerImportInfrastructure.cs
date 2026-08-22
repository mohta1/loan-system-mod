using System.Data;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using LoanSystem.Modules.Borrowers.Application;
using LoanSystem.Modules.Borrowers.Domain;
using Microsoft.EntityFrameworkCore;

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
        if (headers.Length < BorrowerImportTemplate.RequiredColumns || !BorrowerImportTemplate.Columns.Take(BorrowerImportTemplate.RequiredColumns).SequenceEqual(headers.Take(BorrowerImportTemplate.RequiredColumns), StringComparer.Ordinal))
            throw new BorrowerImportException("borrowerImports.invalidTemplate");
        if (headers.Any(x => !BorrowerImportTemplate.Columns.Contains(x, StringComparer.Ordinal))) throw new BorrowerImportException("borrowerImports.invalidTemplate");
        var result = new List<ParsedBorrowerRow>();
        foreach (var row in rows.Skip(1))
        {
            var cells = Values(row, part); if (cells.All(x => string.IsNullOrWhiteSpace(x.Value))) continue;
            if (result.Count >= maximumRows) throw new BorrowerImportException("borrowerImports.tooManyRows");
            var values = Enumerable.Range(0, BorrowerImportTemplate.Columns.Length).Select(i => i < cells.Count ? cells[i] : ("", false)).ToArray();
            var input = new BorrowerInput(values[0].Item1, Null(values[4].Item1), values[1].Item1, Null(values[5].Item1), values[2].Item1, values[3].Item1, Null(values[6].Item1), Null(values[7].Item1));
            var errors = new List<string>(); if (values.Any(x => x.Item2)) errors.Add("borrowerImports.formulaNotSupported");
            try { _ = Borrower.Register(input.CivilNumber, input.EmployeeNumber, input.FullName, input.PhoneNumber, input.Nationality, input.Organization, input.RankGrade, input.EmploymentInformation); }
            catch (BorrowerValidationException) { errors.Add("borrowers.validation"); }
            result.Add(new((int)(row.RowIndex?.Value ?? (uint)(result.Count + 2)), input with { CivilNumber = input.CivilNumber.Trim(), EmployeeNumber = Null(input.EmployeeNumber), FullName = input.FullName.Trim(), PhoneNumber = Null(input.PhoneNumber), Nationality = input.Nationality.Trim(), Organization = input.Organization.Trim(), RankGrade = Null(input.RankGrade), EmploymentInformation = Null(input.EmploymentInformation) }, errors));
        }
        if (result.Count == 0) throw new BorrowerImportException("borrowerImports.invalidTemplate"); return result;
    }
    private static string? Null(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static List<(string Value, bool Formula)> Values(Row row, WorkbookPart part)
    {
        var result = new List<(string, bool)>(); var column = 0;
        foreach (var cell in row.Elements<Cell>())
        {
            var target = Column(cell.CellReference?.Value); while (column++ < target) result.Add(("", false));
            result.Add((Text(cell, part).Trim(), cell.CellFormula is not null));
        }
        return result;
    }
    private static int Column(string? reference) { if (string.IsNullOrEmpty(reference)) return 0; var n = 0; foreach (var c in reference.TakeWhile(char.IsLetter)) n = n * 26 + char.ToUpperInvariant(c) - 'A' + 1; return Math.Max(0, n - 1); }
    private static string Text(Cell cell, WorkbookPart part)
    {
        if (cell.DataType?.Value == CellValues.InlineString) return cell.InlineString?.InnerText ?? "";
        var value = cell.CellValue?.InnerText ?? "";
        if (cell.DataType?.Value == CellValues.SharedString && int.TryParse(value, out var index)) return part.SharedStringTablePart?.SharedStringTable.Elements<SharedStringItem>().ElementAtOrDefault(index)?.InnerText ?? "";
        return value;
    }
}
public sealed class BorrowerImportStore(BorrowersDbContext db) : IBorrowerImportStore
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
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var batch = await db.ImportBatches.Include(x => x.Rows).SingleOrDefaultAsync(x => x.BatchId == batchId, ct); if (batch is null) return null;
        if (batch.Status == "Completed") return Map(batch);
        if (batch.Status != "Validated") throw new BorrowerImportException("borrowerImports.batchNotExecutable"); batch.Status = "Executing"; await db.SaveChangesAsync(ct);
        foreach (var row in batch.Rows.Where(x => x.Status == "Valid"))
        {
            var input = JsonSerializer.Deserialize<BorrowerInput>(row.RawPayload)!;
            try
            {
                var borrower = Borrower.Register(input.CivilNumber, input.EmployeeNumber, input.FullName, input.PhoneNumber, input.Nationality, input.Organization, input.RankGrade, input.EmploymentInformation);
                if (await db.CivilExistsAsync(borrower.CivilNumber, null, ct)) throw new BorrowerConflictException("borrowers.civilNumberConflict");
                if (borrower.EmployeeNumber is not null && await db.EmployeeExistsAsync(borrower.EmployeeNumber, null, ct)) throw new BorrowerConflictException("borrowers.employeeNumberConflict");
                await db.AddAsync(borrower, ct); await db.SaveAsync(ct); row.BorrowerId = borrower.Id; row.Status = "Imported"; batch.ImportedRows++;
            }
            catch (BorrowerConflictException ex) { row.Status = "Failed"; row.ErrorCode = ex.Code; batch.FailedRows++; }
            catch (BorrowerValidationException) { row.Status = "Failed"; row.ErrorCode = "borrowers.validation"; batch.FailedRows++; }
        }
        batch.FailedRows += batch.InvalidRows; batch.Status = "Completed"; batch.CompletedAtUtc = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Map(batch);
    }
    private static BorrowerImportDto Map(BorrowerImportBatch batch) => new(batch.BatchId, batch.SourceDocumentId, batch.Status, batch.TotalRows, batch.ValidRows, batch.InvalidRows, batch.ImportedRows, batch.FailedRows, batch.CreatedAtUtc, batch.CompletedAtUtc, batch.Rows.OrderBy(x => x.RowNumber).Select(x => new BorrowerImportRowDto(x.RowNumber, x.Status, JsonSerializer.Deserialize<BorrowerInput>(x.RawPayload)!.CivilNumber, JsonSerializer.Deserialize<BorrowerInput>(x.RawPayload)!.EmployeeNumber, x.ErrorCode?.Split('|') ?? [], x.BorrowerId)).ToArray());
}
