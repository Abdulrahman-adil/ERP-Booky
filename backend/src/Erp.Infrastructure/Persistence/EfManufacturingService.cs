using System.Data;
using Erp.Application.Inventory;
using Erp.Application.Manufacturing;
using Erp.Application.MasterData;
using Erp.Domain.Common;
using Erp.Domain.Inventory;
using Erp.Domain.Manufacturing;
using Erp.Domain.MasterData;
using Erp.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Erp.Infrastructure.Persistence;

public sealed class EfManufacturingService(ErpDbContext dbContext, IInventoryService inventoryService) : IManufacturingService
{
    public async Task<PagedResult<BillOfMaterialsDto>> GetBillsOfMaterialsAsync(Guid companyId, BillOfMaterialsQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = query with { PageNumber = Math.Max(1, query.PageNumber), PageSize = Math.Clamp(query.PageSize, 1, 100) };
        var bills = dbContext.BillsOfMaterials.AsNoTracking().Include(bill => bill.Components).Where(bill => bill.CompanyId == companyId);
        if (normalized.FinishedProductId.HasValue) bills = bills.Where(bill => bill.FinishedProductId == normalized.FinishedProductId.Value);
        if (normalized.IsActive.HasValue) bills = bills.Where(bill => bill.IsActive == normalized.IsActive.Value);
        if (normalized.EffectiveOn.HasValue)
        {
            var date = normalized.EffectiveOn.Value;
            bills = bills.Where(bill => (!bill.EffectiveFrom.HasValue || bill.EffectiveFrom <= date) && (!bill.EffectiveTo.HasValue || bill.EffectiveTo >= date));
        }
        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            var term = normalized.Search.Trim().ToLower();
            var productIds = dbContext.Products.Where(product => product.CompanyId == companyId && (product.Sku.ToLower().Contains(term) || product.Name.ToLower().Contains(term))).Select(product => product.Id);
            bills = bills.Where(bill => bill.Code.ToLower().Contains(term) || productIds.Contains(bill.FinishedProductId));
        }

        var totalCount = await bills.CountAsync(cancellationToken);
        var items = await bills.OrderByDescending(bill => bill.IsActive).ThenBy(bill => bill.Code).Skip((normalized.PageNumber - 1) * normalized.PageSize).Take(normalized.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<BillOfMaterialsDto>(await ToBillDtosAsync(items, cancellationToken), normalized.PageNumber, normalized.PageSize, totalCount);
    }

    public async Task<BillOfMaterialsDto?> GetBillOfMaterialsAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var bill = await dbContext.BillsOfMaterials.AsNoTracking().Include(item => item.Components).SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        return bill is null ? null : (await ToBillDtosAsync([bill], cancellationToken)).Single();
    }

    public async Task<BillOfMaterialsDto> CreateBillOfMaterialsAsync(Guid companyId, Guid userId, BillOfMaterialsInput input, CancellationToken cancellationToken = default)
    {
        EnsureUser(userId);
        var context = await ValidateBillInputAsync(companyId, input, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var bill = new BillOfMaterials(companyId, await NextNumberAsync("bill_of_material_code_sequence", "BOM", cancellationToken), input.FinishedProductId, input.OutputQuantity, context.FinishedProduct.StockUnitOfMeasureId!.Value, userId, input.EffectiveFrom, input.EffectiveTo, input.Notes, now);
        bill.ReplaceComponents(CreateBillComponents(input.Components, context.ComponentProducts), now);
        if (!input.IsActive) bill.Update(input.FinishedProductId, input.OutputQuantity, context.FinishedProduct.StockUnitOfMeasureId.Value, input.EffectiveFrom, input.EffectiveTo, input.Notes, false, now);
        dbContext.BillsOfMaterials.Add(bill);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetBillOfMaterialsAsync(companyId, bill.Id, cancellationToken))!;
    }

    public async Task<BillOfMaterialsDto?> UpdateBillOfMaterialsAsync(Guid companyId, Guid id, BillOfMaterialsInput input, CancellationToken cancellationToken = default)
    {
        var bill = await dbContext.BillsOfMaterials.Include(item => item.Components).SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (bill is null) return null;
        var context = await ValidateBillInputAsync(companyId, input, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        dbContext.BillOfMaterialsComponents.RemoveRange(bill.Components.ToArray());
        bill.Update(input.FinishedProductId, input.OutputQuantity, context.FinishedProduct.StockUnitOfMeasureId!.Value, input.EffectiveFrom, input.EffectiveTo, input.Notes, input.IsActive, now);
        bill.ReplaceComponents(CreateBillComponents(input.Components, context.ComponentProducts), now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetBillOfMaterialsAsync(companyId, id, cancellationToken);
    }

    public async Task<PagedResult<ProductionOrderDto>> GetProductionOrdersAsync(Guid companyId, ProductionOrderQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = query with { PageNumber = Math.Max(1, query.PageNumber), PageSize = Math.Clamp(query.PageSize, 1, 100) };
        var orders = dbContext.ProductionOrders.AsNoTracking().Include(order => order.Materials).Where(order => order.CompanyId == companyId);
        if (normalized.FinishedProductId.HasValue) orders = orders.Where(order => order.FinishedProductId == normalized.FinishedProductId.Value);
        if (normalized.Status.HasValue) orders = orders.Where(order => order.Status == normalized.Status.Value);
        if (normalized.FromDate.HasValue) orders = orders.Where(order => order.ProductionDate >= normalized.FromDate.Value);
        if (normalized.ToDate.HasValue) orders = orders.Where(order => order.ProductionDate <= normalized.ToDate.Value);
        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            var term = normalized.Search.Trim().ToLower();
            var productIds = dbContext.Products.Where(product => product.CompanyId == companyId && (product.Sku.ToLower().Contains(term) || product.Name.ToLower().Contains(term))).Select(product => product.Id);
            orders = orders.Where(order => order.ProductionOrderNumber.ToLower().Contains(term) || productIds.Contains(order.FinishedProductId));
        }

        var totalCount = await orders.CountAsync(cancellationToken);
        var items = await orders.OrderByDescending(order => order.ProductionDate).ThenByDescending(order => order.ProductionOrderNumber).Skip((normalized.PageNumber - 1) * normalized.PageSize).Take(normalized.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<ProductionOrderDto>(await ToOrderDtosAsync(items, cancellationToken), normalized.PageNumber, normalized.PageSize, totalCount);
    }

    public async Task<ProductionOrderDto?> GetProductionOrderAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.ProductionOrders.AsNoTracking().Include(item => item.Materials).SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        return order is null ? null : (await ToOrderDtosAsync([order], cancellationToken)).Single();
    }

    public async Task<ProductionOrderDto> CreateProductionOrderAsync(Guid companyId, Guid userId, ProductionOrderInput input, CancellationToken cancellationToken = default)
    {
        EnsureUser(userId);
        var context = await ValidateOrderInputAsync(companyId, input, cancellationToken);
        var order = new ProductionOrder(companyId, await NextNumberAsync("production_order_number_sequence", "MO", cancellationToken), input.FinishedProductId, input.BillOfMaterialsId, input.PlannedQuantity, context.FinishedProduct.StockUnitOfMeasureId!.Value, input.SourceWarehouseId, input.DestinationWarehouseId, input.ProductionDate, userId, input.Notes);
        order.ReplaceMaterials(CreateOrderMaterials(context.Bill));
        dbContext.ProductionOrders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetProductionOrderAsync(companyId, order.Id, cancellationToken))!;
    }

    public async Task<ProductionOrderDto?> UpdateProductionOrderAsync(Guid companyId, Guid id, ProductionOrderInput input, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.ProductionOrders.Include(item => item.Materials).SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (order is null) return null;
        if (order.Status != ProductionOrderStatus.Draft) throw new ManufacturingConflictException("Only draft production orders can be edited.");
        var context = await ValidateOrderInputAsync(companyId, input, cancellationToken);
        dbContext.ProductionOrderMaterials.RemoveRange(order.Materials.ToArray());
        order.UpdateDraft(input.FinishedProductId, input.BillOfMaterialsId, input.PlannedQuantity, context.FinishedProduct.StockUnitOfMeasureId!.Value, input.SourceWarehouseId, input.DestinationWarehouseId, input.ProductionDate, input.Notes);
        order.ReplaceMaterials(CreateOrderMaterials(context.Bill));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetProductionOrderAsync(companyId, id, cancellationToken);
    }

    public async Task<ProductionOrderDto?> ReleaseProductionOrderAsync(Guid companyId, Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        EnsureUser(userId);
        var order = await dbContext.ProductionOrders.Include(item => item.Materials).SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (order is null) return null;
        if (order.Status != ProductionOrderStatus.Draft) throw new ManufacturingConflictException("Only draft production orders can be released.");
        await ValidateOrderSnapshotAsync(companyId, order, cancellationToken);
        order.Release(userId, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetProductionOrderAsync(companyId, id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProductionRequirementDto>> GetRequirementsAsync(Guid companyId, Guid id, decimal actualProducedQuantity, CancellationToken cancellationToken = default)
    {
        if (actualProducedQuantity <= 0) throw new ManufacturingValidationException("Actual produced quantity must be greater than zero.");
        var order = await dbContext.ProductionOrders.AsNoTracking().Include(item => item.Materials).SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (order is null) throw new ManufacturingValidationException("The production order was not found.");
        return await BuildRequirementsAsync(companyId, order, actualProducedQuantity, cancellationToken);
    }

    public async Task<ProductionOrderDto?> CompleteProductionOrderAsync(Guid companyId, Guid id, Guid userId, decimal actualProducedQuantity, CancellationToken cancellationToken = default)
    {
        EnsureUser(userId);
        if (actualProducedQuantity <= 0) throw new ManufacturingValidationException("Actual produced quantity must be greater than zero.");
        if (!dbContext.Database.IsRelational())
        {
            var found = await CompleteCoreAsync(companyId, id, userId, actualProducedQuantity, cancellationToken);
            return found ? await GetProductionOrderAsync(companyId, id, cancellationToken) : null;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var found = await CompleteCoreAsync(companyId, id, userId, actualProducedQuantity, cancellationToken);
            if (!found) return null;
            await transaction.CommitAsync(cancellationToken);
            return await GetProductionOrderAsync(companyId, id, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ManufacturingConflictException("The production order changed while it was completing. Refresh and try again.");
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ManufacturingConflictException("The production order changed while it was completing. Refresh and try again.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<bool> CompleteCoreAsync(Guid companyId, Guid id, Guid userId, decimal actualProducedQuantity, CancellationToken cancellationToken)
    {
        var order = await dbContext.ProductionOrders.Include(item => item.Materials).SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (order is null) return false;
        if (order.Status != ProductionOrderStatus.Released) throw new ManufacturingConflictException("Only released production orders can be completed.");
        await ValidateOrderSnapshotAsync(companyId, order, cancellationToken);
        var requirements = await BuildRequirementsAsync(companyId, order, actualProducedQuantity, cancellationToken);
        if (requirements.Any(requirement => !requirement.IsAvailable))
        {
            var unavailable = requirements.First(requirement => !requirement.IsAvailable);
            throw new ManufacturingValidationException($"Insufficient stock for '{unavailable.ComponentName}' in the selected source warehouse.");
        }

        var occurredAt = new DateTimeOffset(DateTime.SpecifyKind(order.ProductionDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc));
        var sourceReference = new SourceReference("Manufacturing", order.Id, "ProductionOrderCompleted");
        var movements = requirements.Select(requirement => new InventorySystemMovementInput(
            requirement.ComponentProductId,
            order.SourceWarehouseId,
            InventoryMovementType.ProductionConsumption,
            requirement.RequiredQuantity,
            occurredAt,
            sourceReference,
            order.ProductionOrderNumber,
            $"Production consumption for {order.ProductionOrderNumber}"))
            .Append(new InventorySystemMovementInput(
                order.FinishedProductId,
                order.DestinationWarehouseId,
                InventoryMovementType.ProductionReceipt,
                actualProducedQuantity,
                occurredAt,
                sourceReference,
                order.ProductionOrderNumber,
                $"Production receipt for {order.ProductionOrderNumber}"))
            .ToArray();

        await inventoryService.RecordSystemMovementsAsync(companyId, userId, movements, cancellationToken);
        order.Complete(actualProducedQuantity, userId, occurredAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<(Product FinishedProduct, IReadOnlyDictionary<Guid, Product> ComponentProducts)> ValidateBillInputAsync(Guid companyId, BillOfMaterialsInput input, CancellationToken cancellationToken)
    {
        await EnsureCompanyAsync(companyId, cancellationToken);
        if (input.FinishedProductId == Guid.Empty) throw new ManufacturingValidationException("A finished product is required.");
        if (input.OutputQuantity <= 0) throw new ManufacturingValidationException("Output quantity must be greater than zero.");
        if (input.EffectiveFrom.HasValue && input.EffectiveTo.HasValue && input.EffectiveTo < input.EffectiveFrom) throw new ManufacturingValidationException("The effective end date cannot be before the effective start date.");
        if (input.Components is null || input.Components.Count == 0) throw new ManufacturingValidationException("Add at least one material component.");
        if (input.Components.Any(component => component.ComponentProductId == Guid.Empty || component.Quantity <= 0)) throw new ManufacturingValidationException("Every material component requires a product and a positive quantity.");
        if (input.Components.GroupBy(component => component.ComponentProductId).Any(group => group.Count() > 1)) throw new ManufacturingValidationException("A material can appear only once in a bill of materials.");
        if (input.Components.Any(component => component.ComponentProductId == input.FinishedProductId)) throw new ManufacturingValidationException("A finished product cannot be its own component.");

        var productIds = input.Components.Select(component => component.ComponentProductId).Append(input.FinishedProductId).Distinct().ToArray();
        var products = await dbContext.Products.Where(product => product.CompanyId == companyId && productIds.Contains(product.Id)).ToDictionaryAsync(product => product.Id, cancellationToken);
        if (products.Count != productIds.Length) throw new ManufacturingValidationException("One or more selected products were not found.");
        var finishedProduct = products[input.FinishedProductId];
        if (!IsFinishedProduct(finishedProduct)) throw new ManufacturingValidationException("The finished product must be an active stock product with the Finished Good item purpose and a unit of measure.");
        foreach (var component in input.Components.Select(component => products[component.ComponentProductId]))
        {
            if (!IsComponentProduct(component)) throw new ManufacturingValidationException($"Component '{component.Name}' must be an active stock item with a raw-material, packaging, consumable, or semi-finished item purpose.");
        }
        return (finishedProduct, products);
    }

    private async Task<(Product FinishedProduct, BillOfMaterials Bill)> ValidateOrderInputAsync(Guid companyId, ProductionOrderInput input, CancellationToken cancellationToken)
    {
        await EnsureCompanyAsync(companyId, cancellationToken);
        if (input.FinishedProductId == Guid.Empty || input.BillOfMaterialsId == Guid.Empty) throw new ManufacturingValidationException("A finished product and active bill of materials are required.");
        if (input.PlannedQuantity <= 0) throw new ManufacturingValidationException("Planned quantity must be greater than zero.");
        if (input.SourceWarehouseId == Guid.Empty || input.DestinationWarehouseId == Guid.Empty || input.SourceWarehouseId == input.DestinationWarehouseId) throw new ManufacturingValidationException("Select different active source and destination warehouses.");
        var bill = await dbContext.BillsOfMaterials.AsNoTracking().Include(item => item.Components).SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == input.BillOfMaterialsId, cancellationToken)
            ?? throw new ManufacturingValidationException("The selected bill of materials was not found.");
        if (bill.FinishedProductId != input.FinishedProductId || !bill.IsEffectiveOn(input.ProductionDate)) throw new ManufacturingValidationException("The selected bill of materials is not active for the production product and date.");
        var product = await dbContext.Products.AsNoTracking().SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == input.FinishedProductId, cancellationToken)
            ?? throw new ManufacturingValidationException("The finished product was not found.");
        if (!IsFinishedProduct(product) || product.StockUnitOfMeasureId != bill.OutputUnitOfMeasureId) throw new ManufacturingValidationException("The finished product must remain an active Finished Good stock item with the bill of materials unit of measure.");
        var warehouses = await dbContext.Warehouses.AsNoTracking().Where(warehouse => warehouse.CompanyId == companyId && (warehouse.Id == input.SourceWarehouseId || warehouse.Id == input.DestinationWarehouseId)).ToDictionaryAsync(warehouse => warehouse.Id, cancellationToken);
        if (warehouses.Count != 2 || warehouses.Values.Any(warehouse => !warehouse.IsActive)) throw new ManufacturingValidationException("Select active source and destination warehouses for the current company.");
        return (product, bill);
    }

    private async Task ValidateOrderSnapshotAsync(Guid companyId, ProductionOrder order, CancellationToken cancellationToken)
    {
        var productIds = order.Materials.Select(material => material.ComponentProductId).Append(order.FinishedProductId).Distinct().ToArray();
        var products = await dbContext.Products.Where(product => product.CompanyId == companyId && productIds.Contains(product.Id)).ToDictionaryAsync(product => product.Id, cancellationToken);
        if (products.Count != productIds.Length || !IsFinishedProduct(products[order.FinishedProductId])) throw new ManufacturingValidationException("The finished product is unavailable for production.");
        if (products[order.FinishedProductId].StockUnitOfMeasureId != order.OutputUnitOfMeasureId) throw new ManufacturingValidationException("The finished product unit of measure changed after the production order was created.");
        foreach (var material in order.Materials)
        {
            var component = products[material.ComponentProductId];
            if (!IsComponentProduct(component) || component.StockUnitOfMeasureId != material.UnitOfMeasureId) throw new ManufacturingValidationException($"Material '{component.Name}' is unavailable or its unit of measure changed after the production order was created.");
        }
        var warehouses = await dbContext.Warehouses.Where(warehouse => warehouse.CompanyId == companyId && (warehouse.Id == order.SourceWarehouseId || warehouse.Id == order.DestinationWarehouseId)).ToArrayAsync(cancellationToken);
        if (warehouses.Length != 2 || warehouses.Any(warehouse => !warehouse.IsActive)) throw new ManufacturingValidationException("The production order warehouses are unavailable.");
    }

    private async Task<IReadOnlyCollection<ProductionRequirementDto>> BuildRequirementsAsync(Guid companyId, ProductionOrder order, decimal actualProducedQuantity, CancellationToken cancellationToken)
    {
        var productIds = order.Materials.Select(material => material.ComponentProductId).Distinct().ToArray();
        var products = await dbContext.Products.AsNoTracking().Where(product => product.CompanyId == companyId && productIds.Contains(product.Id)).ToDictionaryAsync(product => product.Id, cancellationToken);
        var unitIds = order.Materials.Select(material => material.UnitOfMeasureId).Distinct().ToArray();
        var units = await dbContext.UnitsOfMeasure.AsNoTracking().Where(unit => unitIds.Contains(unit.Id)).ToDictionaryAsync(unit => unit.Id, cancellationToken);
        var available = await dbContext.InventoryBalances.AsNoTracking().Where(balance => balance.CompanyId == companyId && balance.WarehouseId == order.SourceWarehouseId && productIds.Contains(balance.ProductId)).ToDictionaryAsync(balance => balance.ProductId, balance => balance.OnHandQuantity.Amount, cancellationToken);
        return order.Materials.OrderBy(material => material.LineNumber).Select(material =>
        {
            var required = material.QuantityRequiredFor(actualProducedQuantity);
            var availableQuantity = available.GetValueOrDefault(material.ComponentProductId);
            var product = products[material.ComponentProductId];
            return new ProductionRequirementDto(material.ComponentProductId, product.Sku, product.Name, required, availableQuantity, material.UnitOfMeasureId, units[material.UnitOfMeasureId].Name, availableQuantity >= required);
        }).ToArray();
    }

    private static IReadOnlyCollection<BillOfMaterialsComponent> CreateBillComponents(IReadOnlyCollection<BillOfMaterialsComponentInput> components, IReadOnlyDictionary<Guid, Product> products) =>
        components.Select((component, index) => new BillOfMaterialsComponent(index + 1, component.ComponentProductId, component.Quantity, products[component.ComponentProductId].StockUnitOfMeasureId!.Value)).ToArray();

    private static IReadOnlyCollection<ProductionOrderMaterial> CreateOrderMaterials(BillOfMaterials bill) =>
        bill.Components.Select(component => new ProductionOrderMaterial(component.LineNumber, component.ComponentProductId, component.Quantity, bill.OutputQuantity, component.UnitOfMeasureId)).ToArray();

    private async Task<IReadOnlyCollection<BillOfMaterialsDto>> ToBillDtosAsync(IReadOnlyCollection<BillOfMaterials> bills, CancellationToken cancellationToken)
    {
        if (bills.Count == 0) return Array.Empty<BillOfMaterialsDto>();
        var productIds = bills.Select(bill => bill.FinishedProductId).Concat(bills.SelectMany(bill => bill.Components.Select(component => component.ComponentProductId))).Distinct().ToArray();
        var unitIds = bills.Select(bill => bill.OutputUnitOfMeasureId).Concat(bills.SelectMany(bill => bill.Components.Select(component => component.UnitOfMeasureId))).Distinct().ToArray();
        var products = await dbContext.Products.AsNoTracking().Where(product => productIds.Contains(product.Id)).ToDictionaryAsync(product => product.Id, cancellationToken);
        var units = await dbContext.UnitsOfMeasure.AsNoTracking().Where(unit => unitIds.Contains(unit.Id)).ToDictionaryAsync(unit => unit.Id, cancellationToken);
        return bills.Select(bill => new BillOfMaterialsDto(bill.Id, bill.Code, bill.FinishedProductId, products[bill.FinishedProductId].Sku, products[bill.FinishedProductId].Name, bill.OutputQuantity, bill.OutputUnitOfMeasureId, units[bill.OutputUnitOfMeasureId].Name, bill.EffectiveFrom, bill.EffectiveTo, bill.Notes, bill.IsActive, bill.CreatedAt, bill.UpdatedAt, bill.Components.OrderBy(component => component.LineNumber).Select(component => new BillOfMaterialsComponentDto(component.Id, component.LineNumber, component.ComponentProductId, products[component.ComponentProductId].Sku, products[component.ComponentProductId].Name, component.Quantity, component.UnitOfMeasureId, units[component.UnitOfMeasureId].Name)).ToArray())).ToArray();
    }

    private async Task<IReadOnlyCollection<ProductionOrderDto>> ToOrderDtosAsync(IReadOnlyCollection<ProductionOrder> orders, CancellationToken cancellationToken)
    {
        if (orders.Count == 0) return Array.Empty<ProductionOrderDto>();
        var productIds = orders.Select(order => order.FinishedProductId).Concat(orders.SelectMany(order => order.Materials.Select(material => material.ComponentProductId))).Distinct().ToArray();
        var unitIds = orders.Select(order => order.OutputUnitOfMeasureId).Concat(orders.SelectMany(order => order.Materials.Select(material => material.UnitOfMeasureId))).Distinct().ToArray();
        var warehouseIds = orders.Select(order => order.SourceWarehouseId).Concat(orders.Select(order => order.DestinationWarehouseId)).Distinct().ToArray();
        var billIds = orders.Select(order => order.BillOfMaterialsId).Distinct().ToArray();
        var userIds = orders.Select(order => order.CreatedByUserId).Concat(orders.Where(order => order.ReleasedByUserId.HasValue).Select(order => order.ReleasedByUserId!.Value)).Concat(orders.Where(order => order.PostedByUserId.HasValue).Select(order => order.PostedByUserId!.Value)).Distinct().ToArray();
        var products = await dbContext.Products.AsNoTracking().Where(product => productIds.Contains(product.Id)).ToDictionaryAsync(product => product.Id, cancellationToken);
        var units = await dbContext.UnitsOfMeasure.AsNoTracking().Where(unit => unitIds.Contains(unit.Id)).ToDictionaryAsync(unit => unit.Id, cancellationToken);
        var warehouses = await dbContext.Warehouses.AsNoTracking().Where(warehouse => warehouseIds.Contains(warehouse.Id)).ToDictionaryAsync(warehouse => warehouse.Id, cancellationToken);
        var bills = await dbContext.BillsOfMaterials.AsNoTracking().Where(bill => billIds.Contains(bill.Id)).ToDictionaryAsync(bill => bill.Id, cancellationToken);
        var users = await dbContext.Users.AsNoTracking().Where(user => userIds.Contains(user.Id)).ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        return orders.Select(order => new ProductionOrderDto(order.Id, order.ProductionOrderNumber, order.FinishedProductId, products[order.FinishedProductId].Sku, products[order.FinishedProductId].Name, order.BillOfMaterialsId, bills[order.BillOfMaterialsId].Code, order.PlannedQuantity, order.ActualProducedQuantity, order.OutputUnitOfMeasureId, units[order.OutputUnitOfMeasureId].Name, order.SourceWarehouseId, warehouses[order.SourceWarehouseId].Code, warehouses[order.SourceWarehouseId].Name, order.DestinationWarehouseId, warehouses[order.DestinationWarehouseId].Code, warehouses[order.DestinationWarehouseId].Name, order.ProductionDate, order.Status, order.Notes, users.GetValueOrDefault(order.CreatedByUserId, "Unknown user"), order.CreatedAt, order.ReleasedByUserId.HasValue ? users.GetValueOrDefault(order.ReleasedByUserId.Value) : null, order.ReleasedAt, order.PostedByUserId.HasValue ? users.GetValueOrDefault(order.PostedByUserId.Value) : null, order.PostedAt, order.Materials.OrderBy(material => material.LineNumber).Select(material => new ProductionOrderMaterialDto(material.Id, material.LineNumber, material.ComponentProductId, products[material.ComponentProductId].Sku, products[material.ComponentProductId].Name, material.QuantityPerBomOutput, material.BomOutputQuantity, material.UnitOfMeasureId, units[material.UnitOfMeasureId].Name)).ToArray())).ToArray();
    }

    private async Task EnsureCompanyAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Companies.AnyAsync(company => company.Id == companyId && company.IsActive, cancellationToken)) throw new ManufacturingValidationException("The selected company is unavailable.");
    }

    private async Task<string> NextNumberAsync(string sequenceName, string prefix, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            var next = sequenceName == "bill_of_material_code_sequence" ? await dbContext.BillsOfMaterials.CountAsync(cancellationToken) + 1 : await dbContext.ProductionOrders.CountAsync(cancellationToken) + 1;
            return $"{prefix}-{next:D6}";
        }
        var connection = dbContext.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection) await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT nextval('erp.{sequenceName}')";
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            return $"{prefix}-{Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)):D6}";
        }
        finally
        {
            if (closeConnection) await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static bool IsFinishedProduct(Product product) => product.IsActive && product.ProductType == ProductType.Stock && product.StockUnitOfMeasureId.HasValue && product.InventoryPurpose == InventoryItemPurpose.FinishedGood;
    private static bool IsComponentProduct(Product product) => product.IsActive && product.ProductType == ProductType.Stock && product.StockUnitOfMeasureId.HasValue && product.InventoryPurpose is InventoryItemPurpose.RawMaterial or InventoryItemPurpose.PackagingMaterial or InventoryItemPurpose.Consumable or InventoryItemPurpose.SemiFinishedGood;
    private static void EnsureUser(Guid userId) { if (userId == Guid.Empty) throw new ManufacturingValidationException("The authenticated user could not be identified."); }
}
