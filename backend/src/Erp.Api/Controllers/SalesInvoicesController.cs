using System.Security.Claims;
using Erp.Api.Contracts.Sales;
using Erp.Application.Authentication;
using Erp.Application.MasterData;
using Erp.Application.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

[ApiController, Route("api/sales-invoices"), Tags("Sales Invoices")]
public sealed class SalesInvoicesController(ISalesService salesService) : CompanyScopedControllerBase
{
    [HttpGet, Authorize(Policy = "permission:sales.view")]
    public async Task<ActionResult<PagedResult<SalesInvoiceDto>>> List([FromQuery] SalesInvoiceListRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        return Ok(await salesService.GetInvoicesAsync(companyId, request.ToQuery(), cancellationToken));
    }

    [HttpGet("dashboard"), Authorize(Policy = "permission:sales.view")]
    public async Task<ActionResult<SalesDashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        return Ok(await salesService.GetDashboardAsync(companyId, cancellationToken));
    }

    [HttpGet("customers/{customerId:guid}/summary"), Authorize(Policy = "permission:sales.view")]
    public async Task<ActionResult<CustomerSalesSummaryDto>> GetCustomerSummary(Guid customerId, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        var summary = await salesService.GetCustomerSummaryAsync(companyId, customerId, cancellationToken);
        return summary is null ? NotFound() : Ok(summary);
    }

    [HttpGet("{id:guid}"), Authorize(Policy = "permission:sales.view")]
    public async Task<ActionResult<SalesInvoiceDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        var invoice = await salesService.GetInvoiceAsync(companyId, id, cancellationToken);
        return invoice is null ? NotFound() : Ok(invoice);
    }

    [HttpPost, Authorize(Policy = "permission:sales.manage")]
    public async Task<ActionResult<SalesInvoiceDto>> Create(SalesInvoiceRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId) || !TryGetUserId(out var userId)) return Forbid();
        var invoice = await salesService.CreateDraftAsync(companyId, userId, request.ToInput(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { invoice.Id }, invoice);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = "permission:sales.manage")]
    public async Task<ActionResult<SalesInvoiceDto>> UpdateDraft(Guid id, SalesInvoiceRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        var invoice = await salesService.UpdateDraftAsync(companyId, id, request.ToInput(), cancellationToken);
        return invoice is null ? NotFound() : Ok(invoice);
    }

    [HttpPost("{id:guid}/post"), Authorize(Policy = "permission:sales.manage")]
    public async Task<ActionResult<SalesInvoiceDto>> Post(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId) || !TryGetUserId(out var userId)) return Forbid();
        var invoice = await salesService.PostAsync(companyId, id, userId, cancellationToken);
        return invoice is null ? NotFound() : Ok(invoice);
    }
}
