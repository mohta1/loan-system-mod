using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
#nullable disable
namespace LoanSystem.Modules.Documents.Infrastructure.Migrations;

[DbContext(typeof(DocumentsDbContext))]
[Migration("20260822000000_Documents_Initial")]
public partial class DocumentsInitial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) { migrationBuilder.EnsureSchema("documents"); migrationBuilder.CreateTable(name: "documents", schema: "documents", columns: t => new { Id = t.Column<Guid>("uniqueidentifier", nullable: false), FileName = t.Column<string>("nvarchar(255)", maxLength: 255, nullable: false), ContentType = t.Column<string>("nvarchar(127)", maxLength: 127, nullable: false), Size = t.Column<long>("bigint", nullable: false), StorageKey = t.Column<string>("nvarchar(64)", maxLength: 64, nullable: false), UploaderId = t.Column<Guid>("uniqueidentifier", nullable: false), UploadedAt = t.Column<DateTimeOffset>("datetimeoffset", nullable: false) }, constraints: t => t.PrimaryKey("PK_documents", x => x.Id)); migrationBuilder.CreateIndex(name: "IX_documents_StorageKey", schema: "documents", table: "documents", column: "StorageKey", unique: true); }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("documents", "documents");
}
