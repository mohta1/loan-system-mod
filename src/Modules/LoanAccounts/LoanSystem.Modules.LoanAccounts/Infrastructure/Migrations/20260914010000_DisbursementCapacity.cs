using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace LoanSystem.Modules.LoanAccounts.Infrastructure.Migrations;

[DbContext(typeof(LoanAccountsDbContext))]
[Migration("20260914010000_DisbursementCapacity")]
public sealed class DisbursementCapacity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE [loan_accounts].[disbursement_reservations] ([loan_id] uniqueidentifier NOT NULL, [disbursement_id] uniqueidentifier NOT NULL, [amount] decimal(19,4) NOT NULL, [currency] char(3) NOT NULL, [status] nvarchar(30) NOT NULL, [reserved_at_utc] datetimeoffset NOT NULL, [updated_at_utc] datetimeoffset NOT NULL, CONSTRAINT [PK_disbursement_reservations] PRIMARY KEY ([loan_id],[disbursement_id]), CONSTRAINT [FK_reservations_loan] FOREIGN KEY ([loan_id]) REFERENCES [loan_accounts].[loan_accounts]([loan_id]));
        CREATE UNIQUE INDEX [UX_reservations_disbursement_id] ON [loan_accounts].[disbursement_reservations]([disbursement_id]);
        CREATE TABLE [loan_accounts].[financing_type_changes] ([change_id] uniqueidentifier NOT NULL CONSTRAINT [PK_financing_type_changes] PRIMARY KEY, [loan_id] uniqueidentifier NOT NULL, [actor_user_id] uniqueidentifier NOT NULL, [previous_financing_type] nvarchar(100) NOT NULL, [new_financing_type] nvarchar(100) NOT NULL, [changed_at_utc] datetimeoffset NOT NULL, [correlation_id] nvarchar(100) NOT NULL, CONSTRAINT [FK_financing_changes_loan] FOREIGN KEY ([loan_id]) REFERENCES [loan_accounts].[loan_accounts]([loan_id]));
        CREATE INDEX [IX_financing_type_changes_loan] ON [loan_accounts].[financing_type_changes]([loan_id]);
        CREATE TABLE [loan_accounts].[outbox_messages] ([event_id] uniqueidentifier NOT NULL CONSTRAINT [PK_loan_account_outbox] PRIMARY KEY, [event_type] nvarchar(200) NOT NULL, [payload] nvarchar(max) NOT NULL, [occurred_at_utc] datetimeoffset NOT NULL, [processed_at_utc] datetimeoffset NULL, [attempts] int NOT NULL, [last_error] nvarchar(2000) NULL);
        CREATE INDEX [IX_loan_account_outbox_pending] ON [loan_accounts].[outbox_messages]([processed_at_utc],[occurred_at_utc]);
        """);
    protected override void Down(MigrationBuilder migrationBuilder) { migrationBuilder.DropTable("outbox_messages", "loan_accounts"); migrationBuilder.DropTable("financing_type_changes", "loan_accounts"); migrationBuilder.DropTable("disbursement_reservations", "loan_accounts"); }
}
