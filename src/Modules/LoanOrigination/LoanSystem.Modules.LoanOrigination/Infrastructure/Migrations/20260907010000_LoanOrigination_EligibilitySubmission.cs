using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace LoanSystem.Modules.LoanOrigination.Infrastructure.Migrations;

[DbContext(typeof(LoanOriginationDbContext))]
[Migration("20260907010000_LoanOrigination_EligibilitySubmission")]
public sealed class LoanOriginationEligibilitySubmission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "eligibility_snapshot", schema: "loan_origination", table: "loan_applications", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "submitted_at_utc", schema: "loan_origination", table: "loan_applications", type: "datetimeoffset", nullable: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder) { migrationBuilder.DropColumn("eligibility_snapshot", "loan_applications", "loan_origination"); migrationBuilder.DropColumn("submitted_at_utc", "loan_applications", "loan_origination"); }
}
