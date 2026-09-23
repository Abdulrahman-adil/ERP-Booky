using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesInvoicingAndReceivables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "sales_invoice_number_sequence",
                schema: "erp");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "erp",
                table: "sales_invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                schema: "erp",
                table: "sales_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "erp",
                table: "sales_invoices",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PostedAt",
                schema: "erp",
                table: "sales_invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PostedByUserId",
                schema: "erp",
                table: "sales_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                schema: "erp",
                table: "sales_invoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                schema: "erp",
                table: "sales_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                schema: "erp",
                table: "open_items",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sales_invoice_line_taxes",
                schema: "erp",
                columns: table => new
                {
                    SalesInvoiceLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_tax_code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoice_line_taxes", x => x.SalesInvoiceLineId);
                    table.ForeignKey(
                        name: "FK_sales_invoice_line_taxes_sales_invoice_lines_SalesInvoiceLi~",
                        column: x => x.SalesInvoiceLineId,
                        principalSchema: "erp",
                        principalTable: "sales_invoice_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sales_invoice_line_taxes_tax_codes_tax_tax_code_id",
                        column: x => x.tax_tax_code_id,
                        principalSchema: "erp",
                        principalTable: "tax_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO erp.sales_invoice_line_taxes ("SalesInvoiceLineId", tax_tax_code_id, tax_rate)
                SELECT "Id", tax_tax_code_id, tax_rate
                FROM erp.sales_invoice_lines;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_sales_invoice_lines_tax_codes_tax_tax_code_id",
                schema: "erp",
                table: "sales_invoice_lines");

            migrationBuilder.DropIndex(
                name: "IX_sales_invoice_lines_tax_tax_code_id",
                schema: "erp",
                table: "sales_invoice_lines");

            migrationBuilder.DropColumn(
                name: "tax_rate",
                schema: "erp",
                table: "sales_invoice_lines");

            migrationBuilder.DropColumn(
                name: "tax_tax_code_id",
                schema: "erp",
                table: "sales_invoice_lines");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_CompanyId_Status_InvoiceDate",
                schema: "erp",
                table: "sales_invoices",
                columns: new[] { "CompanyId", "Status", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_CreatedByUserId",
                schema: "erp",
                table: "sales_invoices",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_PostedByUserId",
                schema: "erp",
                table: "sales_invoices",
                column: "PostedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_WarehouseId",
                schema: "erp",
                table: "sales_invoices",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoice_line_taxes_tax_tax_code_id",
                schema: "erp",
                table: "sales_invoice_line_taxes",
                column: "tax_tax_code_id");

            migrationBuilder.AddForeignKey(
                name: "FK_sales_invoices_users_CreatedByUserId",
                schema: "erp",
                table: "sales_invoices",
                column: "CreatedByUserId",
                principalSchema: "erp",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sales_invoices_users_PostedByUserId",
                schema: "erp",
                table: "sales_invoices",
                column: "PostedByUserId",
                principalSchema: "erp",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sales_invoices_warehouses_WarehouseId",
                schema: "erp",
                table: "sales_invoices",
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
                name: "FK_sales_invoices_users_CreatedByUserId",
                schema: "erp",
                table: "sales_invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_sales_invoices_users_PostedByUserId",
                schema: "erp",
                table: "sales_invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_sales_invoices_warehouses_WarehouseId",
                schema: "erp",
                table: "sales_invoices");

            migrationBuilder.DropTable(
                name: "sales_invoice_line_taxes",
                schema: "erp");

            migrationBuilder.DropIndex(
                name: "IX_sales_invoices_CompanyId_Status_InvoiceDate",
                schema: "erp",
                table: "sales_invoices");

            migrationBuilder.DropIndex(
                name: "IX_sales_invoices_CreatedByUserId",
                schema: "erp",
                table: "sales_invoices");

            migrationBuilder.DropIndex(
                name: "IX_sales_invoices_PostedByUserId",
                schema: "erp",
                table: "sales_invoices");

            migrationBuilder.DropIndex(
                name: "IX_sales_invoices_WarehouseId",
                schema: "erp",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "erp",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                schema: "erp",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "erp",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "PostedAt",
                schema: "erp",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "PostedByUserId",
                schema: "erp",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "Reference",
                schema: "erp",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                schema: "erp",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "DueDate",
                schema: "erp",
                table: "open_items");

            migrationBuilder.DropSequence(
                name: "sales_invoice_number_sequence",
                schema: "erp");

            migrationBuilder.AddColumn<decimal>(
                name: "tax_rate",
                schema: "erp",
                table: "sales_invoice_lines",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "tax_tax_code_id",
                schema: "erp",
                table: "sales_invoice_lines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoice_lines_tax_tax_code_id",
                schema: "erp",
                table: "sales_invoice_lines",
                column: "tax_tax_code_id");

            migrationBuilder.AddForeignKey(
                name: "FK_sales_invoice_lines_tax_codes_tax_tax_code_id",
                schema: "erp",
                table: "sales_invoice_lines",
                column: "tax_tax_code_id",
                principalSchema: "erp",
                principalTable: "tax_codes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
