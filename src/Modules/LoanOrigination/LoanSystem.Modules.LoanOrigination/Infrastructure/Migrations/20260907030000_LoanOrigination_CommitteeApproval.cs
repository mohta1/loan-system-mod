using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace LoanSystem.Modules.LoanOrigination.Infrastructure.Migrations;

[DbContext(typeof(LoanOriginationDbContext))]
[Migration("20260907030000_LoanOrigination_CommitteeApproval")]
public sealed class LoanOriginationCommitteeApproval : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>(name: "committee_approval", schema: "loan_origination", table: "loan_applications", type: "nvarchar(max)", nullable: true);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn("committee_approval", "loan_applications", "loan_origination");
}
