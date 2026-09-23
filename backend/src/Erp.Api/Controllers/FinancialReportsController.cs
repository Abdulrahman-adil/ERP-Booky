using System.Security.Claims;
using Erp.Api.Contracts.Accounting;
using Erp.Application.Accounting;
using Erp.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

[ApiController, Route("api/financial-reports"), Tags("Financial Reports")]
public sealed class FinancialReportsController(IFinancialReportingService reporting, IFinancialReportExportService exports) : CompanyScopedControllerBase
{
    [HttpGet("trial-balance"), Authorize(Policy = "permission:financialreports.view")]
    public async Task<ActionResult<TrialBalanceReportDto>> TrialBalance([FromQuery] FinancialReportRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await reporting.GetTrialBalanceAsync(companyId, request.ToQuery(), cancellationToken));
    }

    [HttpGet("income-statement"), Authorize(Policy = "permission:financialreports.view")]
    public async Task<ActionResult<IncomeStatementReportDto>> IncomeStatement([FromQuery] FinancialReportRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await reporting.GetIncomeStatementAsync(companyId, request.ToQuery(), cancellationToken));
    }

    [HttpGet("balance-sheet"), Authorize(Policy = "permission:financialreports.view")]
    public async Task<ActionResult<BalanceSheetReportDto>> BalanceSheet([FromQuery] FinancialReportRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await reporting.GetBalanceSheetAsync(companyId, request.ToQuery(), cancellationToken));
    }

    [HttpGet("trial-balance/export"), Authorize(Policy = "permission:financialreports.export")]
    public async Task<IActionResult> ExportTrialBalance([FromQuery] FinancialReportRequest request, [FromQuery] FinancialReportExportFormat format, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return FileFrom(await exports.ExportTrialBalanceAsync(companyId, request.ToQuery(), format, cancellationToken));
    }

    [HttpGet("income-statement/export"), Authorize(Policy = "permission:financialreports.export")]
    public async Task<IActionResult> ExportIncomeStatement([FromQuery] FinancialReportRequest request, [FromQuery] FinancialReportExportFormat format, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return FileFrom(await exports.ExportIncomeStatementAsync(companyId, request.ToQuery(), format, cancellationToken));
    }

    [HttpGet("balance-sheet/export"), Authorize(Policy = "permission:financialreports.export")]
    public async Task<IActionResult> ExportBalanceSheet([FromQuery] FinancialReportRequest request, [FromQuery] FinancialReportExportFormat format, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return FileFrom(await exports.ExportBalanceSheetAsync(companyId, request.ToQuery(), format, cancellationToken));
    }

    private FileContentResult FileFrom(FinancialReportExport export) => File(export.Content, export.ContentType, export.FileName);
}
