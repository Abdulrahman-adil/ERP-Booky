using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterDataNumberingAndOptionalOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "customer_code_sequence",
                schema: "erp");

            migrationBuilder.CreateSequence(
                name: "product_sku_sequence",
                schema: "erp");

            migrationBuilder.CreateSequence(
                name: "supplier_code_sequence",
                schema: "erp");

            migrationBuilder.AlterColumn<Guid>(
                name: "StockUnitOfMeasureId",
                schema: "erp",
                table: "products",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "address_line_1",
                schema: "erp",
                table: "business_partners",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "address_country_code",
                schema: "erp",
                table: "business_partners",
                type: "character(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character(2)",
                oldFixedLength: true,
                oldMaxLength: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "customer_code_sequence",
                schema: "erp");

            migrationBuilder.DropSequence(
                name: "product_sku_sequence",
                schema: "erp");

            migrationBuilder.DropSequence(
                name: "supplier_code_sequence",
                schema: "erp");

            migrationBuilder.AlterColumn<Guid>(
                name: "StockUnitOfMeasureId",
                schema: "erp",
                table: "products",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "address_line_1",
                schema: "erp",
                table: "business_partners",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "address_country_code",
                schema: "erp",
                table: "business_partners",
                type: "character(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character(2)",
                oldFixedLength: true,
                oldMaxLength: 2,
                oldNullable: true);
        }
    }
}
