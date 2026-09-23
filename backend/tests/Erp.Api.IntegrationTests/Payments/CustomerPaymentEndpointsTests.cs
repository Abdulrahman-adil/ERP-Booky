using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Erp.Api.Contracts.Authentication;
using Erp.Api.Contracts.Inventory;
using Erp.Api.Contracts.MasterData;
using Erp.Api.Contracts.Payments;
using Erp.Api.Contracts.Sales;
using Erp.Application.Inventory;
using Erp.Application.MasterData;
using Erp.Application.Payments;
using Erp.Application.Sales;
using Erp.Domain.Accounting;
using Erp.Domain.Inventory;
using Erp.Domain.MasterData;

namespace Erp.Api.IntegrationTests.Payments;

public sealed class CustomerPaymentEndpointsTests(Authentication.AuthenticationApiFactory factory) : IClassFixture<Authentication.AuthenticationApiFactory>
{
    [Fact]
    public async Task Draft_then_partial_and_final_payment_updates_receivable_and_invoice_status()
    {
        var client = await CreateAuthenticatedClientAsync();
        var setup = await CreateSetupAsync(client);
        var invoice = await CreatePostedInvoiceAsync(client, setup);
        var receivable = await GetOpenReceivableAsync(client, setup.Customer.Id);
        var cashBank = await CreateCashBankAccountAsync(client);

        var draft = await CreatePaymentAsync(client, setup.Customer.Id, cashBank.Id, 25, receivable.OpenItemId, 25);
        Assert.Equal("Draft", draft.Status.ToString());
        Assert.Equal(25, draft.AllocatedAmount);
        Assert.Equal(0, draft.UnappliedAmount);
        await AssertInvoiceAsync(client, invoice.Id, 100, "Unpaid");

        using var postResponse = await client.PostAsync($"/api/customer-payments/{draft.Id}/post", null);
        postResponse.EnsureSuccessStatusCode();
        var posted = await postResponse.Content.ReadFromJsonAsync<CustomerPaymentDto>(JsonOptions);
        Assert.Equal("Posted", posted!.Status.ToString());
        Assert.Equal(25, posted.AllocatedAmount);
        await AssertInvoiceAsync(client, invoice.Id, 75, "PartiallyPaid");

        var finalPayment = await CreatePaymentAsync(client, setup.Customer.Id, cashBank.Id, 75, receivable.OpenItemId, 75);
        using var finalPostResponse = await client.PostAsync($"/api/customer-payments/{finalPayment.Id}/post", null);
        finalPostResponse.EnsureSuccessStatusCode();
        await AssertInvoiceAsync(client, invoice.Id, 0, "Paid");

        using var summaryResponse = await client.GetAsync($"/api/customer-payments/invoices/{invoice.Id}/summary");
        var summary = await summaryResponse.Content.ReadFromJsonAsync<InvoicePaymentSummaryDto>(JsonOptions);
        Assert.Equal(100, summary!.OriginalAmount);
        Assert.Equal(100, summary.PaidAmount);
        Assert.Equal(0, summary.OutstandingAmount);
        Assert.Equal(2, summary.AppliedPayments.Count);
    }

    [Fact]
    public async Task Payment_rejects_invalid_destination_overallocation_and_posted_edits()
    {
        var client = await CreateAuthenticatedClientAsync();
        var setup = await CreateSetupAsync(client);
        var invoice = await CreatePostedInvoiceAsync(client, setup);
        var receivable = await GetOpenReceivableAsync(client, setup.Customer.Id);

        using var invalidDestination = await client.PostAsJsonAsync("/api/customer-payments", PaymentRequest(setup.Customer.Id, Guid.NewGuid(), 10, receivable.OpenItemId, 10), JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, invalidDestination.StatusCode);

        var cashBank = await CreateCashBankAccountAsync(client);
        using var overAllocation = await client.PostAsJsonAsync("/api/customer-payments", PaymentRequest(setup.Customer.Id, cashBank.Id, 101, receivable.OpenItemId, 101), JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, overAllocation.StatusCode);

        var payment = await CreatePaymentAsync(client, setup.Customer.Id, cashBank.Id, 10, receivable.OpenItemId, 10);
        using var postResponse = await client.PostAsync($"/api/customer-payments/{payment.Id}/post", null);
        postResponse.EnsureSuccessStatusCode();
        using var updateResponse = await client.PutAsJsonAsync($"/api/customer-payments/{payment.Id}", PaymentRequest(setup.Customer.Id, cashBank.Id, 10, receivable.OpenItemId, 10), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);
        await AssertInvoiceAsync(client, invoice.Id, 90, "PartiallyPaid");
    }

    [Fact]
    public async Task Payment_and_receivable_endpoints_require_authentication()
    {
        using var client = factory.CreateClient();
        using var paymentResponse = await client.GetAsync("/api/customer-payments");
        using var receivableResponse = await client.GetAsync("/api/receivables");
        Assert.Equal(HttpStatusCode.Unauthorized, paymentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, receivableResponse.StatusCode);
    }

    [Fact]
    public async Task Unused_cash_bank_account_can_be_renamed_and_deactivated()
    {
        var client = await CreateAuthenticatedClientAsync();
        var cashBank = await CreateCashBankAccountAsync(client);

        using var response = await client.PutAsJsonAsync($"/api/cash-bank-accounts/{cashBank.Id}", new CashBankAccountUpdateRequest
        {
            Name = "Renamed operating bank",
            IsActive = false
        }, JsonOptions);

        response.EnsureSuccessStatusCode();
        var updated = (await response.Content.ReadFromJsonAsync<CashBankAccountDto>(JsonOptions))!;
        Assert.Equal("Renamed operating bank", updated.Name);
        Assert.False(updated.IsActive);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        await factory.SeedAdministratorAsync();
        var client = factory.CreateClient();
        using var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = Authentication.AuthenticationApiFactory.AdministratorEmail, Password = Authentication.AuthenticationApiFactory.AdministratorPassword });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    private async Task<(BusinessPartnerDto Customer, ProductDto Product, WarehouseDto Warehouse)> CreateSetupAsync(HttpClient client)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var customer = await CreateAsync<BusinessPartnerDto>(client, "/api/customers", new BusinessPartnerRequest { LegalName = $"P8 Customer {suffix}" });
        var unit = await CreateAsync<UnitOfMeasureDto>(client, "/api/units-of-measure", new UnitOfMeasureRequest { Code = $"P8U{suffix}", Name = $"P8 Unit {suffix}", Dimension = "Count", ConversionFactorToBase = 1, DecimalPlaces = 0 });
        var product = await CreateAsync<ProductDto>(client, "/api/products", new ProductRequest { Name = $"P8 Product {suffix}", ProductType = ProductType.Stock, StockUnitOfMeasureId = unit.Id }, JsonOptions);
        var warehouse = await CreateAsync<WarehouseDto>(client, "/api/warehouses", new WarehouseRequest { Name = $"P8 Warehouse {suffix}" });
        using var opening = await client.PostAsJsonAsync("/api/inventory/movements", new InventoryMovementRequest { ProductId = product.Id, WarehouseId = warehouse.Id, MovementType = InventoryMovementType.OpeningBalance, Quantity = 10 }, JsonOptions);
        opening.EnsureSuccessStatusCode();
        return (customer, product, warehouse);
    }

    private async Task<SalesInvoiceDto> CreatePostedInvoiceAsync(HttpClient client, (BusinessPartnerDto Customer, ProductDto Product, WarehouseDto Warehouse) setup)
    {
        using var createResponse = await client.PostAsJsonAsync("/api/sales-invoices", new SalesInvoiceRequest { CustomerId = setup.Customer.Id, WarehouseId = setup.Warehouse.Id, InvoiceDate = new DateOnly(2026, 8, 27), Lines = [new SalesInvoiceLineRequest { ProductId = setup.Product.Id, Quantity = 1, UnitPrice = 100 }] }, JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var invoice = (await createResponse.Content.ReadFromJsonAsync<SalesInvoiceDto>(JsonOptions))!;
        using var postResponse = await client.PostAsync($"/api/sales-invoices/{invoice.Id}/post", null);
        postResponse.EnsureSuccessStatusCode();
        return (await postResponse.Content.ReadFromJsonAsync<SalesInvoiceDto>(JsonOptions))!;
    }

    private async Task<CashBankAccountDto> CreateCashBankAccountAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/cash-bank-accounts", new CashBankAccountRequest { Name = $"P8 Bank {Guid.NewGuid():N}", AccountType = CashBankAccountType.Bank }, JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CashBankAccountDto>(JsonOptions))!;
    }

    private async Task<OpenReceivableDto> GetOpenReceivableAsync(HttpClient client, Guid customerId)
    {
        using var response = await client.GetAsync($"/api/customer-payments/customers/{customerId}/open-receivables");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IReadOnlyCollection<OpenReceivableDto>>(JsonOptions))!.Single();
    }

    private async Task<CustomerPaymentDto> CreatePaymentAsync(HttpClient client, Guid customerId, Guid cashBankId, decimal amount, Guid openItemId, decimal allocation)
    {
        using var response = await client.PostAsJsonAsync("/api/customer-payments", PaymentRequest(customerId, cashBankId, amount, openItemId, allocation), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomerPaymentDto>(JsonOptions))!;
    }

    private static CustomerPaymentRequest PaymentRequest(Guid customerId, Guid cashBankAccountId, decimal amount, Guid openItemId, decimal allocation) => new()
    {
        CustomerId = customerId,
        CashBankAccountId = cashBankAccountId,
        PaymentDate = new DateOnly(2026, 8, 27),
        Amount = amount,
        Allocations = [new PaymentAllocationRequest { OpenItemId = openItemId, Amount = allocation }]
    };

    private async Task AssertInvoiceAsync(HttpClient client, Guid invoiceId, decimal outstanding, string paymentStatus)
    {
        using var response = await client.GetAsync($"/api/sales-invoices/{invoiceId}");
        response.EnsureSuccessStatusCode();
        var invoice = await response.Content.ReadFromJsonAsync<SalesInvoiceDto>(JsonOptions);
        Assert.Equal(outstanding, invoice!.OutstandingAmount);
        Assert.Equal(paymentStatus, invoice.PaymentStatus?.ToString());
    }

    private static async Task<T> CreateAsync<T>(HttpClient client, string path, object request, JsonSerializerOptions? options = null)
    {
        using var response = await client.PostAsJsonAsync(path, request, options);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(options))!;
    }

    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
}
