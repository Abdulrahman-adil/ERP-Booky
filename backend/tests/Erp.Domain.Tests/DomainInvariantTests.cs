using Erp.Domain.Accounting;
using Erp.Domain.Common;
using Erp.Domain.Inventory;
using Erp.Domain.Payments;
using Erp.Domain.Sales;
using Erp.Domain.Purchasing;
using Erp.Domain.Identity;

namespace Erp.Domain.Tests;

public sealed class DomainInvariantTests
{
    [Fact]
    public void ExternalIdentityRequiresAUserAndProviderSubject()
    {
        Assert.Throws<ArgumentException>(() => new ExternalIdentity(Guid.Empty, ExternalIdentityProvider.Google, "google-subject"));
        Assert.Throws<ArgumentException>(() => new ExternalIdentity(Guid.NewGuid(), ExternalIdentityProvider.Google, "   "));
    }

    [Fact]
    public void ChartOfAccountsRejectsDuplicateAccountCodes()
    {
        var chart = new ChartOfAccounts(Guid.NewGuid(), "Main chart");

        chart.AddAccount("1000", "Cash", AccountType.Asset, true);

        Assert.Throws<ArgumentException>(() => chart.AddAccount("1000", "Bank", AccountType.Asset, true));
    }

    [Fact]
    public void PostingAccountCannotBeUsedAsAnAccountHierarchyParent()
    {
        var chart = new ChartOfAccounts(Guid.NewGuid(), "Main chart");
        var postingAccount = chart.AddAccount("1000", "Cash", AccountType.Asset, AccountRole.Cash, true);

        Assert.Throws<ArgumentException>(() =>
            chart.AddAccount("1010", "Petty cash", AccountType.Asset, AccountRole.Cash, true, postingAccount.Id));
    }

    [Fact]
    public void AccountHierarchyRejectsDifferentChildAccountTypes()
    {
        var chart = new ChartOfAccounts(Guid.NewGuid(), "Main chart");
        var assetHeader = chart.AddAccount("1000", "Assets", AccountType.Asset, AccountRole.None, false);

        Assert.Throws<ArgumentException>(() =>
            chart.AddAccount("2000", "Liabilities", AccountType.Liability, AccountRole.None, false, assetHeader.Id));
    }

    [Fact]
    public void HeaderAccountsCannotUsePostingRoles()
    {
        var chart = new ChartOfAccounts(Guid.NewGuid(), "Main chart");

        Assert.Throws<ArgumentException>(() =>
            chart.AddAccount("1000", "Assets", AccountType.Asset, AccountRole.Bank, false));
    }

    [Fact]
    public void AccountUsesAnExplicitNormalBalanceOverrideForContraAccounts()
    {
        var chart = new ChartOfAccounts(Guid.NewGuid(), "Main chart");

        var account = chart.AddAccount("1290", "Accumulated depreciation", AccountType.Asset, AccountRole.None, true, normalBalanceOverride: AccountNormalBalance.Credit);

        Assert.Equal(AccountNormalBalance.Debit, account.DefaultNormalBalance);
        Assert.Equal(AccountNormalBalance.Credit, account.NormalBalance);
        Assert.True(account.IsContraAccount);
    }

    [Fact]
    public void JournalLineRequiresExactlyOneDebitOrCredit()
    {
        Assert.Throws<ArgumentException>(() => new JournalEntryLine(
            1,
            Guid.NewGuid(),
            new Money(10, "USD"),
            new Money(10, "USD")));
    }

    [Fact]
    public void JournalEntryMustBalanceBeforePosting()
    {
        var journalEntry = new JournalEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 20),
            "USD",
            "Test entry");

        journalEntry.AddLine(new JournalEntryLine(1, Guid.NewGuid(), new Money(100, "USD"), new Money(0, "USD")));
        journalEntry.AddLine(new JournalEntryLine(2, Guid.NewGuid(), new Money(0, "USD"), new Money(90, "USD")));

        Assert.False(journalEntry.IsBalanced);
        Assert.Throws<InvalidOperationException>(journalEntry.EnsureBalanced);
    }

    [Fact]
    public void PaymentCannotAllocateMoreThanItsAmount()
    {
        var payment = new Payment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            PaymentDirection.Incoming,
            new Money(100, "USD"),
            new DateOnly(2026, 8, 20));

        payment.Allocate(Guid.NewGuid(), new Money(75, "USD"));

        Assert.Throws<InvalidOperationException>(() => payment.Allocate(Guid.NewGuid(), new Money(26, "USD")));
    }

    [Fact]
    public void SalesInvoiceRejectsLinesInAnotherCurrency()
    {
        var invoice = new SalesInvoice(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DocumentNumber("SI-0001"),
            new DateOnly(2026, 8, 20),
            "USD");

        var line = new SalesInvoiceLine(
            1,
            "Consulting service",
            new Quantity(1, Guid.NewGuid()),
            new Money(100, "AED"),
            new TaxSnapshot(Guid.NewGuid(), 5));

        Assert.Throws<ArgumentException>(() => invoice.AddLine(line));
    }

    [Fact]
    public void SalesInvoiceCannotBeChangedAfterPosting()
    {
        var invoice = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), new DocumentNumber("SI-2026-000001"), new DateOnly(2026, 8, 26), "AED");
        invoice.AddLine(new SalesInvoiceLine(1, "Stock item", new Quantity(1, Guid.NewGuid()), new Money(100, "AED")));

        invoice.MarkPosted(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => invoice.UpdateDraft(Guid.NewGuid(), null, new DateOnly(2026, 8, 26), null, null, null));
    }

    [Fact]
    public void ReceiptInventoryMovementRequiresPositiveQuantity()
    {
        Assert.Throws<ArgumentException>(() => new InventoryTransaction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "INV-TEST",
            InventoryMovementType.Receipt,
            new Quantity(-1, Guid.NewGuid()),
            null,
            new SourceReference("Purchasing", Guid.NewGuid(), "PurchaseInvoicePosted"),
            DateTimeOffset.UtcNow,
            Guid.NewGuid()));
    }

    [Fact]
    public void InventoryBalanceRejectsMovementsThatWouldMakeStockNegative()
    {
        var unitOfMeasureId = Guid.NewGuid();
        var balance = new InventoryBalance(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new Quantity(5, unitOfMeasureId),
            DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            balance.ApplyMovement(new Quantity(-6, unitOfMeasureId), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void OutboundInventoryMovementRequiresANegativeQuantity()
    {
        Assert.Throws<ArgumentException>(() => new InventoryTransaction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "INV-TEST",
            InventoryMovementType.StockOut,
            new Quantity(1, Guid.NewGuid()),
            null,
            new SourceReference("Inventory", Guid.NewGuid(), "StockOut"),
            DateTimeOffset.UtcNow,
            Guid.NewGuid()));
    }

    [Fact]
    public void PurchaseBillCannotBeChangedAfterPosting()
    {
        var bill = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), new DocumentNumber("PI-2026-000001"), new DateOnly(2026, 8, 28), "AED");
        bill.ReplaceLines([new PurchaseInvoiceLine(1, "Stock item", new Quantity(2, Guid.NewGuid()), new Money(50, "AED"))]);

        bill.MarkPosted(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => bill.UpdateDraft(Guid.NewGuid(), null, new DateOnly(2026, 8, 28), null, null, null));
    }

    [Fact]
    public void RolePermissionReplacementRemovesOnlyThePreviousPermissionSet()
    {
        var role = new Role(Guid.NewGuid(), "Sales coordinator");
        var previousPermission = Guid.NewGuid();
        var replacementPermission = Guid.NewGuid();
        role.GrantPermission(previousPermission);

        role.ReplacePermissions([replacementPermission]);

        Assert.Equal([replacementPermission], role.Permissions.Select(permission => permission.PermissionId));
    }

    [Fact]
    public void UserRoleReplacementAffectsOnlyTheSelectedCompany()
    {
        var user = new User("Access user", "access@example.test");
        var firstCompany = Guid.NewGuid();
        var secondCompany = Guid.NewGuid();
        var preservedRole = Guid.NewGuid();
        var replacementRole = Guid.NewGuid();
        user.AssignRole(firstCompany, Guid.NewGuid());
        user.AssignRole(secondCompany, preservedRole);

        user.ReplaceRoles(firstCompany, [replacementRole]);

        Assert.Contains(user.RoleAssignments, assignment => assignment.CompanyId == firstCompany && assignment.RoleId == replacementRole);
        Assert.Contains(user.RoleAssignments, assignment => assignment.CompanyId == secondCompany && assignment.RoleId == preservedRole);
    }
}
