using Erp.Domain.Accounting;

namespace Erp.Application.Accounting;

public sealed record FinancialReportQuery(DateOnly? FromDate, DateOnly ToDate, bool IncludeZeroBalances = false);

public sealed record TrialBalanceRowDto(
    Guid AccountId,
    string Code,
    string Name,
    AccountType AccountType,
    AccountRole AccountRole,
    int HierarchyLevel,
    bool IsPosting,
    bool IsContraAccount,
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal PeriodDebit,
    decimal PeriodCredit,
    decimal ClosingDebit,
    decimal ClosingCredit);

public sealed record TrialBalanceReportDto(
    string CurrencyCode,
    DateOnly? FromDate,
    DateOnly ToDate,
    IReadOnlyCollection<TrialBalanceRowDto> Rows,
    decimal OpeningDebitTotal,
    decimal OpeningCreditTotal,
    decimal PeriodDebitTotal,
    decimal PeriodCreditTotal,
    decimal ClosingDebitTotal,
    decimal ClosingCreditTotal)
{
    public decimal Difference => ClosingDebitTotal - ClosingCreditTotal;
}

public sealed record FinancialStatementAccountRowDto(
    Guid AccountId,
    string Code,
    string Name,
    AccountType AccountType,
    AccountRole AccountRole,
    int HierarchyLevel,
    bool IsPosting,
    bool IsContraAccount,
    bool IsDerived,
    decimal Amount);

public sealed record FinancialStatementSectionDto(string Key, string Name, decimal Total, IReadOnlyCollection<FinancialStatementAccountRowDto> Rows);

public sealed record IncomeStatementReportDto(
    string CurrencyCode,
    DateOnly FromDate,
    DateOnly ToDate,
    FinancialStatementSectionDto Revenue,
    FinancialStatementSectionDto CostOfSales,
    decimal GrossProfit,
    FinancialStatementSectionDto OperatingExpenses,
    decimal OperatingProfit,
    FinancialStatementSectionDto OtherIncome,
    FinancialStatementSectionDto OtherExpenses,
    decimal NetProfit);

public sealed record BalanceSheetReportDto(
    string CurrencyCode,
    DateOnly AsOfDate,
    FinancialStatementSectionDto Assets,
    FinancialStatementSectionDto Liabilities,
    FinancialStatementSectionDto Equity,
    decimal CurrentYearEarnings,
    bool CurrentYearEarningsIsDerived,
    decimal TotalAssets,
    decimal TotalLiabilitiesAndEquity)
{
    public decimal Difference => TotalAssets - TotalLiabilitiesAndEquity;
}

public enum FinancialReportExportFormat
{
    Xlsx,
    Pdf
}

public sealed record FinancialReportExport(string FileName, string ContentType, byte[] Content);

public interface IFinancialReportingService
{
    Task<TrialBalanceReportDto> GetTrialBalanceAsync(Guid companyId, FinancialReportQuery query, CancellationToken cancellationToken = default);
    Task<IncomeStatementReportDto> GetIncomeStatementAsync(Guid companyId, FinancialReportQuery query, CancellationToken cancellationToken = default);
    Task<BalanceSheetReportDto> GetBalanceSheetAsync(Guid companyId, FinancialReportQuery query, CancellationToken cancellationToken = default);
}

public interface IFinancialReportExportService
{
    Task<FinancialReportExport> ExportTrialBalanceAsync(Guid companyId, FinancialReportQuery query, FinancialReportExportFormat format, CancellationToken cancellationToken = default);
    Task<FinancialReportExport> ExportIncomeStatementAsync(Guid companyId, FinancialReportQuery query, FinancialReportExportFormat format, CancellationToken cancellationToken = default);
    Task<FinancialReportExport> ExportBalanceSheetAsync(Guid companyId, FinancialReportQuery query, FinancialReportExportFormat format, CancellationToken cancellationToken = default);
}
