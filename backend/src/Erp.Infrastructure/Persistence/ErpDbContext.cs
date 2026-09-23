using Erp.Domain.Accounting;
using Erp.Domain.Identity;
using Erp.Domain.Inventory;
using Erp.Domain.Documents;
using Erp.Domain.MasterData;
using Erp.Domain.Manufacturing;
using Erp.Domain.Organizations;
using Erp.Domain.Payments;
using Erp.Domain.Purchasing;
using Erp.Domain.Sales;
using Erp.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Persistence;

public sealed class ErpDbContext(DbContextOptions<ErpDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();

    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();

    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();

    public DbSet<BusinessPartner> BusinessPartners => Set<BusinessPartner>();

    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();

    public DbSet<SupplierProfile> SupplierProfiles => Set<SupplierProfile>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();

    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();

    public DbSet<TaxCode> TaxCodes => Set<TaxCode>();

    public DbSet<Currency> Currencies => Set<Currency>();

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    public DbSet<InventoryBalance> InventoryBalances => Set<InventoryBalance>();

    public DbSet<BillOfMaterials> BillsOfMaterials => Set<BillOfMaterials>();

    public DbSet<BillOfMaterialsComponent> BillOfMaterialsComponents => Set<BillOfMaterialsComponent>();

    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();

    public DbSet<ProductionOrderMaterial> ProductionOrderMaterials => Set<ProductionOrderMaterial>();

    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();

    public DbSet<SalesInvoiceLine> SalesInvoiceLines => Set<SalesInvoiceLine>();

    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();

    public DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines => Set<PurchaseInvoiceLine>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();

    public DbSet<DocumentAttachment> DocumentAttachments => Set<DocumentAttachment>();

    public DbSet<ChartOfAccounts> ChartsOfAccounts => Set<ChartOfAccounts>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<FinancialClass> FinancialClasses => Set<FinancialClass>();

    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();

    public DbSet<CashBankAccount> CashBankAccounts => Set<CashBankAccount>();

    public DbSet<PostingProfile> PostingProfiles => Set<PostingProfile>();

    public DbSet<PostingProfileEntry> PostingProfileEntries => Set<PostingProfileEntry>();

    public DbSet<AccountingTransaction> AccountingTransactions => Set<AccountingTransaction>();

    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();

    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();

    public DbSet<OpenItem> OpenItems => Set<OpenItem>();

    public DbSet<GeneralLedgerEntry> GeneralLedgerEntries => Set<GeneralLedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("erp");
        modelBuilder.HasSequence<long>("customer_code_sequence");
        modelBuilder.HasSequence<long>("supplier_code_sequence");
        modelBuilder.HasSequence<long>("product_sku_sequence");
        modelBuilder.HasSequence<long>("warehouse_code_sequence");
        modelBuilder.HasSequence<long>("inventory_movement_number_sequence");
        modelBuilder.HasSequence<long>("bill_of_material_code_sequence");
        modelBuilder.HasSequence<long>("production_order_number_sequence");
        modelBuilder.HasSequence<long>("sales_invoice_number_sequence");
        modelBuilder.HasSequence<long>("customer_payment_number_sequence");
        modelBuilder.HasSequence<long>("purchase_invoice_number_sequence");
        modelBuilder.HasSequence<long>("supplier_payment_number_sequence");
        modelBuilder.HasSequence<long>("journal_entry_number_sequence");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ErpDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
