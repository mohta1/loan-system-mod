using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace LoanSystem.Modules.LoanOrigination.Infrastructure.Migrations;

[DbContext(typeof(LoanOriginationDbContext))]
[Migration("20260907020000_LoanOrigination_UnitApproval")]
public sealed class LoanOriginationUnitApproval : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "unit_approval", schema: "loan_origination", table: "loan_applications", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "rejected_at_utc", schema: "loan_origination", table: "loan_applications", type: "datetimeoffset", nullable: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder) { migrationBuilder.DropColumn("unit_approval", "loan_applications", "loan_origination"); migrationBuilder.DropColumn("rejected_at_utc", "loan_applications", "loan_origination"); }
}
