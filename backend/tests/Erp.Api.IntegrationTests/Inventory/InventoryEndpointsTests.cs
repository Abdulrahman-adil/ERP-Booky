using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Erp.Api.Contracts.Authentication;
using Erp.Api.Contracts.Inventory;
using Erp.Api.Contracts.MasterData;
using Erp.Application.Inventory;
using Erp.Application.MasterData;
using Erp.Domain.Inventory;
using Erp.Domain.MasterData;

namespace Erp.Api.IntegrationTests.Inventory;

public sealed class InventoryEndpointsTests(Authentication.AuthenticationApiFactory factory) : IClassFixture<Authentication.AuthenticationApiFactory>
{
    [Fact]
    public async Task Warehouse_and_stock_ledger_keep_a_consistent_non_negative_balance()
    {
        var client = await CreateAuthenticatedClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var product = await CreateStockProductAsync(client, suffix);

        var warehouse = await CreateAsync<WarehouseDto>(client, "/api/warehouses", new WarehouseRequest { Name = $"P6 Warehouse {suffix}" });
        Assert.StartsWith("WH-", warehouse.Code);

        var opening = await RecordMovementAsync(client, product.Id, warehouse.Id, InventoryMovementType.OpeningBalance, 12);
        Assert.Equal(12, opening.QuantityIn);
        Assert.Equal(12, opening.BalanceAfter);

        var inbound = await RecordMovementAsync(client, product.Id, warehouse.Id, InventoryMovementType.StockIn, 3);
        var outbound = await RecordMovementAsync(client, product.Id, warehouse.Id, InventoryMovementType.StockOut, 4);
        Assert.Equal(3, inbound.QuantityIn);
        Assert.Equal(4, outbound.QuantityOut);
        Assert.Equal(11, outbound.BalanceAfter);

        using var inventoryResponse = await client.GetAsync($"/api/inventory/products/{product.Id}");
        inventoryResponse.EnsureSuccessStatusCode();
        var inventory = await inventoryResponse.Content.ReadFromJsonAsync<ProductInventoryDto>(JsonOptions);
        Assert.NotNull(inventory);
        Assert.Equal(11, inventory.TotalQuantityOnHand);
        Assert.Contains(inventory.WarehouseBalances, balance => balance.WarehouseId == warehouse.Id && balance.QuantityOnHand == 11);

        using var duplicateOpeningResponse = await client.PostAsJsonAsync("/api/inventory/movements", new InventoryMovementRequest
        {
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            MovementType = InventoryMovementType.OpeningBalance,
            Quantity = 1
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, duplicateOpeningResponse.StatusCode);

        using var invalidOutboundResponse = await client.PostAsJsonAsync("/api/inventory/movements", new InventoryMovementRequest
        {
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            MovementType = InventoryMovementType.StockOut,
            Quantity = 12
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, invalidOutboundResponse.StatusCode);
    }

    [Fact]
    public async Task Adjustments_require_a_reason_and_inventory_requires_a_stock_unit()
    {
        var client = await CreateAuthenticatedClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var warehouse = await CreateAsync<WarehouseDto>(client, "/api/warehouses", new WarehouseRequest { Name = $"P6 Adjustment {suffix}" });
        var unconfiguredProduct = await CreateAsync<ProductDto>(client, "/api/products", new ProductRequest { Name = $"P6 Product {suffix}", ProductType = ProductType.Stock }, JsonOptions);

        using var missingUnitResponse = await client.PostAsJsonAsync("/api/inventory/movements", new InventoryMovementRequest
        {
            ProductId = unconfiguredProduct.Id,
            WarehouseId = warehouse.Id,
            MovementType = InventoryMovementType.OpeningBalance,
            Quantity = 1
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, missingUnitResponse.StatusCode);

        var configuredProduct = await CreateStockProductAsync(client, $"ADJ{suffix}");
        await RecordMovementAsync(client, configuredProduct.Id, warehouse.Id, InventoryMovementType.OpeningBalance, 5);

        using var noReasonResponse = await client.PostAsJsonAsync("/api/inventory/movements", new InventoryMovementRequest
        {
            ProductId = configuredProduct.Id,
            WarehouseId = warehouse.Id,
            MovementType = InventoryMovementType.PositiveAdjustment,
            Quantity = 1
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, noReasonResponse.StatusCode);

        var adjustment = await RecordMovementAsync(client, configuredProduct.Id, warehouse.Id, InventoryMovementType.NegativeAdjustment, 2, "Count correction");
        Assert.Equal(2, adjustment.QuantityOut);
        Assert.Equal(3, adjustment.BalanceAfter);
    }

    [Fact]
    public async Task Inventory_endpoints_require_authentication()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/inventory/overview");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        await factory.SeedAdministratorAsync();
        var client = factory.CreateClient();
        using var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = Authentication.AuthenticationApiFactory.AdministratorEmail,
            Password = Authentication.AuthenticationApiFactory.AdministratorPassword
        });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    private static async Task<ProductDto> CreateStockProductAsync(HttpClient client, string suffix)
    {
        var unit = await CreateAsync<UnitOfMeasureDto>(client, "/api/units-of-measure", new UnitOfMeasureRequest
        {
            Code = $"P6U{suffix}",
            Name = $"P6 Unit {suffix}",
            Dimension = "Count",
            ConversionFactorToBase = 1,
            DecimalPlaces = 0
        });
        var product = await CreateAsync<ProductDto>(client, "/api/products", new ProductRequest
        {
            Name = $"P6 Stock Product {suffix}",
            ProductType = ProductType.Stock,
            StockUnitOfMeasureId = unit.Id
        }, JsonOptions);
        return product;
    }

    private static async Task<StockMovementDto> RecordMovementAsync(HttpClient client, Guid productId, Guid warehouseId, InventoryMovementType movementType, decimal quantity, string? notes = null)
    {
        using var response = await client.PostAsJsonAsync("/api/inventory/movements", new InventoryMovementRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            MovementType = movementType,
            Quantity = quantity,
            Notes = notes
        }, JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StockMovementDto>(JsonOptions))!;
    }

    private static async Task<T> CreateAsync<T>(HttpClient client, string path, object request, JsonSerializerOptions? options = null)
    {
        using var response = await client.PostAsJsonAsync(path, request, options);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(options))!;
    }

    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
