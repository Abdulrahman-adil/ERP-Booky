using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManufacturing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "bill_of_material_code_sequence",
                schema: "erp");

            migrationBuilder.CreateSequence(
                name: "production_order_number_sequence",
                schema: "erp");

            migrationBuilder.AddColumn<string>(
                name: "InventoryPurpose",
                schema: "erp",
                table: "products",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bills_of_materials",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FinishedProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutputQuantity = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    OutputUnitOfMeasureId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bills_of_materials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bills_of_materials_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bills_of_materials_products_FinishedProductId",
                        column: x => x.FinishedProductId,
                        principalSchema: "erp",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bills_of_materials_units_of_measure_OutputUnitOfMeasureId",
                        column: x => x.OutputUnitOfMeasureId,
                        principalSchema: "erp",
                        principalTable: "units_of_measure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bills_of_materials_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "erp",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bill_of_material_components",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ComponentProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillOfMaterialsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bill_of_material_components", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bill_of_material_components_bills_of_materials_BillOfMateri~",
                        column: x => x.BillOfMaterialsId,
                        principalSchema: "erp",
                        principalTable: "bills_of_materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bill_of_material_components_products_ComponentProductId",
                        column: x => x.ComponentProductId,
                        principalSchema: "erp",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bill_of_material_components_units_of_measure_UnitOfMeasureId",
                        column: x => x.UnitOfMeasureId,
                        principalSchema: "erp",
                        principalTable: "units_of_measure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_orders",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionOrderNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FinishedProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillOfMaterialsId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedQuantity = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    ActualProducedQuantity = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: true),
                    OutputUnitOfMeasureId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleasedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_production_orders_bills_of_materials_BillOfMaterialsId",
                        column: x => x.BillOfMaterialsId,
                        principalSchema: "erp",
                        principalTable: "bills_of_materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_orders_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_orders_products_FinishedProductId",
                        column: x => x.FinishedProductId,
                        principalSchema: "erp",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_orders_units_of_measure_OutputUnitOfMeasureId",
                        column: x => x.OutputUnitOfMeasureId,
                        principalSchema: "erp",
                        principalTable: "units_of_measure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_orders_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "erp",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_orders_users_PostedByUserId",
                        column: x => x.PostedByUserId,
                        principalSchema: "erp",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_orders_users_ReleasedByUserId",
                        column: x => x.ReleasedByUserId,
                        principalSchema: "erp",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_orders_warehouses_DestinationWarehouseId",
                        column: x => x.DestinationWarehouseId,
                        principalSchema: "erp",
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_orders_warehouses_SourceWarehouseId",
                        column: x => x.SourceWarehouseId,
                        principalSchema: "erp",
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_order_materials",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ComponentProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityPerBomOutput = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    BomOutputQuantity = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionOrderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_order_materials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_production_order_materials_production_orders_ProductionOrde~",
                        column: x => x.ProductionOrderId,
                        principalSchema: "erp",
                        principalTable: "production_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_order_materials_products_ComponentProductId",
                        column: x => x.ComponentProductId,
                        principalSchema: "erp",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_order_materials_units_of_measure_UnitOfMeasureId",
                        column: x => x.UnitOfMeasureId,
                        principalSchema: "erp",
                        principalTable: "units_of_measure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bill_of_material_components_BillOfMaterialsId_ComponentProd~",
                schema: "erp",
                table: "bill_of_material_components",
                columns: new[] { "BillOfMaterialsId", "ComponentProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bill_of_material_components_BillOfMaterialsId_LineNumber",
                schema: "erp",
                table: "bill_of_material_components",
                columns: new[] { "BillOfMaterialsId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bill_of_material_components_ComponentProductId",
                schema: "erp",
                table: "bill_of_material_components",
                column: "ComponentProductId");

            migrationBuilder.CreateIndex(
                name: "IX_bill_of_material_components_UnitOfMeasureId",
                schema: "erp",
                table: "bill_of_material_components",
                column: "UnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_bills_of_materials_CompanyId_Code",
                schema: "erp",
                table: "bills_of_materials",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bills_of_materials_CompanyId_FinishedProductId_IsActive",
                schema: "erp",
                table: "bills_of_materials",
                columns: new[] { "CompanyId", "FinishedProductId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_bills_of_materials_CreatedByUserId",
                schema: "erp",
                table: "bills_of_materials",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_bills_of_materials_FinishedProductId",
                schema: "erp",
                table: "bills_of_materials",
                column: "FinishedProductId");

            migrationBuilder.CreateIndex(
                name: "IX_bills_of_materials_OutputUnitOfMeasureId",
                schema: "erp",
                table: "bills_of_materials",
                column: "OutputUnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_production_order_materials_ComponentProductId",
                schema: "erp",
                table: "production_order_materials",
                column: "ComponentProductId");

            migrationBuilder.CreateIndex(
                name: "IX_production_order_materials_ProductionOrderId_ComponentProdu~",
                schema: "erp",
                table: "production_order_materials",
                columns: new[] { "ProductionOrderId", "ComponentProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_production_order_materials_ProductionOrderId_LineNumber",
                schema: "erp",
                table: "production_order_materials",
                columns: new[] { "ProductionOrderId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_production_order_materials_UnitOfMeasureId",
                schema: "erp",
                table: "production_order_materials",
                column: "UnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_BillOfMaterialsId",
                schema: "erp",
                table: "production_orders",
                column: "BillOfMaterialsId");

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_CompanyId_ProductionOrderNumber",
                schema: "erp",
                table: "production_orders",
                columns: new[] { "CompanyId", "ProductionOrderNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_CompanyId_Status_ProductionDate",
                schema: "erp",
                table: "production_orders",
                columns: new[] { "CompanyId", "Status", "ProductionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_CreatedByUserId",
                schema: "erp",
                table: "production_orders",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_DestinationWarehouseId",
                schema: "erp",
                table: "production_orders",
                column: "DestinationWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_FinishedProductId",
                schema: "erp",
                table: "production_orders",
                column: "FinishedProductId");

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_OutputUnitOfMeasureId",
                schema: "erp",
                table: "production_orders",
                column: "OutputUnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_PostedByUserId",
                schema: "erp",
                table: "production_orders",
                column: "PostedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_ReleasedByUserId",
                schema: "erp",
                table: "production_orders",
                column: "ReleasedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_SourceWarehouseId",
                schema: "erp",
                table: "production_orders",
                column: "SourceWarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bill_of_material_components",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "production_order_materials",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "production_orders",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "bills_of_materials",
                schema: "erp");

            migrationBuilder.DropColumn(
                name: "InventoryPurpose",
                schema: "erp",
                table: "products");

            migrationBuilder.DropSequence(
                name: "bill_of_material_code_sequence",
                schema: "erp");

            migrationBuilder.DropSequence(
                name: "production_order_number_sequence",
                schema: "erp");
        }
    }
}
