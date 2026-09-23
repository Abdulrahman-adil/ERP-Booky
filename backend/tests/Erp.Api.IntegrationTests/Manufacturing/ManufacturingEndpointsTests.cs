using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Erp.Api.Contracts.Authentication;
using Erp.Api.Contracts.Inventory;
using Erp.Api.Contracts.Manufacturing;
using Erp.Api.Contracts.MasterData;
using Erp.Api.Contracts.Sales;
using Erp.Application.Inventory;
using Erp.Application.Manufacturing;
using Erp.Application.MasterData;
using Erp.Application.Sales;
using Erp.Domain.Inventory;
using Erp.Domain.Manufacturing;
using Erp.Domain.MasterData;

namespace Erp.Api.IntegrationTests.Manufacturing;

public sealed class ManufacturingEndpointsTests(Authentication.AuthenticationApiFactory factory) : IClassFixture<Authentication.AuthenticationApiFactory>
{
    [Fact]
    public async Task Completion_posts_scaled_material_consumption_finished_goods_and_preserves_sales_issue_flow()
    {
        var client = await CreateAuthenticatedClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var setup = await CreateManufacturingSetupAsync(client, suffix);

        await RecordMovementAsync(client, setup.RawMaterial.Id, setup.RawMaterialsWarehouse.Id, InventoryMovementType.OpeningBalance, 100);
        var bill = await CreateBillAsync(client, setup, 2);
        var order = await CreateOrderAsync(client, setup, bill, 10);

        using var releaseResponse = await client.PostAsync($"/api/manufacturing/production-orders/{order.Id}/release", null);
        releaseResponse.EnsureSuccessStatusCode();

        using var requirementsResponse = await client.GetAsync($"/api/manufacturing/production-orders/{order.Id}/requirements?actualProducedQuantity=10");
        requirementsResponse.EnsureSuccessStatusCode();
        var requirements = await requirementsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<ProductionRequirementDto>>(JsonOptions);
        var requirement = Assert.Single(requirements!);
        Assert.Equal(20, requirement.RequiredQuantity);
        Assert.Equal(100, requirement.AvailableQuantity);
        Assert.True(requirement.IsAvailable);

        using var completionResponse = await client.PostAsJsonAsync($"/api/manufacturing/production-orders/{order.Id}/complete", new ProductionCompletionRequest { ActualProducedQuantity = 10 }, JsonOptions);
        Assert.True(completionResponse.IsSuccessStatusCode, await completionResponse.Content.ReadAsStringAsync());
        var completed = await completionResponse.Content.ReadFromJsonAsync<ProductionOrderDto>(JsonOptions);
        Assert.NotNull(completed);
        Assert.Equal(ProductionOrderStatus.Completed, completed.Status);
        Assert.Equal(10, completed.ActualProducedQuantity);

        await AssertStockAsync(client, setup.RawMaterial.Id, 80);
        await AssertStockAsync(client, setup.FinishedGood.Id, 10);
        await AssertManufacturingLedgerAsync(client, setup.RawMaterial.Id, setup.RawMaterialsWarehouse.Id, InventoryMovementType.ProductionConsumption, 20, completed.ProductionOrderNumber);
        await AssertManufacturingLedgerAsync(client, setup.FinishedGood.Id, setup.FinishedGoodsWarehouse.Id, InventoryMovementType.ProductionReceipt, 10, completed.ProductionOrderNumber);

        var customer = await CreateAsync<BusinessPartnerDto>(client, "/api/customers", new BusinessPartnerRequest { LegalName = $"MFG Customer {suffix}", PaymentTermsDays = 14 });
        var invoice = await CreateAsync<SalesInvoiceDto>(client, "/api/sales-invoices", new SalesInvoiceRequest
        {
            CustomerId = customer.Id,
            WarehouseId = setup.FinishedGoodsWarehouse.Id,
            InvoiceDate = new DateOnly(2026, 9, 8),
            DueDate = new DateOnly(2026, 9, 22),
            Reference = $"MFG-{suffix}",
            Lines = [new SalesInvoiceLineRequest { ProductId = setup.FinishedGood.Id, Quantity = 3, UnitPrice = 25 }]
        }, JsonOptions);
        using var invoicePostResponse = await client.PostAsync($"/api/sales-invoices/{invoice.Id}/post", null);
        invoicePostResponse.EnsureSuccessStatusCode();

        await AssertStockAsync(client, setup.RawMaterial.Id, 80);
        await AssertStockAsync(client, setup.FinishedGood.Id, 7);

        var insufficientOrder = await CreateOrderAsync(client, setup, bill, 50);
        using var releaseInsufficientResponse = await client.PostAsync($"/api/manufacturing/production-orders/{insufficientOrder.Id}/release", null);
        releaseInsufficientResponse.EnsureSuccessStatusCode();
        using var insufficientCompletionResponse = await client.PostAsJsonAsync($"/api/manufacturing/production-orders/{insufficientOrder.Id}/complete", new ProductionCompletionRequest { ActualProducedQuantity = 50 }, JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, insufficientCompletionResponse.StatusCode);

        await AssertStockAsync(client, setup.RawMaterial.Id, 80);
        await AssertStockAsync(client, setup.FinishedGood.Id, 7);
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
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    private static async Task<(ProductDto RawMaterial, ProductDto FinishedGood, WarehouseDto RawMaterialsWarehouse, WarehouseDto FinishedGoodsWarehouse)> CreateManufacturingSetupAsync(HttpClient client, string suffix)
    {
        var unit = await CreateAsync<UnitOfMeasureDto>(client, "/api/units-of-measure", new UnitOfMeasureRequest
        {
            Code = $"MFGU{suffix}",
            Name = $"Manufacturing unit {suffix}",
            Dimension = "Count",
            ConversionFactorToBase = 1,
            DecimalPlaces = 0
        });
        var rawMaterial = await CreateAsync<ProductDto>(client, "/api/products", new ProductRequest
        {
            Name = $"Manufacturing raw material {suffix}",
            ProductType = ProductType.Stock,
            StockUnitOfMeasureId = unit.Id,
            InventoryPurpose = InventoryItemPurpose.RawMaterial
        }, JsonOptions);
        var finishedGood = await CreateAsync<ProductDto>(client, "/api/products", new ProductRequest
        {
            Name = $"Manufacturing finished good {suffix}",
            ProductType = ProductType.Stock,
            StockUnitOfMeasureId = unit.Id,
            InventoryPurpose = InventoryItemPurpose.FinishedGood
        }, JsonOptions);
        var rawMaterialsWarehouse = await CreateAsync<WarehouseDto>(client, "/api/warehouses", new WarehouseRequest { Name = $"Raw materials {suffix}" });
        var finishedGoodsWarehouse = await CreateAsync<WarehouseDto>(client, "/api/warehouses", new WarehouseRequest { Name = $"Finished goods {suffix}" });
        return (rawMaterial, finishedGood, rawMaterialsWarehouse, finishedGoodsWarehouse);
    }

    private static async Task<BillOfMaterialsDto> CreateBillAsync(HttpClient client, (ProductDto RawMaterial, ProductDto FinishedGood, WarehouseDto RawMaterialsWarehouse, WarehouseDto FinishedGoodsWarehouse) setup, decimal materialQuantity) =>
        await CreateAsync<BillOfMaterialsDto>(client, "/api/manufacturing/bills-of-materials", new BillOfMaterialsRequest
        {
            FinishedProductId = setup.FinishedGood.Id,
            OutputQuantity = 1,
            Components = [new BillOfMaterialsComponentRequest { ComponentProductId = setup.RawMaterial.Id, Quantity = materialQuantity }]
        }, JsonOptions);

    private static async Task<ProductionOrderDto> CreateOrderAsync(HttpClient client, (ProductDto RawMaterial, ProductDto FinishedGood, WarehouseDto RawMaterialsWarehouse, WarehouseDto FinishedGoodsWarehouse) setup, BillOfMaterialsDto bill, decimal plannedQuantity) =>
        await CreateAsync<ProductionOrderDto>(client, "/api/manufacturing/production-orders", new ProductionOrderRequest
        {
            FinishedProductId = setup.FinishedGood.Id,
            BillOfMaterialsId = bill.Id,
            PlannedQuantity = plannedQuantity,
            SourceWarehouseId = setup.RawMaterialsWarehouse.Id,
            DestinationWarehouseId = setup.FinishedGoodsWarehouse.Id,
            ProductionDate = new DateOnly(2026, 9, 8)
        }, JsonOptions);

    private static async Task RecordMovementAsync(HttpClient client, Guid productId, Guid warehouseId, InventoryMovementType movementType, decimal quantity)
    {
        using var response = await client.PostAsJsonAsync("/api/inventory/movements", new InventoryMovementRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            MovementType = movementType,
            Quantity = quantity,
            Notes = "Manufacturing test opening stock"
        }, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private static async Task AssertStockAsync(HttpClient client, Guid productId, decimal expectedQuantity)
    {
        using var response = await client.GetAsync($"/api/inventory/products/{productId}");
        response.EnsureSuccessStatusCode();
        var inventory = await response.Content.ReadFromJsonAsync<ProductInventoryDto>(JsonOptions);
        Assert.NotNull(inventory);
        Assert.Equal(expectedQuantity, inventory.TotalQuantityOnHand);
    }

    private static async Task AssertManufacturingLedgerAsync(HttpClient client, Guid productId, Guid warehouseId, InventoryMovementType movementType, decimal expectedQuantity, string productionOrderNumber)
    {
        using var response = await client.GetAsync($"/api/inventory/ledger?productId={productId}&warehouseId={warehouseId}");
        response.EnsureSuccessStatusCode();
        var ledger = await response.Content.ReadFromJsonAsync<PagedResult<StockMovementDto>>(JsonOptions);
        Assert.Contains(ledger!.Items, movement =>
            movement.MovementType == movementType &&
            (movement.QuantityIn == expectedQuantity || movement.QuantityOut == expectedQuantity) &&
            movement.SourceModule == "Manufacturing" &&
            movement.ExternalReference == productionOrderNumber);
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
