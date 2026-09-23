using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Erp.Api.Contracts.Authentication;
using Erp.Api.Contracts.Inventory;
using Erp.Api.Contracts.MasterData;
using Erp.Api.Contracts.Payments;
using Erp.Api.Contracts.Purchasing;
using Erp.Application.Inventory;
using Erp.Application.MasterData;
using Erp.Application.Payments;
using Erp.Application.Purchasing;
using Erp.Domain.Accounting;
using Erp.Domain.MasterData;

namespace Erp.Api.IntegrationTests.Purchasing;

public sealed class PurchasingEndpointsTests(Authentication.AuthenticationApiFactory factory) : IClassFixture<Authentication.AuthenticationApiFactory>
{
    [Fact]
    public async Task Draft_post_and_supplier_payments_complete_the_purchase_to_payable_cycle()
    {
        var client = await CreateClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var supplier = await CreateAsync<BusinessPartnerDto>(client, "/api/suppliers", new BusinessPartnerRequest { LegalName = $"P9 Supplier {suffix}" });
        var unit = await CreateAsync<UnitOfMeasureDto>(client, "/api/units-of-measure", new UnitOfMeasureRequest { Code = $"P9U{suffix}", Name = $"P9 Unit {suffix}", Dimension = "Count", ConversionFactorToBase = 1, DecimalPlaces = 0 });
        var product = await CreateAsync<ProductDto>(client, "/api/products", new ProductRequest { Name = $"P9 Product {suffix}", ProductType = ProductType.Stock, StockUnitOfMeasureId = unit.Id }, JsonOptions);
        var warehouse = await CreateAsync<WarehouseDto>(client, "/api/warehouses", new WarehouseRequest { Name = $"P9 Warehouse {suffix}" });
        var deletedDraft = await CreateBillAsync(client, supplier.Id, product.Id, warehouse.Id);
        using (var deleteDraft = await client.DeleteAsync($"/api/purchase-bills/{deletedDraft.Id}"))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleteDraft.StatusCode);
        }
        using (var deletedDraftLookup = await client.GetAsync($"/api/purchase-bills/{deletedDraft.Id}"))
        {
            Assert.Equal(HttpStatusCode.NotFound, deletedDraftLookup.StatusCode);
        }

        var draft = await CreateBillAsync(client, supplier.Id, product.Id, warehouse.Id);

        using (var beforePost = await client.GetAsync($"/api/payables?supplierId={supplier.Id}"))
        {
            var page = await beforePost.Content.ReadFromJsonAsync<PagedResult<SupplierPayableDto>>(JsonOptions);
            Assert.Empty(page!.Items);
        }

        using (var post = await client.PostAsync($"/api/purchase-bills/{draft.Id}/post", null))
        {
            post.EnsureSuccessStatusCode();
        }
        using (var duplicatePost = await client.PostAsync($"/api/purchase-bills/{draft.Id}/post", null))
        {
            Assert.Equal(HttpStatusCode.Conflict, duplicatePost.StatusCode);
        }
        using (var deletePosted = await client.DeleteAsync($"/api/purchase-bills/{draft.Id}"))
        {
            Assert.Equal(HttpStatusCode.Conflict, deletePosted.StatusCode);
        }

        var payable = await GetPayableAsync(client, supplier.Id);
        Assert.Equal(10_000, payable.OriginalAmount);
        Assert.Equal(10_000, payable.OutstandingAmount);
        var bank = await CreateAsync<CashBankAccountDto>(client, "/api/cash-bank-accounts", new CashBankAccountRequest { Name = $"P9 Bank {suffix}", AccountType = CashBankAccountType.Bank }, JsonOptions);
        using (var overAllocated = await client.PostAsJsonAsync("/api/supplier-payments", PaymentRequest(supplier.Id, bank.Id, 10_001, payable.OpenItemId, 10_001), JsonOptions))
        {
            Assert.Equal(HttpStatusCode.BadRequest, overAllocated.StatusCode);
        }

        var first = await CreatePaymentAsync(client, supplier.Id, bank.Id, payable.OpenItemId, 4_000);
        using (var postFirst = await client.PostAsync($"/api/supplier-payments/{first.Id}/post", null)) postFirst.EnsureSuccessStatusCode();
        await AssertBillAsync(client, draft.Id, 6_000, "PartiallyPaid");
        var second = await CreatePaymentAsync(client, supplier.Id, bank.Id, payable.OpenItemId, 6_000);
        using (var postSecond = await client.PostAsync($"/api/supplier-payments/{second.Id}/post", null)) postSecond.EnsureSuccessStatusCode();
        await AssertBillAsync(client, draft.Id, 0, "Paid");
    }

    private async Task<HttpClient> CreateClientAsync()
    {
        await factory.SeedAdministratorAsync();
        var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = Authentication.AuthenticationApiFactory.AdministratorEmail, Password = Authentication.AuthenticationApiFactory.AdministratorPassword });
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    private static async Task<T> CreateAsync<T>(HttpClient client, string path, object request, JsonSerializerOptions? options = null)
    {
        using var response = await client.PostAsJsonAsync(path, request, options);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(options))!;
    }

    private static async Task<PurchaseInvoiceDto> CreateBillAsync(HttpClient client, Guid supplierId, Guid productId, Guid warehouseId)
    {
        using var response = await client.PostAsJsonAsync("/api/purchase-bills", new PurchaseInvoiceRequest { SupplierId = supplierId, WarehouseId = warehouseId, InvoiceDate = new DateOnly(2026, 8, 28), SupplierReference = "P9-TEST", Lines = [new PurchaseInvoiceLineRequest { ProductId = productId, Quantity = 20, UnitCost = 500 }] }, JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PurchaseInvoiceDto>(JsonOptions))!;
    }

    private static async Task<SupplierPayableDto> GetPayableAsync(HttpClient client, Guid supplierId)
    {
        using var response = await client.GetAsync($"/api/payables?supplierId={supplierId}&isOpen=true");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedResult<SupplierPayableDto>>(JsonOptions))!.Items.Single();
    }

    private static async Task<SupplierPaymentDto> CreatePaymentAsync(HttpClient client, Guid supplierId, Guid bankId, Guid payableId, decimal amount)
    {
        using var response = await client.PostAsJsonAsync("/api/supplier-payments", PaymentRequest(supplierId, bankId, amount, payableId, amount), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SupplierPaymentDto>(JsonOptions))!;
    }

    private static SupplierPaymentRequest PaymentRequest(Guid supplierId, Guid bankId, decimal amount, Guid payableId, decimal allocation) => new()
    {
        SupplierId = supplierId,
        CashBankAccountId = bankId,
        PaymentDate = new DateOnly(2026, 8, 28),
        Amount = amount,
        Allocations = [new SupplierPaymentAllocationRequest { OpenItemId = payableId, Amount = allocation }]
    };

    private static async Task AssertBillAsync(HttpClient client, Guid billId, decimal outstanding, string paymentStatus)
    {
        using var response = await client.GetAsync($"/api/purchase-bills/{billId}");
        response.EnsureSuccessStatusCode();
        var bill = await response.Content.ReadFromJsonAsync<PurchaseInvoiceDto>(JsonOptions);
        Assert.Equal(outstanding, bill!.OutstandingAmount);
        Assert.Equal(paymentStatus, bill.PaymentStatus?.ToString());
    }

    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
}
