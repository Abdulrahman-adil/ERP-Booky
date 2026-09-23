using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "inventory_movement_number_sequence",
                schema: "erp");

            migrationBuilder.CreateSequence(
                name: "warehouse_code_sequence",
                schema: "erp");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "erp",
                table: "warehouses",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address_city",
                schema: "erp",
                table: "warehouses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address_country_code",
                schema: "erp",
                table: "warehouses",
                type: "character(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address_line_1",
                schema: "erp",
                table: "warehouses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address_line_2",
                schema: "erp",
                table: "warehouses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address_postal_code",
                schema: "erp",
                table: "warehouses",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "erp",
                table: "inventory_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                schema: "erp",
                table: "inventory_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalReference",
                schema: "erp",
                table: "inventory_transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MovementNumber",
                schema: "erp",
                table: "inventory_transactions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "erp",
                table: "inventory_transactions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "balance_after_quantity_amount",
                schema: "erp",
                table: "inventory_transactions",
                type: "numeric(19,6)",
                precision: 19,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "balance_after_quantity_unit_of_measure_id",
                schema: "erp",
                table: "inventory_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "inventory_value_currency_code",
                schema: "erp",
                table: "inventory_balances",
                type: "character(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character(3)",
                oldFixedLength: true,
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "inventory_value_amount",
                schema: "erp",
                table: "inventory_balances",
                type: "numeric(19,4)",
                precision: 19,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(19,4)",
                oldPrecision: 19,
                oldScale: 4);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_balance_after_quantity_unit_of_measu~",
                schema: "erp",
                table: "inventory_transactions",
                column: "balance_after_quantity_unit_of_measure_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_CompanyId_MovementNumber",
                schema: "erp",
                table: "inventory_transactions",
                columns: new[] { "CompanyId", "MovementNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_CompanyId_ProductId_WarehouseId",
                schema: "erp",
                table: "inventory_transactions",
                columns: new[] { "CompanyId", "ProductId", "WarehouseId" },
                unique: true,
                filter: "\"MovementType\" = 'OpeningBalance'");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_CreatedByUserId",
                schema: "erp",
                table: "inventory_transactions",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_transactions_units_of_measure_balance_after_quant~",
                schema: "erp",
                table: "inventory_transactions",
                column: "balance_after_quantity_unit_of_measure_id",
                principalSchema: "erp",
                principalTable: "units_of_measure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_transactions_users_CreatedByUserId",
                schema: "erp",
                table: "inventory_transactions",
                column: "CreatedByUserId",
                principalSchema: "erp",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_inventory_transactions_units_of_measure_balance_after_quant~",
                schema: "erp",
                table: "inventory_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_inventory_transactions_users_CreatedByUserId",
                schema: "erp",
                table: "inventory_transactions");

            migrationBuilder.DropIndex(
                name: "IX_inventory_transactions_balance_after_quantity_unit_of_measu~",
                schema: "erp",
                table: "inventory_transactions");

            migrationBuilder.DropIndex(
                name: "IX_inventory_transactions_CompanyId_MovementNumber",
                schema: "erp",
                table: "inventory_transactions");

            migrationBuilder.DropIndex(
                name: "IX_inventory_transactions_CompanyId_ProductId_WarehouseId",
                schema: "erp",
                table: "inventory_transactions");

            migrationBuilder.DropIndex(
                name: "IX_inventory_transactions_CreatedByUserId",
                schema: "erp",
                table: "inventory_transactions");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "erp",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "address_city",
                schema: "erp",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "address_country_code",
                schema: "erp",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "address_line_1",
                schema: "erp",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "address_line_2",
                schema: "erp",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "address_postal_code",
                schema: "erp",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "erp",
                table: "inventory_transactions");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                schema: "erp",
                table: "inventory_transactions");

            migrationBuilder.DropColumn(
                name: "ExternalReference",
                schema: "erp",
                table: "inventory_transactions");

            migrationBuilder.DropColumn(
                name: "MovementNumber",
                schema: "erp",
                table: "inventory_transactions");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "erp",
                table: "inventory_transactions");

            migrationBuilder.DropColumn(
                name: "balance_after_quantity_amount",
                schema: "erp",
                table: "inventory_transactions");

            migrationBuilder.DropColumn(
                name: "balance_after_quantity_unit_of_measure_id",
                schema: "erp",
                table: "inventory_transactions");

            migrationBuilder.DropSequence(
                name: "inventory_movement_number_sequence",
                schema: "erp");

            migrationBuilder.DropSequence(
                name: "warehouse_code_sequence",
                schema: "erp");

            migrationBuilder.AlterColumn<string>(
                name: "inventory_value_currency_code",
                schema: "erp",
                table: "inventory_balances",
                type: "character(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character(3)",
                oldFixedLength: true,
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "inventory_value_amount",
                schema: "erp",
                table: "inventory_balances",
                type: "numeric(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(19,4)",
                oldPrecision: 19,
                oldScale: 4,
                oldNullable: true);
        }
    }
}
