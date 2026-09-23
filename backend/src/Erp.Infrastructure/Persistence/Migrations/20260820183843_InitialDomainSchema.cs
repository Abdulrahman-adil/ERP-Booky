using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDomainSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "erp");

            migrationBuilder.CreateTable(
                name: "currencies",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DecimalPlaces = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "companies",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BaseCurrencyCode = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    TaxIdentifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_companies_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "erp",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "accounting_periods",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_periods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accounting_periods_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "accounting_transactions",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    source_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accounting_transactions_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "business_partners",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LegalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TaxIdentifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_line_1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address_line_2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_postal_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    address_country_code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    contact_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PaymentTermsDays = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_partners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_business_partners_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "charts_of_accounts",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_charts_of_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_charts_of_accounts_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "posting_profiles",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posting_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_posting_profiles_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_categories",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ParentCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_categories_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_categories_product_categories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalSchema: "erp",
                        principalTable: "product_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_roles_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tax_codes",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    IsRecoverable = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tax_codes_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "units_of_measure",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Dimension = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ConversionFactorToBase = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    DecimalPlaces = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units_of_measure", x => x.Id);
                    table.ForeignKey(
                        name: "FK_units_of_measure_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
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
                    table.PrimaryKey("PK_warehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_warehouses_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journal_entries",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountingTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountingPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    document_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_journal_entries_accounting_periods_AccountingPeriodId",
                        column: x => x.AccountingPeriodId,
                        principalSchema: "erp",
                        principalTable: "accounting_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entries_accounting_transactions_AccountingTransacti~",
                        column: x => x.AccountingTransactionId,
                        principalSchema: "erp",
                        principalTable: "accounting_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entries_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "open_items",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    document_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    original_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    original_currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    settled_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    settled_currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_open_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_open_items_business_partners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalSchema: "erp",
                        principalTable: "business_partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_open_items_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_invoices",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    document_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    SupplierReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_aggregate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_invoices_business_partners_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "erp",
                        principalTable: "business_partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_invoices_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoices",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    document_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    source_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_aggregate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_invoices_business_partners_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "erp",
                        principalTable: "business_partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_invoices_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "accounts",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChartOfAccountsId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    AccountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ParentAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsPosting = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accounts_accounts_ParentAccountId",
                        column: x => x.ParentAccountId,
                        principalSchema: "erp",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounts_charts_of_accounts_ChartOfAccountsId",
                        column: x => x.ChartOfAccountsId,
                        principalSchema: "erp",
                        principalTable: "charts_of_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "erp",
                        principalTable: "permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "erp",
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_role_assignments",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_role_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_role_assignments_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_role_assignments_roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "erp",
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_role_assignments_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "erp",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "products",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StockUnitOfMeasureId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostingProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_products_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_posting_profiles_PostingProfileId",
                        column: x => x.PostingProfileId,
                        principalSchema: "erp",
                        principalTable: "posting_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_product_categories_ProductCategoryId",
                        column: x => x.ProductCategoryId,
                        principalSchema: "erp",
                        principalTable: "product_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_units_of_measure_StockUnitOfMeasureId",
                        column: x => x.StockUnitOfMeasureId,
                        principalSchema: "erp",
                        principalTable: "units_of_measure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cash_bank_accounts",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    AccountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PostingAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_bank_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cash_bank_accounts_accounts_PostingAccountId",
                        column: x => x.PostingAccountId,
                        principalSchema: "erp",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_bank_accounts_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_profiles",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_limit_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    credit_limit_currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    ReceivableAccountId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_profiles_accounts_ReceivableAccountId",
                        column: x => x.ReceivableAccountId,
                        principalSchema: "erp",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customer_profiles_business_partners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalSchema: "erp",
                        principalTable: "business_partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journal_entry_lines",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    debit_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    debit_currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    credit_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    credit_currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    OpenItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entry_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_journal_entry_lines_accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "erp",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entry_lines_business_partners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalSchema: "erp",
                        principalTable: "business_partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entry_lines_journal_entries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalSchema: "erp",
                        principalTable: "journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entry_lines_open_items_OpenItemId",
                        column: x => x.OpenItemId,
                        principalSchema: "erp",
                        principalTable: "open_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "posting_profile_entries",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostingProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostingKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posting_profile_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_posting_profile_entries_accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "erp",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_posting_profile_entries_posting_profiles_PostingProfileId",
                        column: x => x.PostingProfileId,
                        principalSchema: "erp",
                        principalTable: "posting_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_profiles",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PayableAccountId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_profiles_accounts_PayableAccountId",
                        column: x => x.PayableAccountId,
                        principalSchema: "erp",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_profiles_business_partners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalSchema: "erp",
                        principalTable: "business_partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_balances",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    on_hand_quantity_amount = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    on_hand_quantity_unit_of_measure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_value_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    inventory_value_currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_balances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_balances_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_balances_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "erp",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_balances_units_of_measure_on_hand_quantity_unit_o~",
                        column: x => x.on_hand_quantity_unit_of_measure_id,
                        principalSchema: "erp",
                        principalTable: "units_of_measure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_balances_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "erp",
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_transactions",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovementType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity_amount = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    quantity_unit_of_measure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cost_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    cost_currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    source_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_transactions_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_transactions_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "erp",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_transactions_units_of_measure_quantity_unit_of_me~",
                        column: x => x.quantity_unit_of_measure_id,
                        principalSchema: "erp",
                        principalTable: "units_of_measure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_transactions_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "erp",
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_invoice_lines",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quantity_amount = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    quantity_unit_of_measure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_cost_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    unit_cost_currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    tax_tax_code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    PostingProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchaseInvoiceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_invoice_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_lines_posting_profiles_PostingProfileId",
                        column: x => x.PostingProfileId,
                        principalSchema: "erp",
                        principalTable: "posting_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_lines_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "erp",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_lines_purchase_invoices_PurchaseInvoiceId",
                        column: x => x.PurchaseInvoiceId,
                        principalSchema: "erp",
                        principalTable: "purchase_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_lines_tax_codes_tax_tax_code_id",
                        column: x => x.tax_tax_code_id,
                        principalSchema: "erp",
                        principalTable: "tax_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_lines_units_of_measure_quantity_unit_of_me~",
                        column: x => x.quantity_unit_of_measure_id,
                        principalSchema: "erp",
                        principalTable: "units_of_measure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoice_lines",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quantity_amount = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    quantity_unit_of_measure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_price_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    unit_price_currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    tax_tax_code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    DiscountPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    SalesInvoiceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoice_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_invoice_lines_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "erp",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_invoice_lines_sales_invoices_SalesInvoiceId",
                        column: x => x.SalesInvoiceId,
                        principalSchema: "erp",
                        principalTable: "sales_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_invoice_lines_tax_codes_tax_tax_code_id",
                        column: x => x.tax_tax_code_id,
                        principalSchema: "erp",
                        principalTable: "tax_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_invoice_lines_units_of_measure_quantity_unit_of_measu~",
                        column: x => x.quantity_unit_of_measure_id,
                        principalSchema: "erp",
                        principalTable: "units_of_measure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashBankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payment_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    payment_currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payments_business_partners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalSchema: "erp",
                        principalTable: "business_partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_cash_bank_accounts_CashBankAccountId",
                        column: x => x.CashBankAccountId,
                        principalSchema: "erp",
                        principalTable: "cash_bank_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "general_ledger_entries",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountingPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalEntryLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    debit_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    debit_currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    credit_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    credit_currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    PostedDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_general_ledger_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_general_ledger_entries_accounting_periods_AccountingPeriodId",
                        column: x => x.AccountingPeriodId,
                        principalSchema: "erp",
                        principalTable: "accounting_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_general_ledger_entries_accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "erp",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_general_ledger_entries_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "erp",
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_general_ledger_entries_journal_entries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalSchema: "erp",
                        principalTable: "journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_general_ledger_entries_journal_entry_lines_JournalEntryLine~",
                        column: x => x.JournalEntryLineId,
                        principalSchema: "erp",
                        principalTable: "journal_entry_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_allocations",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    allocated_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    allocated_currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_allocations_open_items_OpenItemId",
                        column: x => x.OpenItemId,
                        principalSchema: "erp",
                        principalTable: "open_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_allocations_payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "erp",
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounting_periods_CompanyId_Name",
                schema: "erp",
                table: "accounting_periods",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounting_transactions_CompanyId",
                schema: "erp",
                table: "accounting_transactions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_transactions_source_module_source_aggregate_id_s~",
                schema: "erp",
                table: "accounting_transactions",
                columns: new[] { "source_module", "source_aggregate_id", "source_event_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_ChartOfAccountsId_Code",
                schema: "erp",
                table: "accounts",
                columns: new[] { "ChartOfAccountsId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_ParentAccountId",
                schema: "erp",
                table: "accounts",
                column: "ParentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_business_partners_CompanyId_Code",
                schema: "erp",
                table: "business_partners",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_bank_accounts_CompanyId_Name",
                schema: "erp",
                table: "cash_bank_accounts",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_bank_accounts_CompanyId_PostingAccountId",
                schema: "erp",
                table: "cash_bank_accounts",
                columns: new[] { "CompanyId", "PostingAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_bank_accounts_PostingAccountId",
                schema: "erp",
                table: "cash_bank_accounts",
                column: "PostingAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_charts_of_accounts_CompanyId",
                schema: "erp",
                table: "charts_of_accounts",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_companies_OrganizationId_Code",
                schema: "erp",
                table: "companies",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_currencies_Code",
                schema: "erp",
                table: "currencies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_profiles_BusinessPartnerId",
                schema: "erp",
                table: "customer_profiles",
                column: "BusinessPartnerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_profiles_ReceivableAccountId",
                schema: "erp",
                table: "customer_profiles",
                column: "ReceivableAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_general_ledger_entries_AccountId",
                schema: "erp",
                table: "general_ledger_entries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_general_ledger_entries_AccountingPeriodId",
                schema: "erp",
                table: "general_ledger_entries",
                column: "AccountingPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_general_ledger_entries_CompanyId_AccountId_AccountingPeriod~",
                schema: "erp",
                table: "general_ledger_entries",
                columns: new[] { "CompanyId", "AccountId", "AccountingPeriodId", "PostedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_general_ledger_entries_JournalEntryId",
                schema: "erp",
                table: "general_ledger_entries",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_general_ledger_entries_JournalEntryLineId",
                schema: "erp",
                table: "general_ledger_entries",
                column: "JournalEntryLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_balances_CompanyId_ProductId_WarehouseId",
                schema: "erp",
                table: "inventory_balances",
                columns: new[] { "CompanyId", "ProductId", "WarehouseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_balances_on_hand_quantity_unit_of_measure_id",
                schema: "erp",
                table: "inventory_balances",
                column: "on_hand_quantity_unit_of_measure_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_balances_ProductId",
                schema: "erp",
                table: "inventory_balances",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_balances_WarehouseId",
                schema: "erp",
                table: "inventory_balances",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_CompanyId_ProductId_WarehouseId_Occu~",
                schema: "erp",
                table: "inventory_transactions",
                columns: new[] { "CompanyId", "ProductId", "WarehouseId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_ProductId",
                schema: "erp",
                table: "inventory_transactions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_quantity_unit_of_measure_id",
                schema: "erp",
                table: "inventory_transactions",
                column: "quantity_unit_of_measure_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_source_module_source_aggregate_id_so~",
                schema: "erp",
                table: "inventory_transactions",
                columns: new[] { "source_module", "source_aggregate_id", "source_event_type" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_WarehouseId",
                schema: "erp",
                table: "inventory_transactions",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_AccountingPeriodId",
                schema: "erp",
                table: "journal_entries",
                column: "AccountingPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_AccountingTransactionId",
                schema: "erp",
                table: "journal_entries",
                column: "AccountingTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_CompanyId_AccountingPeriodId_EntryDate",
                schema: "erp",
                table: "journal_entries",
                columns: new[] { "CompanyId", "AccountingPeriodId", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entry_lines_AccountId",
                schema: "erp",
                table: "journal_entry_lines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entry_lines_BusinessPartnerId",
                schema: "erp",
                table: "journal_entry_lines",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entry_lines_JournalEntryId_LineNumber",
                schema: "erp",
                table: "journal_entry_lines",
                columns: new[] { "JournalEntryId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_entry_lines_OpenItemId",
                schema: "erp",
                table: "journal_entry_lines",
                column: "OpenItemId");

            migrationBuilder.CreateIndex(
                name: "IX_open_items_BusinessPartnerId",
                schema: "erp",
                table: "open_items",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_open_items_CompanyId_BusinessPartnerId_Type",
                schema: "erp",
                table: "open_items",
                columns: new[] { "CompanyId", "BusinessPartnerId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_open_items_source_module_source_aggregate_id_source_event_t~",
                schema: "erp",
                table: "open_items",
                columns: new[] { "source_module", "source_aggregate_id", "source_event_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organizations_Code",
                schema: "erp",
                table: "organizations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_OpenItemId",
                schema: "erp",
                table: "payment_allocations",
                column: "OpenItemId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_PaymentId_OpenItemId",
                schema: "erp",
                table: "payment_allocations",
                columns: new[] { "PaymentId", "OpenItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_BusinessPartnerId",
                schema: "erp",
                table: "payments",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_CashBankAccountId",
                schema: "erp",
                table: "payments",
                column: "CashBankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_CompanyId_BusinessPartnerId_PaymentDate",
                schema: "erp",
                table: "payments",
                columns: new[] { "CompanyId", "BusinessPartnerId", "PaymentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_permissions_Key",
                schema: "erp",
                table: "permissions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_posting_profile_entries_AccountId",
                schema: "erp",
                table: "posting_profile_entries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_posting_profile_entries_PostingProfileId_PostingKey",
                schema: "erp",
                table: "posting_profile_entries",
                columns: new[] { "PostingProfileId", "PostingKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_posting_profiles_CompanyId_Name",
                schema: "erp",
                table: "posting_profiles",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_categories_CompanyId_Code",
                schema: "erp",
                table: "product_categories",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_categories_ParentCategoryId",
                schema: "erp",
                table: "product_categories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_products_CompanyId_Sku",
                schema: "erp",
                table: "products",
                columns: new[] { "CompanyId", "Sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_PostingProfileId",
                schema: "erp",
                table: "products",
                column: "PostingProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_products_ProductCategoryId",
                schema: "erp",
                table: "products",
                column: "ProductCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_products_StockUnitOfMeasureId",
                schema: "erp",
                table: "products",
                column: "StockUnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_lines_PostingProfileId",
                schema: "erp",
                table: "purchase_invoice_lines",
                column: "PostingProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_lines_ProductId",
                schema: "erp",
                table: "purchase_invoice_lines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_lines_PurchaseInvoiceId_LineNumber",
                schema: "erp",
                table: "purchase_invoice_lines",
                columns: new[] { "PurchaseInvoiceId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_lines_quantity_unit_of_measure_id",
                schema: "erp",
                table: "purchase_invoice_lines",
                column: "quantity_unit_of_measure_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_lines_tax_tax_code_id",
                schema: "erp",
                table: "purchase_invoice_lines",
                column: "tax_tax_code_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_CompanyId_document_number",
                schema: "erp",
                table: "purchase_invoices",
                columns: new[] { "CompanyId", "document_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_CompanyId_SupplierId_InvoiceDate",
                schema: "erp",
                table: "purchase_invoices",
                columns: new[] { "CompanyId", "SupplierId", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_source_module_source_aggregate_id_source_~",
                schema: "erp",
                table: "purchase_invoices",
                columns: new[] { "source_module", "source_aggregate_id", "source_event_type" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoices_SupplierId",
                schema: "erp",
                table: "purchase_invoices",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_PermissionId",
                schema: "erp",
                table: "role_permissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_RoleId_PermissionId",
                schema: "erp",
                table: "role_permissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_CompanyId_Name",
                schema: "erp",
                table: "roles",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoice_lines_ProductId",
                schema: "erp",
                table: "sales_invoice_lines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoice_lines_quantity_unit_of_measure_id",
                schema: "erp",
                table: "sales_invoice_lines",
                column: "quantity_unit_of_measure_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoice_lines_SalesInvoiceId_LineNumber",
                schema: "erp",
                table: "sales_invoice_lines",
                columns: new[] { "SalesInvoiceId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoice_lines_tax_tax_code_id",
                schema: "erp",
                table: "sales_invoice_lines",
                column: "tax_tax_code_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_CompanyId_CustomerId_InvoiceDate",
                schema: "erp",
                table: "sales_invoices",
                columns: new[] { "CompanyId", "CustomerId", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_CompanyId_document_number",
                schema: "erp",
                table: "sales_invoices",
                columns: new[] { "CompanyId", "document_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_CustomerId",
                schema: "erp",
                table: "sales_invoices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_source_module_source_aggregate_id_source_eve~",
                schema: "erp",
                table: "sales_invoices",
                columns: new[] { "source_module", "source_aggregate_id", "source_event_type" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_profiles_BusinessPartnerId",
                schema: "erp",
                table: "supplier_profiles",
                column: "BusinessPartnerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_profiles_PayableAccountId",
                schema: "erp",
                table: "supplier_profiles",
                column: "PayableAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_tax_codes_CompanyId_Code",
                schema: "erp",
                table: "tax_codes",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_units_of_measure_CompanyId_Code",
                schema: "erp",
                table: "units_of_measure",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_role_assignments_CompanyId",
                schema: "erp",
                table: "user_role_assignments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_user_role_assignments_RoleId",
                schema: "erp",
                table: "user_role_assignments",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_user_role_assignments_UserId_CompanyId_RoleId",
                schema: "erp",
                table: "user_role_assignments",
                columns: new[] { "UserId", "CompanyId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                schema: "erp",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_CompanyId_Code",
                schema: "erp",
                table: "warehouses",
                columns: new[] { "CompanyId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "currencies",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "customer_profiles",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "general_ledger_entries",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "inventory_balances",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "inventory_transactions",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "payment_allocations",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "posting_profile_entries",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "purchase_invoice_lines",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "sales_invoice_lines",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "supplier_profiles",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "user_role_assignments",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "journal_entry_lines",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "warehouses",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "purchase_invoices",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "permissions",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "products",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "sales_invoices",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "tax_codes",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "users",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "journal_entries",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "open_items",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "cash_bank_accounts",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "posting_profiles",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "product_categories",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "units_of_measure",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "accounting_periods",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "accounting_transactions",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "business_partners",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "accounts",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "charts_of_accounts",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "companies",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "organizations",
                schema: "erp");
        }
    }
}
