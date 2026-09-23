using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Erp.Api.Contracts.Authentication;
using Erp.Api.Contracts.Accounting;
using Erp.Api.Contracts.Inventory;
using Erp.Api.Contracts.MasterData;
using Erp.Api.Contracts.Sales;
using Erp.Application.Inventory;
using Erp.Application.Accounting;
using Erp.Application.MasterData;
using Erp.Application.Sales;
using Erp.Domain.Accounting;
using Erp.Domain.Inventory;
using Erp.Domain.MasterData;
using Erp.Domain.Sales;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Api.IntegrationTests.Sales;

public sealed class SalesInvoiceEndpointsTests(Authentication.AuthenticationApiFactory factory) : IClassFixture<Authentication.AuthenticationApiFactory>
{
    [Fact]
    public async Task Draft_invoice_has_generated_number_and_does_not_affect_stock_or_receivables()
    {
        var client = await CreateAuthenticatedClientAsync();
        var setup = await CreateSalesSetupAsync(client, 50);

        var invoice = await CreateInvoiceAsync(client, setup, 10, 2000, 10);

        Assert.Equal(SalesInvoiceStatus.Draft, invoice.Status);
        Assert.StartsWith("SI-2026-", invoice.InvoiceNumber);
        Assert.Equal(20_000m, invoice.Subtotal);
        Assert.Equal(2_000m, invoice.DiscountTotal);
        Assert.Equal(18_000m, invoice.Total);
        Assert.Equal(0, invoice.OutstandingAmount);
        Assert.Empty(invoice.Lines.Where(line => line.NetAmount != 18_000m));

        await AssertStockAsync(client, setup.Product.Id, 50);
        await AssertOpenItemCountAsync(invoice.Id, 0);
    }

    [Fact]
    public async Task Posted_invoice_creates_sales_stock_out_receivable_and_customer_history()
    {
        var client = await CreateAuthenticatedClientAsync();
        var setup = await CreateSalesSetupAsync(client, 50);
        var invoice = await CreateInvoiceAsync(client, setup, 10, 2000);

        using var postResponse = await client.PostAsync($"/api/sales-invoices/{invoice.Id}/post", null);
        postResponse.EnsureSuccessStatusCode();
        var posted = await postResponse.Content.ReadFromJsonAsync<SalesInvoiceDto>(JsonOptions);

        Assert.NotNull(posted);
        Assert.Equal(SalesInvoiceStatus.Posted, posted.Status);
        Assert.Equal(SalesInvoicePaymentStatus.Unpaid, posted.PaymentStatus);
        Assert.Equal(20_000m, posted.OutstandingAmount);
        Assert.NotNull(posted.PostedAt);

        await AssertStockAsync(client, setup.Product.Id, 40);
        await AssertOpenItemCountAsync(invoice.Id, 1);

        using var ledgerResponse = await client.GetAsync($"/api/inventory/ledger?productId={setup.Product.Id}&warehouseId={setup.Warehouse.Id}");
        var ledger = await ledgerResponse.Content.ReadFromJsonAsync<PagedResult<StockMovementDto>>(JsonOptions);
        Assert.Contains(ledger!.Items, movement => movement.MovementType == InventoryMovementType.StockOut && movement.ExternalReference == posted.InvoiceNumber && movement.SourceModule == "Sales");

        using var historyResponse = await client.GetAsync($"/api/sales-invoices/customers/{setup.Customer.Id}/summary");
        var summary = await historyResponse.Content.ReadFromJsonAsync<CustomerSalesSummaryDto>(JsonOptions);
        Assert.Equal(20_000m, summary!.TotalPostedSales);
        Assert.Equal(20_000m, summary.OutstandingReceivable);
        Assert.Contains(summary.RecentInvoices, item => item.Id == posted.Id);

        using var updateResponse = await client.PutAsJsonAsync($"/api/sales-invoices/{posted.Id}", CreateRequest(setup, 1, 1), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);
    }

    [Fact]
    public async Task Posted_invoice_with_an_active_profile_creates_a_balanced_sales_journal_and_ledger_lines()
    {
        var client = await CreateAuthenticatedClientAsync();
        var setup = await CreateSalesSetupAsync(client, 50);
        await ConfigureAccountingForOperationalPostingAsync(client);
        var invoice = await CreateInvoiceAsync(client, setup, 10, 2000);

        using var postResponse = await client.PostAsync($"/api/sales-invoices/{invoice.Id}/post", null);
        postResponse.EnsureSuccessStatusCode();

        using var journalsResponse = await client.GetAsync($"/api/accounting/journals?search={Uri.EscapeDataString(invoice.InvoiceNumber)}");
        journalsResponse.EnsureSuccessStatusCode();
        var journals = await journalsResponse.Content.ReadFromJsonAsync<PagedResult<JournalEntryDto>>(JsonOptions);
        var journal = Assert.Single(journals!.Items.Where(item => item.SourceDocumentId == invoice.Id));

        Assert.Equal(JournalEntryStatus.Posted, journal.Status);
        Assert.Equal("Sales", journal.SourceModule);
        Assert.Equal(invoice.Total, journal.TotalDebit);
        Assert.Equal(invoice.Total, journal.TotalCredit);
        Assert.Contains(journal.Lines, line => line.Debit == invoice.Total && line.BusinessPartnerId == setup.Customer.Id);
        Assert.Contains(journal.Lines, line => line.Credit == invoice.Total && line.BusinessPartnerId is null);

        using var ledgerResponse = await client.GetAsync("/api/accounting/general-ledger?sourceModule=Sales");
        ledgerResponse.EnsureSuccessStatusCode();
        var ledger = await ledgerResponse.Content.ReadFromJsonAsync<PagedResult<GeneralLedgerLineDto>>(JsonOptions);
        Assert.Equal(2, ledger!.Items.Count(line => line.JournalNumber == journal.JournalNumber));
    }

    [Fact]
    public async Task Insufficient_stock_rejects_posting_without_partial_effects()
    {
        var client = await CreateAuthenticatedClientAsync();
        var setup = await CreateSalesSetupAsync(client, 5);
        var invoice = await CreateInvoiceAsync(client, setup, 10, 100);

        using var postResponse = await client.PostAsync($"/api/sales-invoices/{invoice.Id}/post", null);
        Assert.Equal(HttpStatusCode.BadRequest, postResponse.StatusCode);

        using var invoiceResponse = await client.GetAsync($"/api/sales-invoices/{invoice.Id}");
        var unchanged = await invoiceResponse.Content.ReadFromJsonAsync<SalesInvoiceDto>(JsonOptions);
        Assert.Equal(SalesInvoiceStatus.Draft, unchanged!.Status);
        await AssertStockAsync(client, setup.Product.Id, 5);
        await AssertOpenItemCountAsync(invoice.Id, 0);
    }

    [Fact]
    public async Task Sales_list_can_filter_by_invoice_number()
    {
        var client = await CreateAuthenticatedClientAsync();
        var setup = await CreateSalesSetupAsync(client, 1);
        var invoice = await CreateInvoiceAsync(client, setup, 1, 100);

        using var response = await client.GetAsync($"/api/sales-invoices?search={Uri.EscapeDataString(invoice.InvoiceNumber)}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<SalesInvoiceDto>>(JsonOptions);

        Assert.Contains(result!.Items, item => item.Id == invoice.Id);
    }

    [Fact]
    public async Task Sales_endpoints_require_authentication()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/sales-invoices");
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

    private async Task<(BusinessPartnerDto Customer, ProductDto Product, WarehouseDto Warehouse)> CreateSalesSetupAsync(HttpClient client, decimal openingQuantity)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var customer = await CreateAsync<BusinessPartnerDto>(client, "/api/customers", new BusinessPartnerRequest { LegalName = $"P7 Customer {suffix}", PaymentTermsDays = 14 });
        var unit = await CreateAsync<UnitOfMeasureDto>(client, "/api/units-of-measure", new UnitOfMeasureRequest { Code = $"P7U{suffix}", Name = $"P7 Unit {suffix}", Dimension = "Count", ConversionFactorToBase = 1, DecimalPlaces = 0 });
        var product = await CreateAsync<ProductDto>(client, "/api/products", new ProductRequest { Name = $"P7 Product {suffix}", ProductType = ProductType.Stock, StockUnitOfMeasureId = unit.Id }, JsonOptions);
        var warehouse = await CreateAsync<WarehouseDto>(client, "/api/warehouses", new WarehouseRequest { Name = $"P7 Warehouse {suffix}" });
        using var openingResponse = await client.PostAsJsonAsync("/api/inventory/movements", new InventoryMovementRequest
        {
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            MovementType = InventoryMovementType.OpeningBalance,
            Quantity = openingQuantity,
            Notes = "P7 automated opening stock"
        }, JsonOptions);
        openingResponse.EnsureSuccessStatusCode();
        return (customer, product, warehouse);
    }

    private static SalesInvoiceRequest CreateRequest((BusinessPartnerDto Customer, ProductDto Product, WarehouseDto Warehouse) setup, decimal quantity, decimal unitPrice, decimal discount = 0) => new()
    {
        CustomerId = setup.Customer.Id,
        WarehouseId = setup.Warehouse.Id,
        InvoiceDate = new DateOnly(2026, 8, 26),
        DueDate = new DateOnly(2026, 9, 9),
        Reference = "P7-TEST",
        Lines = [new SalesInvoiceLineRequest { ProductId = setup.Product.Id, Quantity = quantity, UnitPrice = unitPrice, DiscountPercentage = discount }]
    };

    private static async Task<SalesInvoiceDto> CreateInvoiceAsync(HttpClient client, (BusinessPartnerDto Customer, ProductDto Product, WarehouseDto Warehouse) setup, decimal quantity, decimal unitPrice, decimal discount = 0)
    {
        using var response = await client.PostAsJsonAsync("/api/sales-invoices", CreateRequest(setup, quantity, unitPrice, discount), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SalesInvoiceDto>(JsonOptions))!;
    }

    private static async Task ConfigureAccountingForOperationalPostingAsync(HttpClient client)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var receivable = await CreateAccountingAccountAsync(client, $"11{suffix[..4]}", "Accounts receivable", AccountType.Asset, AccountRole.AccountsReceivable);
        var revenue = await CreateAccountingAccountAsync(client, $"41{suffix[..4]}", "Sales revenue", AccountType.Revenue, AccountRole.SalesRevenue);
        var inventory = await CreateAccountingAccountAsync(client, $"12{suffix[..4]}", "Inventory", AccountType.Asset, AccountRole.Inventory);
        var payable = await CreateAccountingAccountAsync(client, $"21{suffix[..4]}", "Accounts payable", AccountType.Liability, AccountRole.AccountsPayable);

        await CreateAsync<AccountingPeriodDto>(client, "/api/accounting/periods", new AccountingPeriodRequest
        {
            Name = $"P10 {suffix}", StartDate = new DateOnly(2026, 8, 1), EndDate = new DateOnly(2026, 8, 31), Status = AccountingPeriodStatus.Open
        }, JsonOptions);
        await CreateAsync<PostingProfileDto>(client, "/api/accounting/posting-profiles", new PostingProfileRequest
        {
            Name = $"P10 profile {suffix}",
            IsActive = true,
            Mappings =
            [
                new PostingProfileMappingRequest { PostingKey = "SALES_RECEIVABLE", AccountId = receivable.Id },
                new PostingProfileMappingRequest { PostingKey = "SALES_REVENUE", AccountId = revenue.Id },
                new PostingProfileMappingRequest { PostingKey = "CUSTOMER_PAYMENT_RECEIVABLE", AccountId = receivable.Id },
                new PostingProfileMappingRequest { PostingKey = "PURCHASE_INVENTORY", AccountId = inventory.Id },
                new PostingProfileMappingRequest { PostingKey = "PURCHASE_PAYABLE", AccountId = payable.Id },
                new PostingProfileMappingRequest { PostingKey = "SUPPLIER_PAYMENT_PAYABLE", AccountId = payable.Id }
            ]
        }, JsonOptions);
    }

    private static Task<AccountDto> CreateAccountingAccountAsync(HttpClient client, string code, string name, AccountType type, AccountRole role) =>
        CreateAsync<AccountDto>(client, "/api/accounting/chart-of-accounts", new AccountRequest
        {
            Code = code,
            Name = name,
            AccountType = type,
            AccountRole = role,
            IsPosting = true,
            IsActive = true
        }, JsonOptions);

    private async Task AssertStockAsync(HttpClient client, Guid productId, decimal expectedQuantity)
    {
        using var response = await client.GetAsync($"/api/inventory/products/{productId}");
        response.EnsureSuccessStatusCode();
        var inventory = await response.Content.ReadFromJsonAsync<ProductInventoryDto>(JsonOptions);
        Assert.Equal(expectedQuantity, inventory!.TotalQuantityOnHand);
    }

    private async Task AssertOpenItemCountAsync(Guid invoiceId, int expectedCount)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var count = await dbContext.OpenItems.CountAsync(item => item.SourceReference.AggregateId == invoiceId && item.Type == OpenItemType.Receivable);
        Assert.Equal(expectedCount, count);
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
