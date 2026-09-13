using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace LoanSystem.Modules.LoanOrigination.Infrastructure.Migrations;

[DbContext(typeof(LoanOriginationDbContext))]
[Migration("20260913010000_LoanOrigination_ApprovalOutbox")]
public sealed class LoanOriginationApprovalOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE [loan_origination].[outbox_messages] ([event_id] uniqueidentifier NOT NULL CONSTRAINT [PK_outbox_messages] PRIMARY KEY, [event_type] nvarchar(200) NOT NULL, [payload] nvarchar(max) NOT NULL, [occurred_at_utc] datetimeoffset NOT NULL, [processed_at_utc] datetimeoffset NULL, [attempts] int NOT NULL, [last_error] nvarchar(2000) NULL);
        CREATE INDEX [IX_outbox_pending] ON [loan_origination].[outbox_messages] ([processed_at_utc], [occurred_at_utc]);
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("outbox_messages", "loan_origination");
}
