using System.Security.Claims;
using Erp.Api.Contracts.Inventory;
using Erp.Api.Contracts.MasterData;
using Erp.Application.Authentication;
using Erp.Application.Inventory;
using Erp.Application.MasterData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

public abstract class InventoryControllerBase(IInventoryService inventoryService) : CompanyScopedControllerBase
{
    protected IInventoryService InventoryService { get; } = inventoryService;
}

[ApiController, Route("api/warehouses"), Tags("Warehouses")]
public sealed class WarehousesController(IInventoryService inventoryService) : InventoryControllerBase(inventoryService)
{
    [HttpGet, Authorize(Policy = "permission:inventory.view")]
    public async Task<ActionResult<PagedResult<WarehouseDto>>> List([FromQuery] WarehouseListRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        return Ok(await InventoryService.GetWarehousesAsync(companyId, request.ToQuery(), cancellationToken));
    }

    [HttpGet("{id:guid}"), Authorize(Policy = "permission:inventory.view")]
    public async Task<ActionResult<WarehouseDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        var warehouse = await InventoryService.GetWarehouseAsync(companyId, id, cancellationToken);
        return warehouse is null ? NotFound() : Ok(warehouse);
    }

    [HttpGet("{id:guid}/details"), Authorize(Policy = "permission:inventory.view")]
    public async Task<ActionResult<WarehouseDetailsDto>> GetDetails(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        var warehouse = await InventoryService.GetWarehouseDetailsAsync(companyId, id, cancellationToken);
        return warehouse is null ? NotFound() : Ok(warehouse);
    }

    [HttpPost, Authorize(Policy = "permission:inventory.manage")]
    public async Task<ActionResult<WarehouseDto>> Create(WarehouseRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        var warehouse = await InventoryService.CreateWarehouseAsync(companyId, request.ToInput(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { warehouse.Id }, warehouse);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = "permission:inventory.manage")]
    public async Task<ActionResult<WarehouseDto>> Update(Guid id, WarehouseRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        var warehouse = await InventoryService.UpdateWarehouseAsync(companyId, id, request.ToInput(), cancellationToken);
        return warehouse is null ? NotFound() : Ok(warehouse);
    }

    [HttpPut("{id:guid}/status"), Authorize(Policy = "permission:inventory.manage")]
    public async Task<IActionResult> SetStatus(Guid id, StatusRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        return await InventoryService.SetWarehouseActiveAsync(companyId, id, request.IsActive, cancellationToken) ? NoContent() : NotFound();
    }
}

[ApiController, Route("api/inventory"), Tags("Inventory")]
public sealed class InventoryController(IInventoryService inventoryService) : InventoryControllerBase(inventoryService)
{
    [HttpGet("overview"), Authorize(Policy = "permission:inventory.view")]
    public async Task<ActionResult<InventoryOverviewDto>> GetOverview(CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        return Ok(await InventoryService.GetOverviewAsync(companyId, cancellationToken));
    }

    [HttpGet("ledger"), Authorize(Policy = "permission:inventory.view")]
    public async Task<ActionResult<PagedResult<StockMovementDto>>> GetLedger([FromQuery] StockLedgerListRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        return Ok(await InventoryService.GetLedgerAsync(companyId, request.ToQuery(), cancellationToken));
    }

    [HttpGet("products/{productId:guid}"), Authorize(Policy = "permission:inventory.view")]
    public async Task<ActionResult<ProductInventoryDto>> GetProductInventory(Guid productId, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        var inventory = await InventoryService.GetProductInventoryAsync(companyId, productId, cancellationToken);
        return inventory is null ? NotFound() : Ok(inventory);
    }

    [HttpPost("movements"), Authorize(Policy = "permission:inventory.manage")]
    public async Task<ActionResult<StockMovementDto>> RecordMovement(InventoryMovementRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId) || !TryGetUserId(out var userId)) return Forbid();
        var movement = await InventoryService.RecordMovementAsync(companyId, userId, request.ToInput(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, movement);
    }
}
