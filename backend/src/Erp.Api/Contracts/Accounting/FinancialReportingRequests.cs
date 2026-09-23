using System.ComponentModel.DataAnnotations;
using Erp.Application.Accounting;

namespace Erp.Api.Contracts.Accounting;

public sealed class FinancialReportRequest
{
    public DateOnly? FromDate { get; init; }

    [Required] public DateOnly? ToDate { get; init; }

    public bool IncludeZeroBalances { get; init; }

    public FinancialReportQuery ToQuery() => new(FromDate, ToDate ?? DateOnly.MinValue, IncludeZeroBalances);
}
