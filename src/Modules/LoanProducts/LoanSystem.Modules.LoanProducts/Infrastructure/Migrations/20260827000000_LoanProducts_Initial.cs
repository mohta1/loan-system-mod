using LoanSystem.Modules.LoanProducts.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoanSystem.Modules.LoanProducts.Infrastructure.Migrations;

[DbContext(typeof(LoanProductsDbContext))]
[Migration("20260827000000_LoanProducts_Initial")]
public sealed class LoanProductsInitial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("loan_products");
        migrationBuilder.CreateTable(
            name: "loan_products",
            schema: "loan_products",
            columns: table => new
            {
                loan_product_id = table.Column<Guid>("uniqueidentifier", nullable: false),
                name = table.Column<string>("nvarchar(200)", maxLength: 200, nullable: false),
                status = table.Column<string>("nvarchar(20)", maxLength: 20, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>("datetimeoffset", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>("datetimeoffset", nullable: false),
                row_version = table.Column<byte[]>("rowversion", rowVersion: true, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_loan_products", x => x.loan_product_id));
        migrationBuilder.CreateTable(
            name: "loan_product_versions",
            schema: "loan_products",
            columns: table => new
            {
                version_id = table.Column<Guid>("uniqueidentifier", nullable: false),
                loan_product_id = table.Column<Guid>("uniqueidentifier", nullable: false),
                version_number = table.Column<int>("int", nullable: false),
                maximum_amount = table.Column<decimal>("decimal(19,4)", nullable: false),
                currency = table.Column<string>("char(3)", nullable: false),
                deduction_percentage = table.Column<decimal>("decimal(9,4)", nullable: false),
                eligibility_configuration = table.Column<string>("nvarchar(max)", nullable: false),
                effective_from = table.Column<DateOnly>("date", nullable: false),
                effective_to = table.Column<DateOnly>("date", nullable: true),
                status = table.Column<string>("nvarchar(20)", maxLength: 20, nullable: false),
                published_at_utc = table.Column<DateTimeOffset>("datetimeoffset", nullable: true),
                created_at_utc = table.Column<DateTimeOffset>("datetimeoffset", nullable: false),
                row_version = table.Column<byte[]>("rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_loan_product_versions", x => x.version_id);
                table.ForeignKey(
                    name: "FK_versions_products",
                    column: x => x.loan_product_id,
                    principalSchema: "loan_products",
                    principalTable: "loan_products",
                    principalColumn: "loan_product_id",
                    onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_versions_product_number", "loan_product_versions", ["loan_product_id", "version_number"], "loan_products", unique: true);
        migrationBuilder.CreateTable(
            name: "loan_product_financing_types",
            schema: "loan_products",
            columns: table => new
            {
                version_id = table.Column<Guid>("uniqueidentifier", nullable: false),
                financing_type = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_loan_product_financing_types", x => new { x.version_id, x.financing_type });
                table.ForeignKey(
                    name: "FK_financing_types_versions",
                    column: x => x.version_id,
                    principalSchema: "loan_products",
                    principalTable: "loan_product_versions",
                    principalColumn: "version_id",
                    onDelete: ReferentialAction.Cascade);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("loan_product_financing_types", "loan_products");
        migrationBuilder.DropTable("loan_product_versions", "loan_products");
        migrationBuilder.DropTable("loan_products", "loan_products");
    }
}
