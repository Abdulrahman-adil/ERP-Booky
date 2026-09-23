using System.Security.Claims;
using Erp.Api.Contracts.Payments;
using Erp.Application.Authentication;
using Erp.Application.MasterData;
using Erp.Application.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

[ApiController, Route("api/customer-payments"), Tags("Customer Payments")]
public sealed class CustomerPaymentsController(IPaymentService paymentService) : CompanyScopedControllerBase
{
    [HttpGet, Authorize(Policy = "permission:payments.view")]
    public async Task<ActionResult<PagedResult<CustomerPaymentDto>>> List([FromQuery] CustomerPaymentListRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        return Ok(await paymentService.GetPaymentsAsync(companyId, request.ToQuery(), cancellationToken));
    }

    [HttpGet("customers/{customerId:guid}/open-receivables"), Authorize(Policy = "permission:payments.view")]
    public async Task<ActionResult<IReadOnlyCollection<OpenReceivableDto>>> GetCustomerOpenReceivables(Guid customerId, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        return Ok(await paymentService.GetCustomerOpenReceivablesAsync(companyId, customerId, cancellationToken));
    }

    [HttpGet("invoices/{invoiceId:guid}/summary"), Authorize(Policy = "permission:payments.view")]
    public async Task<ActionResult<InvoicePaymentSummaryDto>> GetInvoiceSummary(Guid invoiceId, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        var summary = await paymentService.GetInvoicePaymentSummaryAsync(companyId, invoiceId, cancellationToken);
        return summary is null ? NotFound() : Ok(summary);
    }

    [HttpGet("{id:guid}"), Authorize(Policy = "permission:payments.view")]
    public async Task<ActionResult<CustomerPaymentDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        var payment = await paymentService.GetPaymentAsync(companyId, id, cancellationToken);
        return payment is null ? NotFound() : Ok(payment);
    }

    [HttpPost, Authorize(Policy = "permission:payments.manage")]
    public async Task<ActionResult<CustomerPaymentDto>> Create(CustomerPaymentRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId) || !TryGetUserId(out var userId)) return Forbid();
        var payment = await paymentService.CreateDraftAsync(companyId, userId, request.ToInput(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { payment.Id }, payment);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = "permission:payments.manage")]
    public async Task<ActionResult<CustomerPaymentDto>> UpdateDraft(Guid id, CustomerPaymentRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        var payment = await paymentService.UpdateDraftAsync(companyId, id, request.ToInput(), cancellationToken);
        return payment is null ? NotFound() : Ok(payment);
    }

    [HttpPost("{id:guid}/post"), Authorize(Policy = "permission:payments.manage")]
    public async Task<ActionResult<CustomerPaymentDto>> Post(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId) || !TryGetUserId(out var userId)) return Forbid();
        var payment = await paymentService.PostAsync(companyId, id, userId, cancellationToken);
        return payment is null ? NotFound() : Ok(payment);
    }
}

[ApiController, Route("api/receivables"), Tags("Accounts Receivable")]
public sealed class ReceivablesController(IPaymentService paymentService) : CompanyScopedControllerBase
{
    [HttpGet, Authorize(Policy = "permission:receivables.view")]
    public async Task<ActionResult<PagedResult<OpenReceivableDto>>> List([FromQuery] ReceivableListRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        return Ok(await paymentService.GetReceivablesAsync(companyId, request.ToQuery(), cancellationToken));
    }
}

[ApiController, Route("api/cash-bank-accounts"), Tags("Cash and Bank Accounts")]
public sealed class CashBankAccountsController(IPaymentService paymentService) : CompanyScopedControllerBase
{
    [HttpGet, Authorize(Policy = "permission:payments.view")]
    public async Task<ActionResult<IReadOnlyCollection<CashBankAccountDto>>> List(CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        return Ok(await paymentService.GetCashBankAccountsAsync(companyId, cancellationToken));
    }

    [HttpPost, Authorize(Policy = "permission:cash-bank.manage")]
    public async Task<ActionResult<CashBankAccountDto>> Create(CashBankAccountRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        var account = await paymentService.CreateCashBankAccountAsync(companyId, request.ToInput(), cancellationToken);
        return CreatedAtAction(nameof(List), account);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = "permission:cash-bank.manage")]
    public async Task<ActionResult<CashBankAccountDto>> Update(Guid id, CashBankAccountUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        var account = await paymentService.UpdateCashBankAccountAsync(companyId, id, request.ToInput(), cancellationToken);
        return account is null ? NotFound() : Ok(account);
    }
}
