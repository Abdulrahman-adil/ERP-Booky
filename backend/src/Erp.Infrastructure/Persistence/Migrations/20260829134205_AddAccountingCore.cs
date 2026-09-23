using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_journal_entries_AccountingTransactionId",
                schema: "erp",
                table: "journal_entries");

            migrationBuilder.CreateSequence(
                name: "journal_entry_number_sequence",
                schema: "erp");

            migrationBuilder.AddColumn<Guid>(
                name: "FinancialClassId",
                schema: "erp",
                table: "journal_entry_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Memo",
                schema: "erp",
                table: "journal_entry_lines",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "erp",
                table: "journal_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                schema: "erp",
                table: "journal_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PostedAt",
                schema: "erp",
                table: "journal_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PostedByUserId",
                schema: "erp",
                table: "journal_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                schema: "erp",
                table: "journal_entries",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountRole",
                schema: "erp",
                table: "accounts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<int>(
                name: "HierarchyLevel",
                schema: "erp",
                table: "accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "financial_classes",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_classes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financial_classes_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entry_lines_FinancialClassId",
                schema: "erp",
                table: "journal_entry_lines",
                column: "FinancialClassId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_AccountingTransactionId",
                schema: "erp",
                table: "journal_entries",
                column: "AccountingTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_CreatedByUserId",
                schema: "erp",
                table: "journal_entries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_PostedByUserId",
                schema: "erp",
                table: "journal_entries",
                column: "PostedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_classes_CompanyId_Code",
                schema: "erp",
                table: "financial_classes",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_journal_entries_users_CreatedByUserId",
                schema: "erp",
                table: "journal_entries",
                column: "CreatedByUserId",
                principalSchema: "erp",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_journal_entries_users_PostedByUserId",
                schema: "erp",
                table: "journal_entries",
                column: "PostedByUserId",
                principalSchema: "erp",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_journal_entry_lines_financial_classes_FinancialClassId",
                schema: "erp",
                table: "journal_entry_lines",
                column: "FinancialClassId",
                principalSchema: "erp",
                principalTable: "financial_classes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_journal_entries_users_CreatedByUserId",
                schema: "erp",
                table: "journal_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_journal_entries_users_PostedByUserId",
                schema: "erp",
                table: "journal_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_journal_entry_lines_financial_classes_FinancialClassId",
                schema: "erp",
                table: "journal_entry_lines");

            migrationBuilder.DropTable(
                name: "financial_classes",
                schema: "erp");

            migrationBuilder.DropIndex(
                name: "IX_journal_entry_lines_FinancialClassId",
                schema: "erp",
                table: "journal_entry_lines");

            migrationBuilder.DropIndex(
                name: "IX_journal_entries_AccountingTransactionId",
                schema: "erp",
                table: "journal_entries");

            migrationBuilder.DropIndex(
                name: "IX_journal_entries_CreatedByUserId",
                schema: "erp",
                table: "journal_entries");

            migrationBuilder.DropIndex(
                name: "IX_journal_entries_PostedByUserId",
                schema: "erp",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "FinancialClassId",
                schema: "erp",
                table: "journal_entry_lines");

            migrationBuilder.DropColumn(
                name: "Memo",
                schema: "erp",
                table: "journal_entry_lines");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "erp",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                schema: "erp",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "PostedAt",
                schema: "erp",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "PostedByUserId",
                schema: "erp",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "Reference",
                schema: "erp",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "AccountRole",
                schema: "erp",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "HierarchyLevel",
                schema: "erp",
                table: "accounts");

            migrationBuilder.DropSequence(
                name: "journal_entry_number_sequence",
                schema: "erp");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_AccountingTransactionId",
                schema: "erp",
                table: "journal_entries",
                column: "AccountingTransactionId");
        }
    }
}
