using LoanSystem.Modules.Borrowers.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoanSystem.Modules.Borrowers.Infrastructure.Migrations;

[DbContext(typeof(BorrowersDbContext))]
[Migration("20260822020000_Borrowers_Initial")]
public partial class Borrowers_Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("borrowers");
        migrationBuilder.CreateTable(
            name: "borrowers",
            schema: "borrowers",
            columns: table => new
            {
                Id = table.Column<Guid>("uniqueidentifier", nullable: false),
                CivilNumber = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
                EmployeeNumber = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: true),
                FullName = table.Column<string>("nvarchar(200)", maxLength: 200, nullable: false),
                PhoneNumber = table.Column<string>("nvarchar(50)", maxLength: 50, nullable: true),
                Nationality = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
                Organization = table.Column<string>("nvarchar(200)", maxLength: 200, nullable: false),
                RankGrade = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: true),
                EmploymentInformation = table.Column<string>("nvarchar(1000)", maxLength: 1000, nullable: true),
                Status = table.Column<string>("nvarchar(20)", maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>("datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>("datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>("rowversion", rowVersion: true, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_borrowers", x => x.Id));
        migrationBuilder.CreateIndex("IX_borrowers_CivilNumber", "borrowers", "CivilNumber", schema: "borrowers", unique: true);
        migrationBuilder.CreateIndex("IX_borrowers_EmployeeNumber", "borrowers", "EmployeeNumber", schema: "borrowers", unique: true, filter: "[EmployeeNumber] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_borrowers_FullName", "borrowers", "FullName", schema: "borrowers");
        migrationBuilder.CreateIndex("IX_borrowers_Organization", "borrowers", "Organization", schema: "borrowers");
        migrationBuilder.CreateIndex("IX_borrowers_Status", "borrowers", "Status", schema: "borrowers");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("borrowers", "borrowers");
}
