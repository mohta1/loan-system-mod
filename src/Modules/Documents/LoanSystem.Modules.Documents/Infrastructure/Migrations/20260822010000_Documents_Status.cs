using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace LoanSystem.Modules.Documents.Infrastructure.Migrations;

[DbContext(typeof(DocumentsDbContext))]
[Migration("20260822010000_Documents_Status")]
public partial class DocumentsStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>(name: "Status", schema: "documents", table: "documents", type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "Active");
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(name: "Status", schema: "documents", table: "documents");
}
