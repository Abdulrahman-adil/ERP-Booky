using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchasingAndPayables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                schema: "erp",
                table: "purchase_invoice_lines",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateSequence(
                name: "purchase_invoice_number_sequence",
                schema: "erp");

            migrationBuilder.CreateSequence(
                name: "supplier_payment_number_sequence",
                schema: "erp");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "erp",
                table: "purchase_invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                schema: "erp",
                table: "purchase_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "erp",
                table: "purchase_invoices",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PostedAt",
                schema: "erp",
                table: "purchase_invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PostedByUserId",
                schema: "erp",
                table: "purchase_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                schema: "erp",
                table: "purchase_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Memo",
                schema: "erp",
                table: "purchase_invoice_lines",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "purchase_invoice_line_taxes",
                schema: "erp",
                columns: table => new
                {
                    PurchaseInvoiceLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_tax_code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_invoice_line_taxes", x => x.PurchaseInvoiceLineId);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_line_taxes_purchase_invoice_lines_Purchase~",
                        column: x => x.PurchaseInvoiceLineId,
                        principalSchema: "erp",
                        principalTable: "purchase_invoice_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_line_taxes_tax_codes_tax_tax_code_id",
                        column: x => x.tax_tax_code_id,
                        principalSchema: "erp",
                        principalTable: "tax_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_CompanyId_Status_InvoiceDate",
                schema: "erp",
                table: "purchase_invoices",
                columns: new[] { "CompanyId", "Status", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_CreatedByUserId",
                schema: "erp",
                table: "purchase_invoices",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_PostedByUserId",
                schema: "erp",
                table: "purchase_invoices",
                column: "PostedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_WarehouseId",
                schema: "erp",
                table: "purchase_invoices",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_line_taxes_tax_tax_code_id",
                schema: "erp",
                table: "purchase_invoice_line_taxes",
                column: "tax_tax_code_id");

            migrationBuilder.Sql("""
                INSERT INTO erp.purchase_invoice_line_taxes ("PurchaseInvoiceLineId", tax_tax_code_id, tax_rate)
                SELECT "Id", tax_tax_code_id, tax_rate
                FROM erp.purchase_invoice_lines
                WHERE tax_tax_code_id <> '00000000-0000-0000-0000-000000000000'::uuid;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_invoices_users_CreatedByUserId",
                schema: "erp",
                table: "purchase_invoices",
                column: "CreatedByUserId",
                principalSchema: "erp",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_invoices_users_PostedByUserId",
                schema: "erp",
                table: "purchase_invoices",
                column: "PostedByUserId",
                principalSchema: "erp",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_invoices_warehouses_WarehouseId",
                schema: "erp",
                table: "purchase_invoices",
                column: "WarehouseId",
                principalSchema: "erp",
                principalTable: "warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_purchase_invoices_users_CreatedByUserId",
                schema: "erp",
                table: "purchase_invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_invoices_users_PostedByUserId",
                schema: "erp",
                table: "purchase_invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_invoices_warehouses_WarehouseId",
                schema: "erp",
                table: "purchase_invoices");

            migrationBuilder.DropTable(
                name: "purchase_invoice_line_taxes",
                schema: "erp");

            migrationBuilder.DropIndex(
                name: "IX_purchase_invoices_CompanyId_Status_InvoiceDate",
                schema: "erp",
                table: "purchase_invoices");

            migrationBuilder.DropIndex(
                name: "IX_purchase_invoices_CreatedByUserId",
                schema: "erp",
                table: "purchase_invoices");

            migrationBuilder.DropIndex(
                name: "IX_purchase_invoices_PostedByUserId",
                schema: "erp",
                table: "purchase_invoices");

            migrationBuilder.DropIndex(
                name: "IX_purchase_invoices_WarehouseId",
                schema: "erp",
                table: "purchase_invoices");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "erp",
                table: "purchase_invoices");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                schema: "erp",
                table: "purchase_invoices");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "erp",
                table: "purchase_invoices");

            migrationBuilder.DropColumn(
                name: "PostedAt",
                schema: "erp",
                table: "purchase_invoices");

            migrationBuilder.DropColumn(
                name: "PostedByUserId",
                schema: "erp",
                table: "purchase_invoices");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                schema: "erp",
                table: "purchase_invoices");

            migrationBuilder.DropColumn(
                name: "Memo",
                schema: "erp",
                table: "purchase_invoice_lines");

            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                schema: "erp",
                table: "purchase_invoice_lines");

            migrationBuilder.DropSequence(
                name: "purchase_invoice_number_sequence",
                schema: "erp");

            migrationBuilder.DropSequence(
                name: "supplier_payment_number_sequence",
                schema: "erp");

        }
    }
}
