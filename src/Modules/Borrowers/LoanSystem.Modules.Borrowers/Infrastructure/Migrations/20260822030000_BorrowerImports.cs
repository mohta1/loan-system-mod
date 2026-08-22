using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace LoanSystem.Modules.Borrowers.Infrastructure.Migrations;

[DbContext(typeof(BorrowersDbContext))]
[Migration("20260822030000_BorrowerImports")]
public partial class BorrowerImports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "import_batches", schema: "borrowers", columns: table => new
        {
            BatchId = table.Column<Guid>("uniqueidentifier", nullable: false),
            SourceDocumentId = table.Column<Guid>("uniqueidentifier", nullable: false),
            Status = table.Column<string>("nvarchar(20)", maxLength: 20, nullable: false),
            TotalRows = table.Column<int>("int", nullable: false),
            ValidRows = table.Column<int>("int", nullable: false),
            InvalidRows = table.Column<int>("int", nullable: false),
            ImportedRows = table.Column<int>("int", nullable: false),
            FailedRows = table.Column<int>("int", nullable: false),
            CreatedBy = table.Column<Guid>("uniqueidentifier", nullable: false),
            CreatedAtUtc = table.Column<DateTimeOffset>("datetimeoffset", nullable: false),
            CompletedAtUtc = table.Column<DateTimeOffset>("datetimeoffset", nullable: true),
            IdempotencyKey = table.Column<string>("nvarchar(32)", maxLength: 32, nullable: false),
            RowVersion = table.Column<byte[]>("rowversion", rowVersion: true, nullable: false)
        }, constraints: table => table.PrimaryKey("PK_import_batches", x => x.BatchId));
        migrationBuilder.CreateTable(name: "import_rows", schema: "borrowers", columns: table => new
        {
            BatchId = table.Column<Guid>("uniqueidentifier", nullable: false),
            RowNumber = table.Column<int>("int", nullable: false),
            RawPayload = table.Column<string>("nvarchar(max)", nullable: false),
            Status = table.Column<string>("nvarchar(20)", maxLength: 20, nullable: false),
            ErrorCode = table.Column<string>("nvarchar(1000)", maxLength: 1000, nullable: true),
            ErrorMessage = table.Column<string>("nvarchar(1000)", maxLength: 1000, nullable: true),
            BorrowerId = table.Column<Guid>("uniqueidentifier", nullable: true)
        }, constraints: table => { table.PrimaryKey("PK_import_rows", x => new { x.BatchId, x.RowNumber }); table.ForeignKey("FK_import_rows_import_batches_BatchId", x => x.BatchId, "import_batches", "BatchId", "borrowers", onDelete: ReferentialAction.Cascade); });
        migrationBuilder.CreateIndex("IX_import_batches_IdempotencyKey", "import_batches", "IdempotencyKey", "borrowers", unique: true); migrationBuilder.CreateIndex("IX_import_batches_Status", "import_batches", "Status", "borrowers");
    }
    protected override void Down(MigrationBuilder migrationBuilder) { migrationBuilder.DropTable("import_rows", "borrowers"); migrationBuilder.DropTable("import_batches", "borrowers"); }
}
