using System.Security.Claims;
using Erp.Api.Contracts.MasterData;
using Erp.Application.Authentication;
using Erp.Application.MasterData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

public abstract class MasterDataControllerBase(IMasterDataService masterDataService) : CompanyScopedControllerBase
{
    protected IMasterDataService MasterDataService { get; } = masterDataService;
}

[ApiController, Route("api/customers"), Tags("Customers")]
public sealed class CustomersController(IMasterDataService masterDataService) : MasterDataControllerBase(masterDataService)
{
    [HttpGet, Authorize(Policy = "permission:customers.view")]
    public async Task<ActionResult<PagedResult<BusinessPartnerDto>>> List([FromQuery] MasterDataListRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        return Ok(await MasterDataService.GetCustomersAsync(companyId, request.ToQuery(), cancellationToken));
    }

    [HttpGet("{id:guid}"), Authorize(Policy = "permission:customers.view")]
    public async Task<ActionResult<BusinessPartnerDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        var customer = await MasterDataService.GetCustomerAsync(companyId, id, cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPost, Authorize(Policy = "permission:customers.manage")]
    public async Task<ActionResult<BusinessPartnerDto>> Create(BusinessPartnerRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        var customer = await MasterDataService.CreateCustomerAsync(companyId, request.ToInput(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { customer.Id }, customer);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = "permission:customers.manage")]
    public async Task<ActionResult<BusinessPartnerDto>> Update(Guid id, BusinessPartnerRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        var customer = await MasterDataService.UpdateCustomerAsync(companyId, id, request.ToInput(), cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPut("{id:guid}/status"), Authorize(Policy = "permission:customers.manage")]
    public async Task<IActionResult> SetStatus(Guid id, StatusRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        return await MasterDataService.SetCustomerActiveAsync(companyId, id, request.IsActive, cancellationToken) ? NoContent() : NotFound();
    }
}

[ApiController, Route("api/suppliers"), Tags("Suppliers")]
public sealed class SuppliersController(IMasterDataService masterDataService) : MasterDataControllerBase(masterDataService)
{
    [HttpGet, Authorize(Policy = "permission:purchases.view")]
    public async Task<ActionResult<PagedResult<BusinessPartnerDto>>> List([FromQuery] MasterDataListRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        return Ok(await MasterDataService.GetSuppliersAsync(companyId, request.ToQuery(), cancellationToken));
    }

    [HttpGet("{id:guid}"), Authorize(Policy = "permission:purchases.view")]
    public async Task<ActionResult<BusinessPartnerDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        var supplier = await MasterDataService.GetSupplierAsync(companyId, id, cancellationToken);
        return supplier is null ? NotFound() : Ok(supplier);
    }

    [HttpPost, Authorize(Policy = "permission:purchases.manage")]
    public async Task<ActionResult<BusinessPartnerDto>> Create(BusinessPartnerRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        var supplier = await MasterDataService.CreateSupplierAsync(companyId, request.ToInput(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { supplier.Id }, supplier);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = "permission:purchases.manage")]
    public async Task<ActionResult<BusinessPartnerDto>> Update(Guid id, BusinessPartnerRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        var supplier = await MasterDataService.UpdateSupplierAsync(companyId, id, request.ToInput(), cancellationToken);
        return supplier is null ? NotFound() : Ok(supplier);
    }

    [HttpPut("{id:guid}/status"), Authorize(Policy = "permission:purchases.manage")]
    public async Task<IActionResult> SetStatus(Guid id, StatusRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        return await MasterDataService.SetSupplierActiveAsync(companyId, id, request.IsActive, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}"), Authorize(Policy = "permission:purchases.manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        return await MasterDataService.DeleteSupplierAsync(companyId, id, cancellationToken) ? NoContent() : NotFound();
    }
}

[ApiController, Route("api/product-categories"), Tags("Product Categories")]
public sealed class ProductCategoriesController(IMasterDataService masterDataService) : MasterDataControllerBase(masterDataService)
{
    [HttpGet, Authorize(Policy = "permission:products.view")]
    public async Task<ActionResult<PagedResult<ProductCategoryDto>>> List([FromQuery] MasterDataListRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId)) return Forbid();
        return Ok(await MasterDataService.GetCategoriesAsync(companyId, request.ToQuery(), cancellationToken));
    }
    [HttpGet("{id:guid}"), Authorize(Policy = "permission:products.view")]
    public async Task<ActionResult<ProductCategoryDto>> Get(Guid id, CancellationToken cancellationToken) { if (!TryGetCompanyId(out var companyId)) return Forbid(); var item = await MasterDataService.GetCategoryAsync(companyId, id, cancellationToken); return item is null ? NotFound() : Ok(item); }
    [HttpPost, Authorize(Policy = "permission:products.manage")]
    public async Task<ActionResult<ProductCategoryDto>> Create(ProductCategoryRequest request, CancellationToken cancellationToken) { if (!TryGetCompanyId(out var companyId)) return Forbid(); var item = await MasterDataService.CreateCategoryAsync(companyId, request.ToInput(), cancellationToken); return CreatedAtAction(nameof(Get), new { item.Id }, item); }
    [HttpPut("{id:guid}"), Authorize(Policy = "permission:products.manage")]
    public async Task<ActionResult<ProductCategoryDto>> Update(Guid id, ProductCategoryRequest request, CancellationToken cancellationToken) { if (!TryGetCompanyId(out var companyId)) return Forbid(); var item = await MasterDataService.UpdateCategoryAsync(companyId, id, request.ToInput(), cancellationToken); return item is null ? NotFound() : Ok(item); }
    [HttpPut("{id:guid}/status"), Authorize(Policy = "permission:products.manage")]
    public async Task<IActionResult> SetStatus(Guid id, StatusRequest request, CancellationToken cancellationToken) { if (!TryGetCompanyId(out var companyId)) return Forbid(); return await MasterDataService.SetCategoryActiveAsync(companyId, id, request.IsActive, cancellationToken) ? NoContent() : NotFound(); }
}

[ApiController, Route("api/units-of-measure"), Tags("Units of Measure")]
public sealed class UnitsOfMeasureController(IMasterDataService masterDataService) : MasterDataControllerBase(masterDataService)
{
    [HttpGet, Authorize(Policy = "permission:products.view")]
    public async Task<ActionResult<PagedResult<UnitOfMeasureDto>>> List([FromQuery] MasterDataListRequest request, CancellationToken cancellationToken) { if (!TryGetCompanyId(out var companyId)) return Forbid(); return Ok(await MasterDataService.GetUnitsAsync(companyId, request.ToQuery(), cancellationToken)); }
    [HttpGet("{id:guid}"), Authorize(Policy = "permission:products.view")]
    public async Task<ActionResult<UnitOfMeasureDto>> Get(Guid id, CancellationToken cancellationToken) { if (!TryGetCompanyId(out var companyId)) return Forbid(); var item = await MasterDataService.GetUnitAsync(companyId, id, cancellationToken); return item is null ? NotFound() : Ok(item); }
    [HttpPost, Authorize(Policy = "permission:products.manage")]
    public async Task<ActionResult<UnitOfMeasureDto>> Create(UnitOfMeasureRequest request, CancellationToken cancellationToken) { if (!TryGetCompanyId(out var companyId)) return Forbid(); var item = await MasterDataService.CreateUnitAsync(companyId, request.ToInput(), cancellationToken); return CreatedAtAction(nameof(Get), new { item.Id }, item); }
    [HttpPut("{id:guid}"), Authorize(Policy = "permission:products.manage")]
    public async Task<ActionResult<UnitOfMeasureDto>> Update(Guid id, UnitOfMeasureRequest request, CancellationToken cancellationToken) { if (!TryGetCompanyId(out var companyId)) return Forbid(); var item = await MasterDataService.UpdateUnitAsync(companyId, id, request.ToInput(), cancellationToken); return item is null ? NotFound() : Ok(item); }
}

[ApiController, Route("api/products"), Tags("Products")]
public sealed class ProductsController(IMasterDataService masterDataService) : MasterDataControllerBase(masterDataService)
{
    [HttpGet, Authorize(Policy = "permission:products.view")]
    public async Task<ActionResult<PagedResult<ProductDto>>> List([FromQuery] MasterDataListRequest request, CancellationToken cancellationToken) { if (!TryGetCompanyId(out var companyId)) return Forbid(); return Ok(await MasterDataService.GetProductsAsync(companyId, request.ToQuery(), cancellationToken)); }
    [HttpGet("{id:guid}"), Authorize(Policy = "permission:products.view")]
    public async Task<ActionResult<ProductDto>> Get(Guid id, CancellationToken cancellationToken) { if (!TryGetCompanyId(out var companyId)) return Forbid(); var item = await MasterDataService.GetProductAsync(companyId, id, cancellationToken); return item is null ? NotFound() : Ok(item); }
    [HttpPost, Authorize(Policy = "permission:products.manage")]
    public async Task<ActionResult<ProductDto>> Create(ProductRequest request, CancellationToken cancellationToken) { if (!TryGetCompanyId(out var companyId)) return Forbid(); var item = await MasterDataService.CreateProductAsync(companyId, request.ToInput(), cancellationToken); return CreatedAtAction(nameof(Get), new { item.Id }, item); }
    [HttpPut("{id:guid}"), Authorize(Policy = "permission:products.manage")]
    public async Task<ActionResult<ProductDto>> Update(Guid id, ProductRequest request, CancellationToken cancellationToken) { if (!TryGetCompanyId(out var companyId)) return Forbid(); var item = await MasterDataService.UpdateProductAsync(companyId, id, request.ToInput(), cancellationToken); return item is null ? NotFound() : Ok(item); }
    [HttpPut("{id:guid}/status"), Authorize(Policy = "permission:products.manage")]
    public async Task<IActionResult> SetStatus(Guid id, StatusRequest request, CancellationToken cancellationToken) { if (!TryGetCompanyId(out var companyId)) return Forbid(); return await MasterDataService.SetProductActiveAsync(companyId, id, request.IsActive, cancellationToken) ? NoContent() : NotFound(); }
}
