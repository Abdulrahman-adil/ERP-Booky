using System.Security.Claims;
using Erp.Api.Contracts.Purchasing;
using Erp.Application.Authentication;
using Erp.Application.MasterData;
using Erp.Application.Purchasing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

[ApiController, Route("api/purchase-bills"), Tags("Purchase Bills")]
public sealed class PurchaseBillsController(IPurchaseService purchases) : CompanyScopedControllerBase
{
    [HttpGet, Authorize(Policy = "permission:purchases.view")]
    public async Task<ActionResult<PagedResult<PurchaseInvoiceDto>>> List([FromQuery] PurchaseInvoiceListRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await purchases.GetBillsAsync(companyId, request.ToQuery(), cancellationToken));
    }

    [HttpGet("dashboard"), Authorize(Policy = "permission:purchases.view")]
    public async Task<ActionResult<PurchaseDashboardDto>> Dashboard(CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await purchases.GetDashboardAsync(companyId, cancellationToken));
    }

    [HttpGet("suppliers/{supplierId:guid}/summary"), Authorize(Policy = "permission:purchases.view")]
    public async Task<ActionResult<SupplierPurchaseSummaryDto>> SupplierSummary(Guid supplierId, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var summary = await purchases.GetSupplierSummaryAsync(companyId, supplierId, cancellationToken);
        return summary is null ? NotFound() : Ok(summary);
    }

    [HttpGet("{id:guid}"), Authorize(Policy = "permission:purchases.view")]
    public async Task<ActionResult<PurchaseInvoiceDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var bill = await purchases.GetBillAsync(companyId, id, cancellationToken);
        return bill is null ? NotFound() : Ok(bill);
    }

    [HttpPost, Authorize(Policy = "permission:purchases.manage")]
    public async Task<ActionResult<PurchaseInvoiceDto>> Create(PurchaseInvoiceRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId) || !TryUser(out var userId)) return Forbid();
        var bill = await purchases.CreateDraftBillAsync(companyId, userId, request.ToInput(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { bill.Id }, bill);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = "permission:purchases.manage")]
    public async Task<ActionResult<PurchaseInvoiceDto>> Update(Guid id, PurchaseInvoiceRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var bill = await purchases.UpdateDraftBillAsync(companyId, id, request.ToInput(), cancellationToken);
        return bill is null ? NotFound() : Ok(bill);
    }

    [HttpDelete("{id:guid}"), Authorize(Policy = "permission:purchases.manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return await purchases.DeleteDraftBillAsync(companyId, id, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/post"), Authorize(Policy = "permission:purchases.manage")]
    public async Task<ActionResult<PurchaseInvoiceDto>> Post(Guid id, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId) || !TryUser(out var userId)) return Forbid();
        var bill = await purchases.PostBillAsync(companyId, id, userId, cancellationToken);
        return bill is null ? NotFound() : Ok(bill);
    }
}

[ApiController, Route("api/supplier-payments"), Tags("Supplier Payments")]
public sealed class SupplierPaymentsController(IPurchaseService purchases) : CompanyScopedControllerBase
{
    [HttpGet, Authorize(Policy = "permission:supplierpayments.view")]
    public async Task<ActionResult<PagedResult<SupplierPaymentDto>>> List([FromQuery] SupplierPaymentListRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await purchases.GetSupplierPaymentsAsync(companyId, request.ToQuery(), cancellationToken));
    }

    [HttpGet("suppliers/{supplierId:guid}/open-payables"), Authorize(Policy = "permission:supplierpayments.view")]
    public async Task<ActionResult<IReadOnlyCollection<SupplierPayableDto>>> OpenPayables(Guid supplierId, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await purchases.GetSupplierOpenPayablesAsync(companyId, supplierId, cancellationToken));
    }

    [HttpGet("bills/{billId:guid}/summary"), Authorize(Policy = "permission:supplierpayments.view")]
    public async Task<ActionResult<PurchaseBillPaymentSummaryDto>> BillSummary(Guid billId, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var summary = await purchases.GetBillPaymentSummaryAsync(companyId, billId, cancellationToken);
        return summary is null ? NotFound() : Ok(summary);
    }

    [HttpGet("{id:guid}"), Authorize(Policy = "permission:supplierpayments.view")]
    public async Task<ActionResult<SupplierPaymentDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var payment = await purchases.GetSupplierPaymentAsync(companyId, id, cancellationToken);
        return payment is null ? NotFound() : Ok(payment);
    }

    [HttpPost, Authorize(Policy = "permission:supplierpayments.manage")]
    public async Task<ActionResult<SupplierPaymentDto>> Create(SupplierPaymentRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId) || !TryUser(out var userId)) return Forbid();
        var payment = await purchases.CreateDraftSupplierPaymentAsync(companyId, userId, request.ToInput(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { payment.Id }, payment);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = "permission:supplierpayments.manage")]
    public async Task<ActionResult<SupplierPaymentDto>> Update(Guid id, SupplierPaymentRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var payment = await purchases.UpdateDraftSupplierPaymentAsync(companyId, id, request.ToInput(), cancellationToken);
        return payment is null ? NotFound() : Ok(payment);
    }

    [HttpPost("{id:guid}/post"), Authorize(Policy = "permission:supplierpayments.manage")]
    public async Task<ActionResult<SupplierPaymentDto>> Post(Guid id, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId) || !TryUser(out var userId)) return Forbid();
        var payment = await purchases.PostSupplierPaymentAsync(companyId, id, userId, cancellationToken);
        return payment is null ? NotFound() : Ok(payment);
    }
}

[ApiController, Route("api/payables"), Tags("Accounts Payable")]
public sealed class PayablesController(IPurchaseService purchases) : CompanyScopedControllerBase
{
    [HttpGet, Authorize(Policy = "permission:payables.view")]
    public async Task<ActionResult<PagedResult<SupplierPayableDto>>> List([FromQuery] SupplierPayableListRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        return Ok(await purchases.GetPayablesAsync(companyId, request.ToQuery(), cancellationToken));
    }
}
