using System;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ErpDbContext))]
[Migration("20260828164500_MakeLegacyPurchaseTaxColumnsOptional")]
public partial class MakeLegacyPurchaseTaxColumnsOptional : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<decimal>(
            name: "tax_rate",
            schema: "erp",
            table: "purchase_invoice_lines",
            type: "numeric(5,2)",
            precision: 5,
            scale: 2,
            nullable: true,
            oldClrType: typeof(decimal),
            oldType: "numeric(5,2)",
            oldPrecision: 5,
            oldScale: 2);

        migrationBuilder.AlterColumn<Guid>(
            name: "tax_tax_code_id",
            schema: "erp",
            table: "purchase_invoice_lines",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<decimal>(
            name: "tax_rate",
            schema: "erp",
            table: "purchase_invoice_lines",
            type: "numeric(5,2)",
            precision: 5,
            scale: 2,
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "numeric(5,2)",
            oldPrecision: 5,
            oldScale: 2,
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "tax_tax_code_id",
            schema: "erp",
            table: "purchase_invoice_lines",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);
    }
}
