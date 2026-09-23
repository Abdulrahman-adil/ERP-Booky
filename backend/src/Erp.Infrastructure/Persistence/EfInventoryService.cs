using System.Data;
using Erp.Application.Inventory;
using Erp.Application.MasterData;
using Erp.Domain.Common;
using Erp.Domain.Inventory;
using Erp.Domain.MasterData;
using Erp.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Erp.Infrastructure.Persistence;

public sealed class EfInventoryService(ErpDbContext dbContext) : IInventoryService
{
    public async Task<PagedResult<WarehouseDto>> GetWarehousesAsync(Guid companyId, WarehouseQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(query);
        var warehouses = dbContext.Warehouses.AsNoTracking().Where(warehouse => warehouse.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            var term = normalized.Search.Trim().ToLower();
            warehouses = warehouses.Where(warehouse => warehouse.Code.ToLower().Contains(term) || warehouse.Name.ToLower().Contains(term));
        }

        if (normalized.IsActive.HasValue)
        {
            warehouses = warehouses.Where(warehouse => warehouse.IsActive == normalized.IsActive.Value);
        }

        var totalCount = await warehouses.CountAsync(cancellationToken);
        var ordered = normalized.SortBy?.Equals("name", StringComparison.OrdinalIgnoreCase) == true
            ? (normalized.SortDescending ? warehouses.OrderByDescending(warehouse => warehouse.Name) : warehouses.OrderBy(warehouse => warehouse.Name))
            : (normalized.SortDescending ? warehouses.OrderByDescending(warehouse => warehouse.Code) : warehouses.OrderBy(warehouse => warehouse.Code));
        var items = await ordered
            .Skip((normalized.PageNumber - 1) * normalized.PageSize)
            .Take(normalized.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<WarehouseDto>(items.Select(ToWarehouseDto).ToArray(), normalized.PageNumber, normalized.PageSize, totalCount);
    }

    public async Task<WarehouseDto?> GetWarehouseAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var warehouse = await dbContext.Warehouses.AsNoTracking().SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        return warehouse is null ? null : ToWarehouseDto(warehouse);
    }

    public async Task<WarehouseDto> CreateWarehouseAsync(Guid companyId, WarehouseInput input, CancellationToken cancellationToken = default)
    {
        await EnsureCompanyAsync(companyId, cancellationToken);
        var warehouse = new Warehouse(companyId, await NextNumberAsync("warehouse_code_sequence", "WH", cancellationToken), input.Name, input.Description, CreateAddress(input));
        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToWarehouseDto(warehouse);
    }

    public async Task<WarehouseDto?> UpdateWarehouseAsync(Guid companyId, Guid id, WarehouseInput input, CancellationToken cancellationToken = default)
    {
        var warehouse = await dbContext.Warehouses.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (warehouse is null) return null;
        warehouse.Update(input.Name, input.Description, CreateAddress(input));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToWarehouseDto(warehouse);
    }

    public async Task<bool> SetWarehouseActiveAsync(Guid companyId, Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var warehouse = await dbContext.Warehouses.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (warehouse is null) return false;
        warehouse.SetActive(isActive);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<WarehouseDetailsDto?> GetWarehouseDetailsAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var warehouse = await GetWarehouseAsync(companyId, id, cancellationToken);
        if (warehouse is null) return null;

        var balances = await dbContext.InventoryBalances.AsNoTracking()
            .Where(balance => balance.CompanyId == companyId && balance.WarehouseId == id && balance.OnHandQuantity.Amount > 0)
            .OrderBy(balance => balance.ProductId)
            .ToListAsync(cancellationToken);
        var movements = await dbContext.InventoryTransactions.AsNoTracking()
            .Where(transaction => transaction.CompanyId == companyId && transaction.WarehouseId == id)
            .OrderByDescending(transaction => transaction.OccurredAt)
            .ThenByDescending(transaction => transaction.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        return new WarehouseDetailsDto(
            warehouse,
            await ToBalanceDtosAsync(balances, cancellationToken),
            await ToMovementDtosAsync(movements, cancellationToken));
    }

    public async Task<InventoryOverviewDto> GetOverviewAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var activeWarehouseCount = await dbContext.Warehouses.CountAsync(warehouse => warehouse.CompanyId == companyId && warehouse.IsActive, cancellationToken);
        var stockedProductCount = await dbContext.InventoryBalances
            .Where(balance => balance.CompanyId == companyId && balance.OnHandQuantity.Amount > 0)
            .Select(balance => balance.ProductId)
            .Distinct()
            .CountAsync(cancellationToken);
        var movementCount = await dbContext.InventoryTransactions.CountAsync(transaction => transaction.CompanyId == companyId, cancellationToken);
        var recent = await dbContext.InventoryTransactions.AsNoTracking()
            .Where(transaction => transaction.CompanyId == companyId)
            .OrderByDescending(transaction => transaction.OccurredAt)
            .ThenByDescending(transaction => transaction.CreatedAt)
            .Take(8)
            .ToListAsync(cancellationToken);

        var quantities = await (
            from balance in dbContext.InventoryBalances.AsNoTracking()
            join product in dbContext.Products.AsNoTracking() on balance.ProductId equals product.Id
            where balance.CompanyId == companyId && product.CompanyId == companyId && balance.OnHandQuantity.Amount > 0
            group balance by product.InventoryPurpose into purposeBalances
            select new
            {
                Purpose = purposeBalances.Key,
                Quantity = purposeBalances.Sum(balance => balance.OnHandQuantity.Amount),
                ProductCount = purposeBalances.Select(balance => balance.ProductId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        return new InventoryOverviewDto(
            activeWarehouseCount,
            stockedProductCount,
            movementCount,
            await ToMovementDtosAsync(recent, cancellationToken),
            quantities
                .OrderBy(item => item.Purpose.HasValue ? item.Purpose.Value.ToString() : "Unclassified")
                .Select(item => new InventoryPurposeQuantityDto(item.Purpose?.ToString() ?? "Unclassified", item.Quantity, item.ProductCount))
                .ToArray());
    }

    public async Task<PagedResult<StockMovementDto>> GetLedgerAsync(Guid companyId, StockLedgerQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(query);
        var movements = dbContext.InventoryTransactions.AsNoTracking().Where(transaction => transaction.CompanyId == companyId);

        if (normalized.ProductId.HasValue) movements = movements.Where(transaction => transaction.ProductId == normalized.ProductId.Value);
        if (normalized.WarehouseId.HasValue) movements = movements.Where(transaction => transaction.WarehouseId == normalized.WarehouseId.Value);
        if (normalized.MovementType.HasValue) movements = movements.Where(transaction => transaction.MovementType == normalized.MovementType.Value);
        if (normalized.FromDate.HasValue)
        {
            var from = ToUtcStart(normalized.FromDate.Value);
            movements = movements.Where(transaction => transaction.OccurredAt >= from);
        }
        if (normalized.ToDate.HasValue)
        {
            var until = ToUtcStart(normalized.ToDate.Value.AddDays(1));
            movements = movements.Where(transaction => transaction.OccurredAt < until);
        }
        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            var term = normalized.Search.Trim().ToLower();
            var productIds = dbContext.Products
                .Where(product => product.CompanyId == companyId && (product.Sku.ToLower().Contains(term) || product.Name.ToLower().Contains(term)))
                .Select(product => product.Id);
            movements = movements.Where(transaction =>
                (transaction.MovementNumber != null && transaction.MovementNumber.ToLower().Contains(term)) ||
                (transaction.ExternalReference != null && transaction.ExternalReference.ToLower().Contains(term)) ||
                productIds.Contains(transaction.ProductId));
        }

        var totalCount = await movements.CountAsync(cancellationToken);
        var ordered = normalized.SortDescending
            ? movements.OrderByDescending(transaction => transaction.OccurredAt).ThenByDescending(transaction => transaction.CreatedAt)
            : movements.OrderBy(transaction => transaction.OccurredAt).ThenBy(transaction => transaction.CreatedAt);
        var items = await ordered
            .Skip((normalized.PageNumber - 1) * normalized.PageSize)
            .Take(normalized.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StockMovementDto>(await ToMovementDtosAsync(items, cancellationToken), normalized.PageNumber, normalized.PageSize, totalCount);
    }

    public async Task<StockMovementDto> RecordMovementAsync(Guid companyId, Guid userId, InventoryMovementInput input, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new InventoryValidationException("The authenticated user could not be identified.");
        return await RecordWithBalanceProtectionAsync(companyId, userId, input, null, true, null, cancellationToken);
    }

    public async Task<StockMovementDto> RecordSystemStockOutAsync(Guid companyId, Guid userId, InventorySystemStockOutInput input, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new InventoryValidationException("The authenticated user could not be identified.");
        if (input.SourceReference is null) throw new InventoryValidationException("A source reference is required for a system inventory movement.");

        var manualInput = new InventoryMovementInput(
            input.ProductId,
            input.WarehouseId,
            InventoryMovementType.StockOut,
            input.Quantity,
            input.OccurredAt,
            input.ExternalReference,
            input.Notes);
        return await RecordWithBalanceProtectionAsync(companyId, userId, manualInput, input.SourceReference, false, null, cancellationToken);
    }

    public async Task<StockMovementDto> RecordSystemStockInAsync(Guid companyId, Guid userId, InventorySystemStockInInput input, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new InventoryValidationException("The authenticated user could not be identified.");
        if (input.SourceReference is null) throw new InventoryValidationException("A source reference is required for a system inventory movement.");
        if (input.CostAmount < 0) throw new InventoryValidationException("Inventory receipt cost cannot be negative.");

        var movementInput = new InventoryMovementInput(input.ProductId, input.WarehouseId, InventoryMovementType.StockIn, input.Quantity, input.OccurredAt, input.ExternalReference, input.Notes);
        return await RecordWithBalanceProtectionAsync(companyId, userId, movementInput, input.SourceReference, false, new Money(input.CostAmount, input.CurrencyCode), cancellationToken);
    }

    public async Task<IReadOnlyCollection<StockMovementDto>> RecordSystemMovementsAsync(Guid companyId, Guid userId, IReadOnlyCollection<InventorySystemMovementInput> inputs, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new InventoryValidationException("The authenticated user could not be identified.");
        if (inputs is null || inputs.Count == 0) throw new InventoryValidationException("At least one system inventory movement is required.");
        if (!dbContext.Database.IsRelational()) return await RecordSystemMovementsCoreAsync(companyId, userId, inputs, cancellationToken);

        if (dbContext.Database.CurrentTransaction is not null)
        {
            await AcquireBalanceLocksAsync(companyId, inputs, cancellationToken);
            return await RecordSystemMovementsCoreAsync(companyId, userId, inputs, cancellationToken);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await AcquireBalanceLocksAsync(companyId, inputs, cancellationToken);
            var movements = await RecordSystemMovementsCoreAsync(companyId, userId, inputs, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return movements;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ProductInventoryDto?> GetProductInventoryAsync(Guid companyId, Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products.AsNoTracking().SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == productId, cancellationToken);
        if (product is null) return null;

        var balances = await dbContext.InventoryBalances.AsNoTracking()
            .Where(balance => balance.CompanyId == companyId && balance.ProductId == productId && balance.OnHandQuantity.Amount > 0)
            .OrderBy(balance => balance.WarehouseId)
            .ToListAsync(cancellationToken);
        var recent = await dbContext.InventoryTransactions.AsNoTracking()
            .Where(transaction => transaction.CompanyId == companyId && transaction.ProductId == productId)
            .OrderByDescending(transaction => transaction.OccurredAt)
            .ThenByDescending(transaction => transaction.CreatedAt)
            .Take(8)
            .ToListAsync(cancellationToken);
        var unitName = product.StockUnitOfMeasureId.HasValue
            ? await dbContext.UnitsOfMeasure.Where(unit => unit.Id == product.StockUnitOfMeasureId.Value).Select(unit => unit.Name).SingleOrDefaultAsync(cancellationToken)
            : null;

        return new ProductInventoryDto(
            productId,
            balances.Sum(balance => balance.OnHandQuantity.Amount),
            unitName,
            await ToBalanceDtosAsync(balances, cancellationToken),
            await ToMovementDtosAsync(recent, cancellationToken));
    }

    private async Task<StockMovementDto> RecordWithBalanceProtectionAsync(
        Guid companyId,
        Guid userId,
        InventoryMovementInput input,
        SourceReference? sourceReference,
        bool isManual,
        Money? costAmount,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return await RecordMovementCoreAsync(companyId, userId, input, sourceReference, isManual, costAmount, cancellationToken);
        }

        if (dbContext.Database.CurrentTransaction is not null)
        {
            await AcquireBalanceLockAsync(companyId, input.ProductId, input.WarehouseId, cancellationToken);
            return await RecordMovementCoreAsync(companyId, userId, input, sourceReference, isManual, costAmount, cancellationToken);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await AcquireBalanceLockAsync(companyId, input.ProductId, input.WarehouseId, cancellationToken);
        var result = await RecordMovementCoreAsync(companyId, userId, input, sourceReference, isManual, costAmount, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<StockMovementDto> RecordMovementCoreAsync(
        Guid companyId,
        Guid userId,
        InventoryMovementInput input,
        SourceReference? sourceReference,
        bool isManual,
        Money? costAmount,
        CancellationToken cancellationToken)
    {
        await EnsureCompanyAsync(companyId, cancellationToken);
        ValidateMovement(input, isManual);

        var product = await dbContext.Products.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == input.ProductId, cancellationToken)
            ?? throw new InventoryValidationException("The selected product was not found.");
        if (!product.IsActive || product.ProductType != ProductType.Stock || !product.StockUnitOfMeasureId.HasValue)
        {
            throw new InventoryValidationException("The selected product must be an active stock product with a unit of measure before it can enter inventory.");
        }

        var warehouse = await dbContext.Warehouses.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == input.WarehouseId, cancellationToken)
            ?? throw new InventoryValidationException("The selected warehouse was not found.");
        if (!warehouse.IsActive) throw new InventoryValidationException("Inventory movements cannot be recorded against an inactive warehouse.");

        if (input.MovementType == InventoryMovementType.OpeningBalance && await dbContext.InventoryTransactions.AnyAsync(
                transaction => transaction.CompanyId == companyId && transaction.ProductId == input.ProductId && transaction.WarehouseId == input.WarehouseId,
                cancellationToken))
        {
            throw new InventoryConflictException("Opening stock can only be the first movement for a product in a warehouse.");
        }

        var occurredAt = (input.OccurredAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var signedQuantity = IsInbound(input.MovementType) ? input.Quantity : -input.Quantity;
        var unitOfMeasureId = product.StockUnitOfMeasureId.Value;
        var balance = await dbContext.InventoryBalances.SingleOrDefaultAsync(item =>
            item.CompanyId == companyId && item.ProductId == input.ProductId && item.WarehouseId == input.WarehouseId,
            cancellationToken);

        if (balance is null)
        {
            if (signedQuantity < 0) throw new InventoryValidationException("Stock out or a negative adjustment cannot create a negative stock balance.");
            balance = new InventoryBalance(companyId, input.ProductId, input.WarehouseId, new Quantity(0, unitOfMeasureId), occurredAt);
            dbContext.InventoryBalances.Add(balance);
        }

        if (balance.OnHandQuantity.UnitOfMeasureId != unitOfMeasureId)
        {
            throw new InventoryValidationException("The product unit of measure does not match its existing warehouse balance.");
        }

        if (balance.OnHandQuantity.Amount + signedQuantity < 0)
        {
            throw new InventoryValidationException("This movement would create a negative stock balance.");
        }

        balance.ApplyMovement(new Quantity(signedQuantity, unitOfMeasureId), occurredAt);
        var movementId = Guid.NewGuid();
        var movement = new InventoryTransaction(
            companyId,
            input.ProductId,
            input.WarehouseId,
            await NextNumberAsync("inventory_movement_number_sequence", "INV", cancellationToken),
            input.MovementType,
            new Quantity(signedQuantity, unitOfMeasureId),
            new Quantity(balance.OnHandQuantity.Amount, unitOfMeasureId),
            sourceReference ?? new SourceReference("Inventory", movementId, input.MovementType.ToString()),
            occurredAt,
            userId,
            input.ExternalReference,
            input.Notes,
            DateTimeOffset.UtcNow,
            costAmount,
            movementId);
        dbContext.InventoryTransactions.Add(movement);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await ToMovementDtosAsync([movement], cancellationToken)).Single();
    }

    private async Task<IReadOnlyCollection<StockMovementDto>> RecordSystemMovementsCoreAsync(Guid companyId, Guid userId, IReadOnlyCollection<InventorySystemMovementInput> inputs, CancellationToken cancellationToken)
    {
        var results = new List<StockMovementDto>(inputs.Count);
        foreach (var input in inputs)
        {
            if (input.SourceReference is null) throw new InventoryValidationException("A source reference is required for a system inventory movement.");
            if (input.CostAmount.HasValue && (input.CostAmount.Value < 0 || string.IsNullOrWhiteSpace(input.CurrencyCode)))
            {
                throw new InventoryValidationException("A non-negative cost and currency are required together for inventory receipts.");
            }

            var movementInput = new InventoryMovementInput(input.ProductId, input.WarehouseId, input.MovementType, input.Quantity, input.OccurredAt, input.ExternalReference, input.Notes);
            var costAmount = input.CostAmount.HasValue ? new Money(input.CostAmount.Value, input.CurrencyCode!) : null;
            var sourceReference = new SourceReference(input.SourceReference.Module, input.SourceReference.AggregateId, input.SourceReference.EventType);
            results.Add(await RecordMovementCoreAsync(companyId, userId, movementInput, sourceReference, false, costAmount, cancellationToken));
        }
        return results;
    }

    private async Task<IReadOnlyCollection<StockMovementDto>> ToMovementDtosAsync(IReadOnlyCollection<InventoryTransaction> movements, CancellationToken cancellationToken)
    {
        if (movements.Count == 0) return Array.Empty<StockMovementDto>();

        var productIds = movements.Select(movement => movement.ProductId).Distinct().ToArray();
        var warehouseIds = movements.Select(movement => movement.WarehouseId).Distinct().ToArray();
        var unitIds = movements.Select(movement => movement.Quantity.UnitOfMeasureId).Distinct().ToArray();
        var userIds = movements.Where(movement => movement.CreatedByUserId.HasValue).Select(movement => movement.CreatedByUserId!.Value).Distinct().ToArray();
        var products = await dbContext.Products.AsNoTracking().Where(product => productIds.Contains(product.Id)).ToDictionaryAsync(product => product.Id, cancellationToken);
        var warehouses = await dbContext.Warehouses.AsNoTracking().Where(warehouse => warehouseIds.Contains(warehouse.Id)).ToDictionaryAsync(warehouse => warehouse.Id, cancellationToken);
        var units = await dbContext.UnitsOfMeasure.AsNoTracking().Where(unit => unitIds.Contains(unit.Id)).ToDictionaryAsync(unit => unit.Id, cancellationToken);
        var users = await dbContext.Users.AsNoTracking().Where(user => userIds.Contains(user.Id)).ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);

        return movements.Select(movement =>
        {
            var product = products[movement.ProductId];
            var warehouse = warehouses[movement.WarehouseId];
            var quantity = movement.Quantity.Amount;
            return new StockMovementDto(
                movement.Id,
                movement.MovementNumber ?? "Legacy movement",
                movement.MovementType,
                movement.ProductId,
                product.Name,
                product.Sku,
                movement.WarehouseId,
                warehouse.Name,
                warehouse.Code,
                quantity > 0 ? quantity : 0,
                quantity < 0 ? -quantity : 0,
                movement.BalanceAfterQuantity?.Amount,
                units[movement.Quantity.UnitOfMeasureId].Name,
                movement.OccurredAt,
                movement.ExternalReference,
                movement.Notes,
                movement.CreatedByUserId.HasValue && users.TryGetValue(movement.CreatedByUserId.Value, out var userName) ? userName : "Unknown user",
                movement.CreatedAt,
                movement.SourceReference.Module,
                movement.SourceReference.EventType);
        }).ToArray();
    }

    private async Task<IReadOnlyCollection<StockBalanceDto>> ToBalanceDtosAsync(IReadOnlyCollection<InventoryBalance> balances, CancellationToken cancellationToken)
    {
        if (balances.Count == 0) return Array.Empty<StockBalanceDto>();

        var productIds = balances.Select(balance => balance.ProductId).Distinct().ToArray();
        var warehouseIds = balances.Select(balance => balance.WarehouseId).Distinct().ToArray();
        var unitIds = balances.Select(balance => balance.OnHandQuantity.UnitOfMeasureId).Distinct().ToArray();
        var products = await dbContext.Products.AsNoTracking().Where(product => productIds.Contains(product.Id)).ToDictionaryAsync(product => product.Id, cancellationToken);
        var warehouses = await dbContext.Warehouses.AsNoTracking().Where(warehouse => warehouseIds.Contains(warehouse.Id)).ToDictionaryAsync(warehouse => warehouse.Id, cancellationToken);
        var units = await dbContext.UnitsOfMeasure.AsNoTracking().Where(unit => unitIds.Contains(unit.Id)).ToDictionaryAsync(unit => unit.Id, cancellationToken);

        return balances.Select(balance =>
        {
            var product = products[balance.ProductId];
            var warehouse = warehouses[balance.WarehouseId];
            return new StockBalanceDto(
                balance.ProductId,
                product.Name,
                product.Sku,
                balance.WarehouseId,
                warehouse.Name,
                warehouse.Code,
                balance.OnHandQuantity.Amount,
                units[balance.OnHandQuantity.UnitOfMeasureId].Name,
                product.InventoryPurpose,
                balance.UpdatedAt);
        }).ToArray();
    }

    private async Task EnsureCompanyAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Companies.AnyAsync(company => company.Id == companyId && company.IsActive, cancellationToken))
        {
            throw new InventoryValidationException("The selected company is unavailable.");
        }
    }

    private async Task<string> NextNumberAsync(string sequenceName, string prefix, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            var next = sequenceName switch
            {
                "warehouse_code_sequence" => await dbContext.Warehouses.CountAsync(cancellationToken) + 1,
                "inventory_movement_number_sequence" => await dbContext.InventoryTransactions.CountAsync(cancellationToken) + 1,
                _ => throw new ArgumentOutOfRangeException(nameof(sequenceName), sequenceName, "Unsupported inventory numbering sequence.")
            };
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
            var value = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
            return $"{prefix}-{value:D6}";
        }
        finally
        {
            if (closeConnection) await dbContext.Database.CloseConnectionAsync();
        }
    }

    private async Task AcquireBalanceLockAsync(Guid companyId, Guid productId, Guid warehouseId, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended(@key, 0));";
        var key = command.CreateParameter();
        key.ParameterName = "@key";
        key.Value = $"{companyId:N}:{productId:N}:{warehouseId:N}";
        command.Parameters.Add(key);
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private async Task AcquireBalanceLocksAsync(Guid companyId, IReadOnlyCollection<InventorySystemMovementInput> inputs, CancellationToken cancellationToken)
    {
        foreach (var item in inputs
                     .Select(input => new { input.ProductId, input.WarehouseId })
                     .Distinct()
                     .OrderBy(item => item.ProductId)
                     .ThenBy(item => item.WarehouseId))
        {
            await AcquireBalanceLockAsync(companyId, item.ProductId, item.WarehouseId, cancellationToken);
        }
    }

    private static WarehouseDto ToWarehouseDto(Warehouse warehouse) => new(
        warehouse.Id,
        warehouse.Code,
        warehouse.Name,
        warehouse.Description,
        warehouse.Address?.Line1,
        warehouse.Address?.Line2,
        warehouse.Address?.City,
        warehouse.Address?.PostalCode,
        warehouse.Address?.CountryCode,
        warehouse.IsActive);

    private static Address? CreateAddress(WarehouseInput input)
    {
        if (string.IsNullOrWhiteSpace(input.AddressLine1) && string.IsNullOrWhiteSpace(input.CountryCode)) return null;
        if (string.IsNullOrWhiteSpace(input.AddressLine1) || string.IsNullOrWhiteSpace(input.CountryCode))
        {
            throw new InventoryValidationException("Provide both an address line and country code when recording a warehouse address.");
        }

        return new Address(input.AddressLine1, input.CountryCode, input.AddressLine2, input.City, input.PostalCode);
    }

    private static void ValidateMovement(InventoryMovementInput input, bool isManual)
    {
        if (input.ProductId == Guid.Empty || input.WarehouseId == Guid.Empty) throw new InventoryValidationException("A product and warehouse are required.");
        if (input.Quantity <= 0) throw new InventoryValidationException("Movement quantity must be greater than zero.");
        if (isManual && input.MovementType is not (InventoryMovementType.OpeningBalance or InventoryMovementType.StockIn or InventoryMovementType.StockOut or InventoryMovementType.PositiveAdjustment or InventoryMovementType.NegativeAdjustment))
        {
            throw new InventoryValidationException("The selected inventory movement type is not supported for manual entry.");
        }
        if (!isManual && input.MovementType is not (InventoryMovementType.StockOut or InventoryMovementType.StockIn or InventoryMovementType.ProductionConsumption or InventoryMovementType.ProductionReceipt))
        {
            throw new InventoryValidationException("The selected inventory movement type is not supported for system document posting.");
        }
        if (isManual &&
            (input.MovementType is InventoryMovementType.PositiveAdjustment or InventoryMovementType.NegativeAdjustment) &&
            string.IsNullOrWhiteSpace(input.Notes))
        {
            throw new InventoryValidationException("A reason is required for inventory adjustments.");
        }
    }

    private static bool IsInbound(InventoryMovementType movementType) => movementType is InventoryMovementType.OpeningBalance or InventoryMovementType.StockIn or InventoryMovementType.PositiveAdjustment or InventoryMovementType.ProductionReceipt;
    private static DateTimeOffset ToUtcStart(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    private static WarehouseQuery Normalize(WarehouseQuery query) => query with { PageNumber = Math.Max(query.PageNumber, 1), PageSize = Math.Clamp(query.PageSize, 1, 100) };
    private static StockLedgerQuery Normalize(StockLedgerQuery query) => query with { PageNumber = Math.Max(query.PageNumber, 1), PageSize = Math.Clamp(query.PageSize, 1, 100) };
}
