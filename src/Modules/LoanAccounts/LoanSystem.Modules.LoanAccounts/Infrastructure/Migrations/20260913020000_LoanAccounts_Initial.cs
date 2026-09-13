using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace LoanSystem.Modules.LoanAccounts.Infrastructure.Migrations;

[DbContext(typeof(LoanAccountsDbContext))]
[Migration("20260913020000_LoanAccounts_Initial")]
public sealed class LoanAccountsInitial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        IF SCHEMA_ID(N'loan_accounts') IS NULL EXEC(N'CREATE SCHEMA [loan_accounts]');
        CREATE TABLE [loan_accounts].[loan_accounts] ([loan_id] uniqueidentifier NOT NULL CONSTRAINT [PK_loan_accounts] PRIMARY KEY, [source_application_id] uniqueidentifier NOT NULL, [borrower_id] uniqueidentifier NOT NULL, [loan_product_id] uniqueidentifier NOT NULL, [loan_product_version_id] uniqueidentifier NOT NULL, [approved_amount] decimal(19,4) NOT NULL, [currency] char(3) NOT NULL, [financing_type] nvarchar(100) NOT NULL, [reserved_disbursement_amount] decimal(19,4) NOT NULL, [total_disbursed] decimal(19,4) NOT NULL, [total_repaid] decimal(19,4) NOT NULL, [status] nvarchar(30) NOT NULL, [opened_at_utc] datetimeoffset NOT NULL, [row_version] rowversion NOT NULL);
        CREATE UNIQUE INDEX [UX_loan_accounts_source_application_id] ON [loan_accounts].[loan_accounts] ([source_application_id]);
        CREATE INDEX [IX_loan_accounts_borrower_id] ON [loan_accounts].[loan_accounts] ([borrower_id]); CREATE INDEX [IX_loan_accounts_status] ON [loan_accounts].[loan_accounts] ([status]);
        CREATE TABLE [loan_accounts].[inbox_messages] ([event_id] uniqueidentifier NOT NULL CONSTRAINT [PK_inbox_messages] PRIMARY KEY, [occurred_at_utc] datetimeoffset NOT NULL, [processed_at_utc] datetimeoffset NOT NULL);
        """);
    protected override void Down(MigrationBuilder migrationBuilder) { migrationBuilder.DropTable("inbox_messages", "loan_accounts"); migrationBuilder.DropTable("loan_accounts", "loan_accounts"); }
}
