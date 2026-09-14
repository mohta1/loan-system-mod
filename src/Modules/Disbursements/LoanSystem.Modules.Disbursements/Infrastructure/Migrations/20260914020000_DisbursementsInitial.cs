using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace LoanSystem.Modules.Disbursements.Infrastructure.Migrations;

[DbContext(typeof(DisbursementsDbContext))]
[Migration("20260914020000_DisbursementsInitial")]
public sealed class DisbursementsInitial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        IF SCHEMA_ID(N'disbursements') IS NULL EXEC(N'CREATE SCHEMA [disbursements]');
        CREATE TABLE [disbursements].[disbursements] ([disbursement_id] uniqueidentifier NOT NULL CONSTRAINT [PK_disbursements] PRIMARY KEY, [loan_id] uniqueidentifier NOT NULL, [amount] decimal(19,4) NOT NULL, [currency] char(3) NOT NULL, [beneficiary_snapshot] nvarchar(max) NOT NULL, [status] nvarchar(30) NOT NULL, [capacity_reservation_status] nvarchar(30) NOT NULL, [capacity_rejection_reason_code] nvarchar(100) NULL, [capacity_rejection_reason] nvarchar(500) NULL, [actor_user_id] uniqueidentifier NOT NULL, [created_at_utc] datetimeoffset NOT NULL, [updated_at_utc] datetimeoffset NOT NULL, [correlation_id] nvarchar(100) NOT NULL, [creation_token_hash] nvarchar(64) NOT NULL, [row_version] rowversion NOT NULL);
        CREATE UNIQUE INDEX [UX_disbursements_creation_token] ON [disbursements].[disbursements]([creation_token_hash]); CREATE INDEX [IX_disbursements_loan_id] ON [disbursements].[disbursements]([loan_id]); CREATE INDEX [IX_disbursements_status] ON [disbursements].[disbursements]([status]); CREATE INDEX [IX_disbursements_created_at] ON [disbursements].[disbursements]([created_at_utc]);
        CREATE TABLE [disbursements].[supporting_documents] ([disbursement_id] uniqueidentifier NOT NULL, [document_id] uniqueidentifier NOT NULL, CONSTRAINT [PK_disbursement_documents] PRIMARY KEY ([disbursement_id],[document_id]), CONSTRAINT [FK_disbursement_documents] FOREIGN KEY ([disbursement_id]) REFERENCES [disbursements].[disbursements]([disbursement_id]) ON DELETE CASCADE);
        CREATE TABLE [disbursements].[outbox_messages] ([event_id] uniqueidentifier NOT NULL CONSTRAINT [PK_disbursement_outbox] PRIMARY KEY, [event_type] nvarchar(200) NOT NULL, [payload] nvarchar(max) NOT NULL, [occurred_at_utc] datetimeoffset NOT NULL, [processed_at_utc] datetimeoffset NULL, [attempts] int NOT NULL, [last_error] nvarchar(2000) NULL); CREATE INDEX [IX_disbursement_outbox_pending] ON [disbursements].[outbox_messages]([processed_at_utc],[occurred_at_utc]);
        CREATE TABLE [disbursements].[inbox_messages] ([event_id] uniqueidentifier NOT NULL CONSTRAINT [PK_disbursement_inbox] PRIMARY KEY, [occurred_at_utc] datetimeoffset NOT NULL, [processed_at_utc] datetimeoffset NOT NULL);
        IF SCHEMA_ID(N'platform') IS NULL EXEC(N'CREATE SCHEMA [platform]');
        CREATE TABLE [platform].[idempotency_records] ([id] uniqueidentifier NOT NULL CONSTRAINT [PK_idempotency_records] PRIMARY KEY, [scope] nvarchar(100) NOT NULL, [actor_user_id] uniqueidentifier NOT NULL, [key_hash] nvarchar(64) NOT NULL, [request_hash] nvarchar(64) NOT NULL, [resource_id] uniqueidentifier NOT NULL, [created_at_utc] datetimeoffset NOT NULL); CREATE UNIQUE INDEX [UX_idempotency_scope_actor_key] ON [platform].[idempotency_records]([scope],[actor_user_id],[key_hash]);
        """);
    protected override void Down(MigrationBuilder migrationBuilder) { migrationBuilder.DropTable("idempotency_records", "platform"); migrationBuilder.DropTable("inbox_messages", "disbursements"); migrationBuilder.DropTable("outbox_messages", "disbursements"); migrationBuilder.DropTable("supporting_documents", "disbursements"); migrationBuilder.DropTable("disbursements", "disbursements"); }
}
