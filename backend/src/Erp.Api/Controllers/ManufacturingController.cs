using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using Erp.Api.Contracts.Manufacturing;
using Erp.Application.Authentication;
using Erp.Application.Manufacturing;
using Erp.Application.MasterData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

[ApiController, Route("api/manufacturing"), Tags("Manufacturing")]
public sealed class ManufacturingController(IManufacturingService manufacturingService) : CompanyScopedControllerBase
{
    [HttpGet("bills-of-materials"), Authorize(Policy = "permission:manufacturing.view")]
    public async Task<ActionResult<PagedResult<BillOfMaterialsDto>>> ListBills([FromQuery] BillOfMaterialsListRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await manufacturingService.GetBillsOfMaterialsAsync(companyId, request.ToQuery(), cancellationToken));
    }

    [HttpGet("bills-of-materials/{id:guid}"), Authorize(Policy = "permission:manufacturing.view")]
    public async Task<ActionResult<BillOfMaterialsDto>> GetBill(Guid id, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var bill = await manufacturingService.GetBillOfMaterialsAsync(companyId, id, cancellationToken);
        return bill is null ? NotFound() : Ok(bill);
    }

    [HttpPost("bills-of-materials"), Authorize(Policy = "permission:manufacturing.manage")]
    public async Task<ActionResult<BillOfMaterialsDto>> CreateBill(BillOfMaterialsRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId) || !TryUser(out var userId)) return Forbid();
        var bill = await manufacturingService.CreateBillOfMaterialsAsync(companyId, userId, request.ToInput(), cancellationToken);
        return CreatedAtAction(nameof(GetBill), new { bill.Id }, bill);
    }

    [HttpPut("bills-of-materials/{id:guid}"), Authorize(Policy = "permission:manufacturing.manage")]
    public async Task<ActionResult<BillOfMaterialsDto>> UpdateBill(Guid id, BillOfMaterialsRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var bill = await manufacturingService.UpdateBillOfMaterialsAsync(companyId, id, request.ToInput(), cancellationToken);
        return bill is null ? NotFound() : Ok(bill);
    }

    [HttpGet("production-orders"), Authorize(Policy = "permission:manufacturing.view")]
    public async Task<ActionResult<PagedResult<ProductionOrderDto>>> ListOrders([FromQuery] ProductionOrderListRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await manufacturingService.GetProductionOrdersAsync(companyId, request.ToQuery(), cancellationToken));
    }

    [HttpGet("production-orders/{id:guid}"), Authorize(Policy = "permission:manufacturing.view")]
    public async Task<ActionResult<ProductionOrderDto>> GetOrder(Guid id, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var order = await manufacturingService.GetProductionOrderAsync(companyId, id, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("production-orders"), Authorize(Policy = "permission:manufacturing.manage")]
    public async Task<ActionResult<ProductionOrderDto>> CreateOrder(ProductionOrderRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId) || !TryUser(out var userId)) return Forbid();
        var order = await manufacturingService.CreateProductionOrderAsync(companyId, userId, request.ToInput(), cancellationToken);
        return CreatedAtAction(nameof(GetOrder), new { order.Id }, order);
    }

    [HttpPut("production-orders/{id:guid}"), Authorize(Policy = "permission:manufacturing.manage")]
    public async Task<ActionResult<ProductionOrderDto>> UpdateOrder(Guid id, ProductionOrderRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var order = await manufacturingService.UpdateProductionOrderAsync(companyId, id, request.ToInput(), cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("production-orders/{id:guid}/release"), Authorize(Policy = "permission:manufacturing.manage")]
    public async Task<ActionResult<ProductionOrderDto>> ReleaseOrder(Guid id, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId) || !TryUser(out var userId)) return Forbid();
        var order = await manufacturingService.ReleaseProductionOrderAsync(companyId, id, userId, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpGet("production-orders/{id:guid}/requirements"), Authorize(Policy = "permission:manufacturing.view")]
    public async Task<ActionResult<IReadOnlyCollection<ProductionRequirementDto>>> Requirements(Guid id, [FromQuery, Range(typeof(decimal), "0.000001", "9999999999999")] decimal actualProducedQuantity, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await manufacturingService.GetRequirementsAsync(companyId, id, actualProducedQuantity, cancellationToken));
    }

    [HttpPost("production-orders/{id:guid}/complete"), Authorize(Policy = "permission:manufacturing.manage")]
    public async Task<ActionResult<ProductionOrderDto>> CompleteOrder(Guid id, ProductionCompletionRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId) || !TryUser(out var userId)) return Forbid();
        var order = await manufacturingService.CompleteProductionOrderAsync(companyId, id, userId, request.ActualProducedQuantity, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }
}
