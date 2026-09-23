using Erp.Domain.Common;

namespace Erp.Domain.Accounting;

#pragma warning disable CS8618

public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Revenue,
    Expense
}

public enum AccountRole
{
    None,
    Cash,
    Bank,
    AccountsReceivable,
    AccountsPayable,
    Inventory,
    SalesRevenue,
    OtherIncome,
    CostOfGoodsSold,
    Expense,
    OtherExpense,
    Equity,
    RetainedEarnings,
    CurrentYearEarnings
}

public enum AccountNormalBalance
{
    Debit,
    Credit
}

public sealed class ChartOfAccounts : AggregateRoot
{
    private readonly List<Account> _accounts = [];

    private ChartOfAccounts()
    {
    }

    public ChartOfAccounts(Guid companyId, string name, Guid id = default)
        : base(id)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("A company is required.", nameof(companyId));

        CompanyId = companyId;
        Name = Money.RequireText(name, nameof(name));
    }

    public Guid CompanyId { get; private set; }

    public string Name { get; private set; }

    public IReadOnlyCollection<Account> Accounts => _accounts.AsReadOnly();

    public Account AddAccount(
        string code,
        string name,
        AccountType accountType,
        bool isPosting,
        Guid? parentAccountId = null,
        Guid id = default) =>
        AddAccount(code, name, accountType, AccountRole.None, isPosting, parentAccountId, id);

    public Account AddAccount(
        string code,
        string name,
        AccountType accountType,
        AccountRole accountRole,
        bool isPosting,
        Guid? parentAccountId = null,
        Guid id = default,
        AccountNormalBalance? normalBalanceOverride = null)
    {
        var normalizedCode = Money.RequireText(code, nameof(code)).ToUpperInvariant();
        if (_accounts.Any(account => string.Equals(account.Code, normalizedCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Account codes must be unique within a chart of accounts.", nameof(code));
        }

        var parent = parentAccountId is null ? null : _accounts.SingleOrDefault(account => account.Id == parentAccountId);
        if (parentAccountId is not null && parent is null)
        {
            throw new ArgumentException("The parent account must belong to this chart of accounts.", nameof(parentAccountId));
        }

        if (parent is not null && parent.IsPosting)
        {
            throw new ArgumentException("Posting accounts cannot contain child accounts.", nameof(parentAccountId));
        }

        if (parent is not null && parent.AccountType != accountType)
        {
            throw new ArgumentException("Child accounts must use the same account type as their parent.", nameof(accountType));
        }

        var account = new Account(Id, normalizedCode, name, accountType, accountRole, isPosting, parentAccountId, parent is null ? 0 : parent.HierarchyLevel + 1, id, normalBalanceOverride);
        _accounts.Add(account);
        return account;
    }
}

public sealed class Account : Entity
{
    private Account()
    {
    }

    internal Account(
        Guid chartOfAccountsId,
        string code,
        string name,
        AccountType accountType,
        AccountRole accountRole,
        bool isPosting,
        Guid? parentAccountId,
        int hierarchyLevel,
        Guid id = default,
        AccountNormalBalance? normalBalanceOverride = null)
        : base(id)
    {
        if (hierarchyLevel < 0) throw new ArgumentOutOfRangeException(nameof(hierarchyLevel));
        EnsureValidConfiguration(accountType, accountRole, isPosting);

        ChartOfAccountsId = chartOfAccountsId;
        Code = Money.RequireText(code, nameof(code)).ToUpperInvariant();
        Name = Money.RequireText(name, nameof(name));
        AccountType = accountType;
        AccountRole = accountRole;
        IsPosting = isPosting;
        ParentAccountId = parentAccountId;
        HierarchyLevel = hierarchyLevel;
        NormalBalanceOverride = normalBalanceOverride;
        IsActive = true;
    }

    public Guid ChartOfAccountsId { get; private set; }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public AccountType AccountType { get; private set; }

    public AccountRole AccountRole { get; private set; }

    public Guid? ParentAccountId { get; private set; }

    public int HierarchyLevel { get; private set; }

    public bool IsPosting { get; private set; }

    public bool IsActive { get; private set; }

    public AccountNormalBalance? NormalBalanceOverride { get; private set; }

    public AccountNormalBalance DefaultNormalBalance => AccountType is AccountType.Asset or AccountType.Expense ? AccountNormalBalance.Debit : AccountNormalBalance.Credit;

    public AccountNormalBalance NormalBalance => NormalBalanceOverride ?? DefaultNormalBalance;

    public bool IsContraAccount => NormalBalanceOverride.HasValue && NormalBalanceOverride.Value != DefaultNormalBalance;

    public bool RequiresBusinessPartner => AccountRole is AccountRole.AccountsReceivable or AccountRole.AccountsPayable;

    public static bool IsRoleCompatible(AccountType accountType, AccountRole accountRole) => accountRole switch
    {
        AccountRole.None => true,
        AccountRole.Cash or AccountRole.Bank or AccountRole.AccountsReceivable or AccountRole.Inventory => accountType == AccountType.Asset,
        AccountRole.AccountsPayable => accountType == AccountType.Liability,
        AccountRole.Equity or AccountRole.RetainedEarnings or AccountRole.CurrentYearEarnings => accountType == AccountType.Equity,
        AccountRole.SalesRevenue or AccountRole.OtherIncome => accountType == AccountType.Revenue,
        AccountRole.CostOfGoodsSold or AccountRole.Expense or AccountRole.OtherExpense => accountType == AccountType.Expense,
        _ => false
    };

    public void Update(string code, string name, AccountType accountType, AccountRole accountRole, bool isPosting, Guid? parentAccountId, int hierarchyLevel, AccountNormalBalance? normalBalanceOverride = null)
    {
        if (hierarchyLevel < 0) throw new ArgumentOutOfRangeException(nameof(hierarchyLevel));
        EnsureValidConfiguration(accountType, accountRole, isPosting);
        Code = Money.RequireText(code, nameof(code)).ToUpperInvariant();
        Name = Money.RequireText(name, nameof(name));
        AccountType = accountType;
        AccountRole = accountRole;
        IsPosting = isPosting;
        ParentAccountId = parentAccountId;
        HierarchyLevel = hierarchyLevel;
        NormalBalanceOverride = normalBalanceOverride;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static void EnsureValidConfiguration(AccountType accountType, AccountRole accountRole, bool isPosting)
    {
        if (!isPosting && accountRole != AccountRole.None)
        {
            throw new ArgumentException("Header accounts cannot have posting roles.", nameof(accountRole));
        }

        if (!IsRoleCompatible(accountType, accountRole))
        {
            throw new ArgumentException("The account role is not compatible with the account type.", nameof(accountRole));
        }
    }
}

#pragma warning restore CS8618
