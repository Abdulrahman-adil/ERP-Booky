using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPaymentsAndReceivables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "customer_payment_number_sequence",
                schema: "erp");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "erp",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                schema: "erp",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "erp",
                table: "payments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PostedAt",
                schema: "erp",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PostedByUserId",
                schema: "erp",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document_number",
                schema: "erp",
                table: "payments",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_aggregate_id",
                schema: "erp",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_event_type",
                schema: "erp",
                table: "payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_module",
                schema: "erp",
                table: "payments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE erp.payments
                SET document_number = 'CP-L-' || REPLACE("Id"::text, '-', ''),
                    source_module = 'Payments',
                    source_aggregate_id = "Id",
                    source_event_type = 'LegacyCustomerPayment'
                WHERE document_number IS NULL
                   OR source_module IS NULL
                   OR source_aggregate_id IS NULL
                   OR source_event_type IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "document_number",
                schema: "erp",
                table: "payments",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "source_aggregate_id",
                schema: "erp",
                table: "payments",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "source_event_type",
                schema: "erp",
                table: "payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "source_module",
                schema: "erp",
                table: "payments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PostingAccountId",
                schema: "erp",
                table: "cash_bank_accounts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "document_attachments",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_attachments_users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalSchema: "erp",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payments_CompanyId_document_number",
                schema: "erp",
                table: "payments",
                columns: new[] { "CompanyId", "document_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_CompanyId_Status_PaymentDate",
                schema: "erp",
                table: "payments",
                columns: new[] { "CompanyId", "Status", "PaymentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_CreatedByUserId",
                schema: "erp",
                table: "payments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_PostedByUserId",
                schema: "erp",
                table: "payments",
                column: "PostedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_source_module_source_aggregate_id_source_event_type",
                schema: "erp",
                table: "payments",
                columns: new[] { "source_module", "source_aggregate_id", "source_event_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_attachments_source_module_source_aggregate_id_sour~",
                schema: "erp",
                table: "document_attachments",
                columns: new[] { "source_module", "source_aggregate_id", "source_event_type" });

            migrationBuilder.CreateIndex(
                name: "IX_document_attachments_StorageKey",
                schema: "erp",
                table: "document_attachments",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_attachments_UploadedByUserId",
                schema: "erp",
                table: "document_attachments",
                column: "UploadedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_payments_users_CreatedByUserId",
                schema: "erp",
                table: "payments",
                column: "CreatedByUserId",
                principalSchema: "erp",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_users_PostedByUserId",
                schema: "erp",
                table: "payments",
                column: "PostedByUserId",
                principalSchema: "erp",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payments_users_CreatedByUserId",
                schema: "erp",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_users_PostedByUserId",
                schema: "erp",
                table: "payments");

            migrationBuilder.DropTable(
                name: "document_attachments",
                schema: "erp");

            migrationBuilder.DropIndex(
                name: "IX_payments_CompanyId_document_number",
                schema: "erp",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payments_CompanyId_Status_PaymentDate",
                schema: "erp",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payments_CreatedByUserId",
                schema: "erp",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payments_PostedByUserId",
                schema: "erp",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payments_source_module_source_aggregate_id_source_event_type",
                schema: "erp",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "erp",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                schema: "erp",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "erp",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "PostedAt",
                schema: "erp",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "PostedByUserId",
                schema: "erp",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "document_number",
                schema: "erp",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "source_aggregate_id",
                schema: "erp",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "source_event_type",
                schema: "erp",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "source_module",
                schema: "erp",
                table: "payments");

            migrationBuilder.DropSequence(
                name: "customer_payment_number_sequence",
                schema: "erp");

            migrationBuilder.AlterColumn<Guid>(
                name: "PostingAccountId",
                schema: "erp",
                table: "cash_bank_accounts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
