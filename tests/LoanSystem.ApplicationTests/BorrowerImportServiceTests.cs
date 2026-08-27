using LoanSystem.Contracts;
using LoanSystem.Modules.Borrowers.Application;

namespace LoanSystem.ApplicationTests;

public sealed class BorrowerImportServiceTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly BorrowerImportOptions Limits = new() { MaximumFileSizeBytes = 10, MaximumRows = 20 };

    [Theory]
    [InlineData("borrowers.xls", BorrowerImportService.ContentType)]
    [InlineData("borrowers.xlsx", "application/octet-stream")]
    public async Task Validate_rejects_unsupported_name_or_content_type_before_side_effects(string name, string contentType)
    {
        var context = Context();
        var exception = await Assert.ThrowsAsync<BorrowerImportException>(() => context.Service.ValidateAsync(name, contentType, 1, new MemoryStream([1]), UserId, Limits, default));
        Assert.Equal("borrowerImports.unsupportedFile", exception.Code); Assert.Equal(0, context.Parser.Calls); Assert.Equal(0, context.Documents.Calls); Assert.Equal(0, context.Store.CreateCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task Validate_rejects_empty_or_oversized_content_before_parsing(long length)
    {
        var context = Context();
        var exception = await Assert.ThrowsAsync<BorrowerImportException>(() => context.Service.ValidateAsync("borrowers.xlsx", BorrowerImportService.ContentType, length, new MemoryStream(new byte[Math.Max(1, (int)length)]), UserId, Limits, default));
        Assert.Equal("borrowerImports.invalidFile", exception.Code); Assert.Equal(0, context.Parser.Calls); Assert.Equal(0, context.Documents.Calls);
    }

    [Fact]
    public async Task Validate_preserves_known_parser_error_and_converts_unknown_parser_failure()
    {
        var known = Context(parserError: new BorrowerImportException("borrowerImports.invalidTemplate"));
        Assert.Equal("borrowerImports.invalidTemplate", (await Assert.ThrowsAsync<BorrowerImportException>(() => Validate(known))).Code);
        var unknown = Context(parserError: new InvalidOperationException("parser detail"));
        Assert.Equal("borrowerImports.invalidFile", (await Assert.ThrowsAsync<BorrowerImportException>(() => Validate(unknown))).Code);
        Assert.Equal(0, known.Documents.Calls); Assert.Equal(0, unknown.Documents.Calls);
    }

    [Fact]
    public async Task Validate_combines_distinct_file_duplicates_existing_conflicts_and_parser_errors()
    {
        var rows = new[]
        {
            Row(2, "civil-1", "employee-1", "borrowers.validation"),
            Row(3, "civil-1", "employee-2"),
            Row(4, "civil-3", "employee-2"),
            Row(5, "civil-existing", "employee-existing", "borrowers.validation")
        };
        var context = Context(rows); context.Store.ExistingCivil.Add("civil-existing"); context.Store.ExistingEmployee.Add("employee-existing");
        var result = await Validate(context, "../unsafe.xlsx");
        Assert.Equal(context.Store.Result, result); Assert.Equal(1, context.Documents.Calls); Assert.Equal("unsafe.xlsx", context.Documents.FileName); Assert.Equal(BorrowerImportService.ContentType, context.Documents.ContentType); Assert.Equal(UserId, context.Documents.UserId); Assert.Equal(4, context.Documents.Length);
        Assert.Equal(1, context.Store.CreateCalls); Assert.Equal(context.Documents.DocumentId, context.Store.SourceDocumentId); Assert.Equal(UserId, context.Store.CreatedBy);
        Assert.Equal(["borrowers.validation", "borrowerImports.duplicateCivilNumberInFile"], context.Store.Rows![0].Errors);
        Assert.Equal(["borrowerImports.duplicateCivilNumberInFile", "borrowerImports.duplicateEmployeeNumberInFile"], context.Store.Rows[1].Errors);
        Assert.Equal(["borrowerImports.duplicateEmployeeNumberInFile"], context.Store.Rows[2].Errors);
        Assert.Equal(["borrowers.validation", "borrowers.civilNumberConflict", "borrowers.employeeNumberConflict"], context.Store.Rows[3].Errors);
    }

    [Fact]
    public async Task Validate_stores_valid_source_then_creates_preview_with_normalized_arguments()
    {
        var context = Context([Row(2, "civil", null)]);
        await Validate(context);
        Assert.Equal(20, context.Parser.MaximumRows); Assert.True(context.Documents.ContentWasReadable); Assert.Single(context.Store.Rows!);
    }

    [Fact]
    public async Task Validate_discards_source_document_when_batch_persistence_fails()
    {
        var context = Context(); context.Store.CreateError = new InvalidOperationException("persistence failed");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Validate(context));
        Assert.Equal(1, context.Documents.DiscardCalls); Assert.Equal(context.Documents.DocumentId, context.Documents.DiscardedDocumentId); Assert.Equal(UserId, context.Documents.DiscardedBy);
    }

    [Fact]
    public async Task Get_and_execute_delegate_existing_and_missing_results()
    {
        var context = Context(); var id = Guid.NewGuid(); context.Store.GetResult = context.Store.Result; context.Store.ExecuteResult = context.Store.Result;
        Assert.Same(context.Store.Result, await context.Service.GetAsync(id, default)); Assert.Equal(id, context.Store.LastGetId);
        Assert.Same(context.Store.Result, await context.Service.ExecuteAsync(id, default)); Assert.Equal(id, context.Store.LastExecuteId);
        context.Store.GetResult = null; context.Store.ExecuteResult = null;
        Assert.Null(await context.Service.GetAsync(id, default)); Assert.Null(await context.Service.ExecuteAsync(id, default));
    }

    private static ParsedBorrowerRow Row(int number, string civil, string? employee, params string[] errors) => new(number, new(civil, employee, "Name", null, "Omani", "MOD", null, null), errors);
    private static Task<BorrowerImportDto> Validate(ContextData context, string name = "borrowers.xlsx") => context.Service.ValidateAsync(name, BorrowerImportService.ContentType, 4, new MemoryStream([1, 2, 3, 4]), UserId, Limits, default);
    private static ContextData Context(IReadOnlyList<ParsedBorrowerRow>? rows = null, Exception? parserError = null)
    {
        var parser = new ParserFake(rows ?? [Row(2, "civil", "employee")], parserError); var store = new StoreFake(); var documents = new DocumentFake();
        return new(new(parser, store, documents), parser, store, documents);
    }
    private sealed record ContextData(BorrowerImportService Service, ParserFake Parser, StoreFake Store, DocumentFake Documents);
    private sealed class ParserFake(IReadOnlyList<ParsedBorrowerRow> rows, Exception? error) : IBorrowerWorkbookParser
    {
        public int Calls { get; private set; }
        public int MaximumRows { get; private set; }
        public IReadOnlyList<ParsedBorrowerRow> Parse(Stream workbook, int maximumRows) { Calls++; MaximumRows = maximumRows; if (error is not null) throw error; return rows; }
    }
    private sealed class DocumentFake : IImportSourceDocumentStore
    {
        public Guid DocumentId { get; } = Guid.NewGuid(); public int Calls { get; private set; }
        public string? FileName { get; private set; }
        public string? ContentType { get; private set; }
        public long Length { get; private set; }
        public Guid UserId { get; private set; }
        public bool ContentWasReadable { get; private set; }
        public int DiscardCalls { get; private set; }
        public Guid DiscardedDocumentId { get; private set; }
        public Guid DiscardedBy { get; private set; }
        public Task<Guid> StoreAsync(string fileName, string contentType, long length, Stream content, Guid uploadedBy, CancellationToken cancellationToken) { Calls++; FileName = fileName; ContentType = contentType; Length = length; UserId = uploadedBy; ContentWasReadable = content.ReadByte() >= 0; return Task.FromResult(DocumentId); }
        public Task DiscardAsync(Guid documentId, Guid uploadedBy, CancellationToken cancellationToken) { DiscardCalls++; DiscardedDocumentId = documentId; DiscardedBy = uploadedBy; return Task.CompletedTask; }
    }
    private sealed class StoreFake : IBorrowerImportStore
    {
        public HashSet<string> ExistingCivil { get; } = []; public HashSet<string> ExistingEmployee { get; } = []; public int CreateCalls { get; private set; }
        public Guid SourceDocumentId { get; private set; }
        public Guid CreatedBy { get; private set; }
        public IReadOnlyList<ParsedBorrowerRow>? Rows { get; private set; }
        public BorrowerImportDto Result { get; } = new(Guid.NewGuid(), Guid.NewGuid(), "Validated", 1, 1, 0, 0, 0, DateTimeOffset.UtcNow, null, []); public BorrowerImportDto? GetResult { get; set; }
        public BorrowerImportDto? ExecuteResult { get; set; }
        public Guid LastGetId { get; private set; }
        public Guid LastExecuteId { get; private set; }
        public Exception? CreateError { get; set; }
        public Task<IReadOnlySet<string>> ExistingCivilNumbersAsync(IEnumerable<string> values, CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(ExistingCivil);
        public Task<IReadOnlySet<string>> ExistingEmployeeNumbersAsync(IEnumerable<string> values, CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(ExistingEmployee);
        public Task<BorrowerImportDto> CreateAsync(Guid sourceDocumentId, Guid createdBy, IReadOnlyList<ParsedBorrowerRow> rows, CancellationToken ct) { CreateCalls++; SourceDocumentId = sourceDocumentId; CreatedBy = createdBy; Rows = rows; return CreateError is null ? Task.FromResult(Result) : Task.FromException<BorrowerImportDto>(CreateError); }
        public Task<BorrowerImportDto?> GetAsync(Guid batchId, CancellationToken ct) { LastGetId = batchId; return Task.FromResult(GetResult); }
        public Task<BorrowerImportDto?> ExecuteAsync(Guid batchId, CancellationToken ct) { LastExecuteId = batchId; return Task.FromResult(ExecuteResult); }
    }
}
