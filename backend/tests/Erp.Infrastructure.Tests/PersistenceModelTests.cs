using Erp.Domain.Accounting;
using Erp.Domain.Common;
using Erp.Domain.Inventory;
using Erp.Domain.Identity;
using Erp.Domain.MasterData;
using Erp.Domain.Organizations;
using Erp.Domain.Purchasing;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Tests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void Model_contains_the_organization_and_company_mapping()
    {
        using var dbContext = CreateDbContext();

        var companyEntityType = dbContext.Model.FindEntityType(typeof(Company));
        var organizationForeignKey = Assert.Single(companyEntityType!.GetForeignKeys());

        Assert.Equal("companies", companyEntityType.GetTableName());
        Assert.Equal(nameof(Company.OrganizationId), Assert.Single(organizationForeignKey.Properties).Name);
        Assert.True(organizationForeignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, organizationForeignKey.DeleteBehavior);
    }

    [Fact]
    public void External_identities_are_unique_per_provider_subject_and_restrict_user_deletion()
    {
        using var dbContext = CreateDbContext();

        var externalIdentityType = dbContext.Model.FindEntityType(typeof(ExternalIdentity));
        var providerSubjectIndex = Assert.Single(externalIdentityType!.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(ExternalIdentity.Provider), nameof(ExternalIdentity.ProviderSubject)]));
        var userProviderIndex = Assert.Single(externalIdentityType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(ExternalIdentity.UserId), nameof(ExternalIdentity.Provider)]));
        var userForeignKey = Assert.Single(externalIdentityType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(User));

        Assert.Equal("external_identities", externalIdentityType.GetTableName());
        Assert.True(providerSubjectIndex.IsUnique);
        Assert.True(userProviderIndex.IsUnique);
        Assert.Equal(DeleteBehavior.Restrict, userForeignKey.DeleteBehavior);
    }

    [Fact]
    public void Inventory_balance_has_a_unique_company_product_warehouse_constraint()
    {
        using var dbContext = CreateDbContext();

        var inventoryBalanceEntityType = dbContext.Model.FindEntityType(typeof(InventoryBalance));
        var index = Assert.Single(inventoryBalanceEntityType!.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(InventoryBalance.CompanyId), nameof(InventoryBalance.ProductId), nameof(InventoryBalance.WarehouseId)]));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void Inventory_movement_mapping_preserves_history_and_opening_stock_constraint()
    {
        using var dbContext = CreateDbContext();

        var transactionType = dbContext.Model.FindEntityType(typeof(InventoryTransaction));
        var userForeignKey = Assert.Single(transactionType!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType.Name == "User");
        var openingStockIndex = Assert.Single(transactionType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(InventoryTransaction.CompanyId), nameof(InventoryTransaction.ProductId), nameof(InventoryTransaction.WarehouseId)]));

        Assert.Equal(DeleteBehavior.Restrict, userForeignKey.DeleteBehavior);
        Assert.True(openingStockIndex.IsUnique);
        Assert.Equal("\"MovementType\" = 'OpeningBalance'", openingStockIndex.GetFilter());
        Assert.Equal(40, transactionType.FindProperty(nameof(InventoryTransaction.MovementType))!.GetMaxLength());
    }

    [Fact]
    public void Journal_entry_lines_require_a_restricted_link_to_their_header()
    {
        using var dbContext = CreateDbContext();

        var journalEntryLineEntityType = dbContext.Model.FindEntityType(typeof(JournalEntryLine));
        var journalEntryForeignKey = Assert.Single(journalEntryLineEntityType!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(JournalEntry));

        Assert.Equal("JournalEntryId", Assert.Single(journalEntryForeignKey.Properties).Name);
        Assert.True(journalEntryForeignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, journalEntryForeignKey.DeleteBehavior);
    }

    [Fact]
    public void Accounting_model_preserves_posting_and_financial_dimension_constraints()
    {
        using var dbContext = CreateDbContext();

        var accountType = dbContext.Model.FindEntityType(typeof(Account));
        var accountingSourceType = dbContext.Model.GetEntityTypes().Single(entityType =>
            entityType.IsOwned()
            && entityType.ClrType == typeof(SourceReference)
            && entityType.FindOwnership()?.PrincipalEntityType.ClrType == typeof(AccountingTransaction));
        var sourceIndex = Assert.Single(accountingSourceType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(["Module", "AggregateId", "EventType"]));
        var journalType = dbContext.Model.FindEntityType(typeof(JournalEntry));
        var journalTransactionIndex = Assert.Single(journalType!.GetIndexes(), index =>
            index.Properties.Count == 1 && index.Properties[0].Name == nameof(JournalEntry.AccountingTransactionId));
        var classForeignKey = Assert.Single(dbContext.Model.FindEntityType(typeof(JournalEntryLine))!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(FinancialClass));
        var customerAccountForeignKey = Assert.Single(dbContext.Model.FindEntityType(typeof(CustomerProfile))!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Account));
        var supplierAccountForeignKey = Assert.Single(dbContext.Model.FindEntityType(typeof(SupplierProfile))!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Account));

        Assert.Equal(typeof(AccountRole), accountType!.FindProperty(nameof(Account.AccountRole))!.ClrType);
        Assert.True(sourceIndex.IsUnique);
        Assert.True(journalTransactionIndex.IsUnique);
        Assert.Equal(DeleteBehavior.Restrict, classForeignKey.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, customerAccountForeignKey.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, supplierAccountForeignKey.DeleteBehavior);
    }

    [Fact]
    public void Purchase_bill_mapping_preserves_restricted_history_links()
    {
        using var dbContext = CreateDbContext();

        var billType = dbContext.Model.FindEntityType(typeof(PurchaseInvoice));
        var warehouseForeignKey = Assert.Single(billType!.GetForeignKeys(), foreignKey => foreignKey.Properties.Single().Name == nameof(PurchaseInvoice.WarehouseId));
        var documentNumberIndex = Assert.Single(billType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(PurchaseInvoice.CompanyId), nameof(PurchaseInvoice.DocumentNumber)]));

        Assert.Equal(DeleteBehavior.Restrict, warehouseForeignKey.DeleteBehavior);
        Assert.True(documentNumberIndex.IsUnique);
    }

    private static ErpDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql("Host=localhost;Database=erp_model;Username=erp")
            .Options;

        return new ErpDbContext(options);
    }
}
