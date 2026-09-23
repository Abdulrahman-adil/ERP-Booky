using Erp.Application.Accounting;
using Erp.Domain.Accounting;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Persistence;

public sealed class EfFinancialReportingService(ErpDbContext dbContext) : IFinancialReportingService
{
    public async Task<TrialBalanceReportDto> GetTrialBalanceAsync(Guid companyId, FinancialReportQuery query, CancellationToken cancellationToken = default)
    {
        ValidateQuery(query);
        var data = await LoadReportDataAsync(companyId, query.ToDate, cancellationToken);
        var rows = BuildTrialBalanceRows(data.Accounts, data.Movements, query).ToArray();
        var postingRows = rows.Where(row => row.IsPosting).ToArray();
        return new TrialBalanceReportDto(
            data.CurrencyCode,
            query.FromDate,
            query.ToDate,
            rows,
            postingRows.Sum(row => row.OpeningDebit),
            postingRows.Sum(row => row.OpeningCredit),
            postingRows.Sum(row => row.PeriodDebit),
            postingRows.Sum(row => row.PeriodCredit),
            postingRows.Sum(row => row.ClosingDebit),
            postingRows.Sum(row => row.ClosingCredit));
    }

    public async Task<IncomeStatementReportDto> GetIncomeStatementAsync(Guid companyId, FinancialReportQuery query, CancellationToken cancellationToken = default)
    {
        ValidateQuery(query);
        var data = await LoadReportDataAsync(companyId, query.ToDate, cancellationToken);
        return BuildIncomeStatement(data.CurrencyCode, data.Accounts, data.Movements, query);
    }

    public async Task<BalanceSheetReportDto> GetBalanceSheetAsync(Guid companyId, FinancialReportQuery query, CancellationToken cancellationToken = default)
    {
        ValidateQuery(query);
        var data = await LoadReportDataAsync(companyId, query.ToDate, cancellationToken);
        var balanceQuery = query with { FromDate = null };
        var currentYearQuery = new FinancialReportQuery(new DateOnly(query.ToDate.Year, 1, 1), query.ToDate, query.IncludeZeroBalances);
        var currentYearIncome = BuildIncomeStatement(data.CurrencyCode, data.Accounts, data.Movements, currentYearQuery);
        var currentEarningsAccount = data.Accounts.SingleOrDefault(account => account.AccountRole == AccountRole.CurrentYearEarnings);
        var currentEarningsActual = currentEarningsAccount is null
            ? 0m
            : StatementAmount(currentEarningsAccount, Sum(data.Movements, currentEarningsAccount.Id, currentYearQuery.FromDate, currentYearQuery.ToDate));
        var currentEarningsIsDerived = currentEarningsAccount is null || currentEarningsActual == 0;
        var currentEarnings = currentEarningsIsDerived ? currentYearIncome.NetProfit : currentEarningsActual;
        var overrides = currentEarningsAccount is null ? new Dictionary<Guid, decimal>() : new Dictionary<Guid, decimal> { [currentEarningsAccount.Id] = currentEarnings };

        var assets = BuildStatementSection("assets", "Assets", data.Accounts, data.Movements, balanceQuery, account => account.AccountType == AccountType.Asset, overrides, query.IncludeZeroBalances);
        var liabilities = BuildStatementSection("liabilities", "Liabilities", data.Accounts, data.Movements, balanceQuery, account => account.AccountType == AccountType.Liability, overrides, query.IncludeZeroBalances);
        var equity = BuildStatementSection("equity", "Equity", data.Accounts, data.Movements, balanceQuery, account => account.AccountType == AccountType.Equity, overrides, query.IncludeZeroBalances, currentEarningsAccount?.Id, currentEarningsIsDerived);

        return new BalanceSheetReportDto(
            data.CurrencyCode,
            query.ToDate,
            assets,
            liabilities,
            equity,
            currentEarnings,
            currentEarningsIsDerived,
            assets.Total,
            liabilities.Total + equity.Total);
    }

    private static IncomeStatementReportDto BuildIncomeStatement(string currencyCode, IReadOnlyCollection<Account> accounts, IReadOnlyCollection<LedgerMovement> movements, FinancialReportQuery query)
    {
        var revenue = BuildStatementSection("revenue", "Revenue", accounts, movements, query, account => account.AccountType == AccountType.Revenue && account.AccountRole != AccountRole.OtherIncome, null, query.IncludeZeroBalances);
        var costOfSales = BuildStatementSection("cost-of-sales", "Cost of sales", accounts, movements, query, account => account.AccountRole == AccountRole.CostOfGoodsSold, null, query.IncludeZeroBalances);
        var operatingExpenses = BuildStatementSection("operating-expenses", "Operating expenses", accounts, movements, query, account => account.AccountType == AccountType.Expense && account.AccountRole is not AccountRole.CostOfGoodsSold and not AccountRole.OtherExpense, null, query.IncludeZeroBalances);
        var otherIncome = BuildStatementSection("other-income", "Other income", accounts, movements, query, account => account.AccountRole == AccountRole.OtherIncome, null, query.IncludeZeroBalances);
        var otherExpenses = BuildStatementSection("other-expenses", "Other expenses", accounts, movements, query, account => account.AccountRole == AccountRole.OtherExpense, null, query.IncludeZeroBalances);
        var grossProfit = revenue.Total - costOfSales.Total;
        var operatingProfit = grossProfit - operatingExpenses.Total;
        var netProfit = operatingProfit + otherIncome.Total - otherExpenses.Total;
        return new IncomeStatementReportDto(currencyCode, query.FromDate ?? DateOnly.MinValue, query.ToDate, revenue, costOfSales, grossProfit, operatingExpenses, operatingProfit, otherIncome, otherExpenses, netProfit);
    }

    private static FinancialStatementSectionDto BuildStatementSection(
        string key,
        string name,
        IReadOnlyCollection<Account> accounts,
        IReadOnlyCollection<LedgerMovement> movements,
        FinancialReportQuery query,
        Func<Account, bool> includesPostingAccount,
        IReadOnlyDictionary<Guid, decimal>? overrides,
        bool includeZeroBalances,
        Guid? derivedAccountId = null,
        bool derivedAccount = false)
    {
        var includedPostingIds = accounts.Where(account => account.IsPosting && includesPostingAccount(account)).Select(account => account.Id).ToHashSet();
        var visibleIds = new HashSet<Guid>(includedPostingIds);
        var accountsById = accounts.ToDictionary(account => account.Id);
        foreach (var accountId in includedPostingIds)
        {
            var parentId = accountsById[accountId].ParentAccountId;
            while (parentId.HasValue && accountsById.TryGetValue(parentId.Value, out var parent))
            {
                visibleIds.Add(parent.Id);
                parentId = parent.ParentAccountId;
            }
        }

        var children = accounts.Where(account => visibleIds.Contains(account.Id) && account.ParentAccountId.HasValue).GroupBy(account => account.ParentAccountId!.Value).ToDictionary(group => group.Key, group => group.OrderBy(account => account.Code).ToArray());
        var rows = new List<FinancialStatementAccountRowDto>();

        decimal Append(Account account)
        {
            var rowIndex = rows.Count;
            rows.Add(new FinancialStatementAccountRowDto(account.Id, account.Code, account.Name, account.AccountType, account.AccountRole, account.HierarchyLevel, account.IsPosting, account.IsContraAccount, derivedAccount && account.Id == derivedAccountId, 0));
            var directAmount = overrides is not null && overrides.TryGetValue(account.Id, out var overridden)
                ? overridden
                : StatementAmount(account, Sum(movements, account.Id, query.FromDate, query.ToDate));
            var childAmount = 0m;
            if (children.TryGetValue(account.Id, out var childAccounts))
            {
                foreach (var child in childAccounts) childAmount += Append(child);
            }

            var amount = account.IsPosting ? directAmount : childAmount;
            if (!includeZeroBalances && account.IsPosting && amount == 0)
            {
                rows.RemoveAt(rowIndex);
            }
            else
            {
                rows[rowIndex] = new FinancialStatementAccountRowDto(account.Id, account.Code, account.Name, account.AccountType, account.AccountRole, account.HierarchyLevel, account.IsPosting, account.IsContraAccount, derivedAccount && account.Id == derivedAccountId, amount);
            }
            return amount;
        }

        var total = 0m;
        foreach (var root in accounts.Where(account => visibleIds.Contains(account.Id) && (!account.ParentAccountId.HasValue || !visibleIds.Contains(account.ParentAccountId.Value))).OrderBy(account => account.Code))
        {
            total += Append(root);
        }
        return new FinancialStatementSectionDto(key, name, total, rows);
    }

    private static IEnumerable<TrialBalanceRowDto> BuildTrialBalanceRows(IReadOnlyCollection<Account> accounts, IReadOnlyCollection<LedgerMovement> movements, FinancialReportQuery query)
    {
        var childAccounts = accounts.Where(account => account.ParentAccountId.HasValue).GroupBy(account => account.ParentAccountId!.Value).ToDictionary(group => group.Key, group => group.OrderBy(account => account.Code).ToArray());
        var rows = new List<TrialBalanceRowDto>();

        (decimal OpeningDebit, decimal OpeningCredit, decimal PeriodDebit, decimal PeriodCredit, decimal ClosingDebit, decimal ClosingCredit) Append(Account account)
        {
            var rowIndex = rows.Count;
            rows.Add(new TrialBalanceRowDto(account.Id, account.Code, account.Name, account.AccountType, account.AccountRole, account.HierarchyLevel, account.IsPosting, account.IsContraAccount, 0, 0, 0, 0, 0, 0));
            var opening = query.FromDate.HasValue ? Sum(movements, account.Id, null, query.FromDate.Value.AddDays(-1)) : LedgerAmount.Zero;
            var period = Sum(movements, account.Id, query.FromDate, query.ToDate);
            var direct = (opening.Debit, opening.Credit, period.Debit, period.Credit);
            var child = (OpeningDebit: 0m, OpeningCredit: 0m, PeriodDebit: 0m, PeriodCredit: 0m, ClosingDebit: 0m, ClosingCredit: 0m);
            if (childAccounts.TryGetValue(account.Id, out var children))
            {
                foreach (var item in children)
                {
                    var childResult = Append(item);
                    child.OpeningDebit += childResult.OpeningDebit;
                    child.OpeningCredit += childResult.OpeningCredit;
                    child.PeriodDebit += childResult.PeriodDebit;
                    child.PeriodCredit += childResult.PeriodCredit;
                    child.ClosingDebit += childResult.ClosingDebit;
                    child.ClosingCredit += childResult.ClosingCredit;
                }
            }

            var openingNet = direct.Item1 - direct.Item2;
            var closingNet = openingNet + direct.Item3 - direct.Item4;
            var result = account.IsPosting
                ? (Math.Max(openingNet, 0), Math.Max(-openingNet, 0), direct.Item3, direct.Item4, Math.Max(closingNet, 0), Math.Max(-closingNet, 0))
                : child;
            if (query.IncludeZeroBalances || result != (0m, 0m, 0m, 0m, 0m, 0m) || !account.IsPosting)
            {
                rows[rowIndex] = new TrialBalanceRowDto(account.Id, account.Code, account.Name, account.AccountType, account.AccountRole, account.HierarchyLevel, account.IsPosting, account.IsContraAccount, result.Item1, result.Item2, result.Item3, result.Item4, result.Item5, result.Item6);
            }
            else
            {
                rows.RemoveAt(rowIndex);
            }
            return result;
        }

        foreach (var root in accounts.Where(account => !account.ParentAccountId.HasValue).OrderBy(account => account.Code)) Append(root);
        return rows;
    }

    private async Task<ReportData> LoadReportDataAsync(Guid companyId, DateOnly toDate, CancellationToken cancellationToken)
    {
        var chartId = await dbContext.ChartsOfAccounts.AsNoTracking().Where(chart => chart.CompanyId == companyId).Select(chart => (Guid?)chart.Id).SingleOrDefaultAsync(cancellationToken);
        var currencyCode = await dbContext.Companies.AsNoTracking().Where(company => company.Id == companyId).Select(company => company.BaseCurrencyCode).SingleOrDefaultAsync(cancellationToken) ?? "USD";
        if (!chartId.HasValue) return new ReportData(currencyCode, [], []);
        var accounts = await dbContext.Accounts.AsNoTracking().Where(account => account.ChartOfAccountsId == chartId.Value).OrderBy(account => account.Code).ToArrayAsync(cancellationToken);
        var movements = await (
            from ledger in dbContext.GeneralLedgerEntries.AsNoTracking()
            join journal in dbContext.JournalEntries.AsNoTracking() on ledger.JournalEntryId equals journal.Id
            where ledger.CompanyId == companyId && ledger.PostedDate <= toDate && journal.Status == JournalEntryStatus.Posted
            select new LedgerMovement(ledger.AccountId, ledger.PostedDate, ledger.Debit.Amount, ledger.Credit.Amount))
            .ToArrayAsync(cancellationToken);
        return new ReportData(currencyCode, accounts, movements);
    }

    private static LedgerAmount Sum(IReadOnlyCollection<LedgerMovement> movements, Guid accountId, DateOnly? fromDate, DateOnly toDate) => new(
        movements.Where(movement => movement.AccountId == accountId && (!fromDate.HasValue || movement.PostedDate >= fromDate.Value) && movement.PostedDate <= toDate).Sum(movement => movement.Debit),
        movements.Where(movement => movement.AccountId == accountId && (!fromDate.HasValue || movement.PostedDate >= fromDate.Value) && movement.PostedDate <= toDate).Sum(movement => movement.Credit));

    private static decimal StatementAmount(Account account, LedgerAmount balance)
    {
        var amount = account.NormalBalance == AccountNormalBalance.Debit ? balance.Debit - balance.Credit : balance.Credit - balance.Debit;
        return account.IsContraAccount ? -amount : amount;
    }

    private static void ValidateQuery(FinancialReportQuery query)
    {
        if (query.ToDate == DateOnly.MinValue) throw new AccountingValidationException("A report end date is required.");
        if (query.FromDate.HasValue && query.FromDate.Value > query.ToDate) throw new AccountingValidationException("The report start date cannot be later than the end date.");
    }

    private sealed record ReportData(string CurrencyCode, IReadOnlyCollection<Account> Accounts, IReadOnlyCollection<LedgerMovement> Movements);
    private sealed record LedgerMovement(Guid AccountId, DateOnly PostedDate, decimal Debit, decimal Credit);
    private readonly record struct LedgerAmount(decimal Debit, decimal Credit)
    {
        public static LedgerAmount Zero => new(0, 0);
    }
}
