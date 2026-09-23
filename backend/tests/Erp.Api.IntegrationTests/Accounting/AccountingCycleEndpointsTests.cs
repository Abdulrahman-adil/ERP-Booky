using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Erp.Api.Contracts.Accounting;
using Erp.Api.Contracts.Authentication;
using Erp.Api.Contracts.Inventory;
using Erp.Api.Contracts.MasterData;
using Erp.Api.Contracts.Payments;
using Erp.Api.Contracts.Purchasing;
using Erp.Api.Contracts.Sales;
using Erp.Application.Accounting;
using Erp.Application.Inventory;
using Erp.Application.MasterData;
using Erp.Application.Payments;
using Erp.Application.Purchasing;
using Erp.Application.Sales;
using Erp.Domain.Accounting;
using Erp.Domain.Inventory;
using Erp.Domain.MasterData;
using Erp.Domain.Payments;

namespace Erp.Api.IntegrationTests.Accounting;

public sealed class AccountingCycleEndpointsTests(Authentication.AuthenticationApiFactory factory) : IClassFixture<Authentication.AuthenticationApiFactory>
{
    [Fact]
    public async Task Posted_operational_cycle_reconciles_open_items_ledger_and_financial_reports()
    {
        var client = await CreateAuthenticatedClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var date = new DateOnly(2030, 1, 15);
        var accounts = await CreateAccountsAsync(client, suffix);
        await CreateAsync<AccountingPeriodDto>(client, "/api/accounting/periods", new AccountingPeriodRequest { Name = $"Cycle {suffix}", StartDate = date, EndDate = date, Status = AccountingPeriodStatus.Open }, JsonOptions);
        await CreateAsync<PostingProfileDto>(client, "/api/accounting/posting-profiles", new PostingProfileRequest
        {
            Name = $"Cycle profile {suffix}",
            IsActive = true,
            Mappings =
            [
                new PostingProfileMappingRequest { PostingKey = "SALES_RECEIVABLE", AccountId = accounts.Receivable.Id },
                new PostingProfileMappingRequest { PostingKey = "SALES_REVENUE", AccountId = accounts.Revenue.Id },
                new PostingProfileMappingRequest { PostingKey = "CUSTOMER_PAYMENT_RECEIVABLE", AccountId = accounts.Receivable.Id },
                new PostingProfileMappingRequest { PostingKey = "PURCHASE_INVENTORY", AccountId = accounts.Inventory.Id },
                new PostingProfileMappingRequest { PostingKey = "PURCHASE_PAYABLE", AccountId = accounts.Payable.Id },
                new PostingProfileMappingRequest { PostingKey = "SUPPLIER_PAYMENT_PAYABLE", AccountId = accounts.Payable.Id }
            ]
        });

        var customer = await CreateAsync<BusinessPartnerDto>(client, "/api/customers", new BusinessPartnerRequest { LegalName = $"Cycle customer {suffix}" });
        var supplier = await CreateAsync<BusinessPartnerDto>(client, "/api/suppliers", new BusinessPartnerRequest { LegalName = $"Cycle supplier {suffix}" });
        var unit = await CreateAsync<UnitOfMeasureDto>(client, "/api/units-of-measure", new UnitOfMeasureRequest { Code = $"CU{suffix[..5]}", Name = $"Cycle unit {suffix}", Dimension = "Count", ConversionFactorToBase = 1, DecimalPlaces = 0 });
        var product = await CreateAsync<ProductDto>(client, "/api/products", new ProductRequest { Name = $"Cycle product {suffix}", ProductType = ProductType.Stock, StockUnitOfMeasureId = unit.Id }, JsonOptions);
        var warehouse = await CreateAsync<WarehouseDto>(client, "/api/warehouses", new WarehouseRequest { Name = $"Cycle warehouse {suffix}" });
        await CreateAsync<StockMovementDto>(client, "/api/inventory/movements", new InventoryMovementRequest { ProductId = product.Id, WarehouseId = warehouse.Id, MovementType = InventoryMovementType.OpeningBalance, Quantity = 20, Notes = "Isolated accounting-cycle stock" }, JsonOptions);
        var bank = await CreateAsync<CashBankAccountDto>(client, "/api/cash-bank-accounts", new CashBankAccountRequest { Name = $"Cycle bank {suffix}", AccountType = CashBankAccountType.Bank, PostingAccountId = accounts.Bank.Id }, JsonOptions);

        var sale = await CreateAsync<SalesInvoiceDto>(client, "/api/sales-invoices", new SalesInvoiceRequest { CustomerId = customer.Id, WarehouseId = warehouse.Id, InvoiceDate = date, DueDate = date, Lines = [new SalesInvoiceLineRequest { ProductId = product.Id, Quantity = 10, UnitPrice = 100 }] }, JsonOptions);
        await PostAsync(client, $"/api/sales-invoices/{sale.Id}/post");
        var receivable = await GetAsync<PagedResult<OpenReceivableDto>>(client, $"/api/receivables?customerId={customer.Id}&isOpen=true");
        var receivableItem = Assert.Single(receivable.Items);
        var partiallyAllocatedCustomerPayment = await CreateAsync<CustomerPaymentDto>(client, "/api/customer-payments", new CustomerPaymentRequest { CustomerId = customer.Id, CashBankAccountId = bank.Id, PaymentDate = date, Amount = 400, Allocations = [new PaymentAllocationRequest { OpenItemId = receivableItem.OpenItemId, Amount = 200 }] }, JsonOptions);
        await PostExpectStatusAsync(client, $"/api/customer-payments/{partiallyAllocatedCustomerPayment.Id}/post", HttpStatusCode.BadRequest);
        var customerPayment = await CreateAsync<CustomerPaymentDto>(client, "/api/customer-payments", new CustomerPaymentRequest { CustomerId = customer.Id, CashBankAccountId = bank.Id, PaymentDate = date, Amount = 400, Allocations = [new PaymentAllocationRequest { OpenItemId = receivableItem.OpenItemId, Amount = 400 }] }, JsonOptions);
        await PostAsync(client, $"/api/customer-payments/{customerPayment.Id}/post");

        var bill = await CreateAsync<PurchaseInvoiceDto>(client, "/api/purchase-bills", new PurchaseInvoiceRequest { SupplierId = supplier.Id, WarehouseId = warehouse.Id, InvoiceDate = date, DueDate = date, Lines = [new PurchaseInvoiceLineRequest { ProductId = product.Id, Quantity = 10, UnitCost = 50 }] }, JsonOptions);
        await PostAsync(client, $"/api/purchase-bills/{bill.Id}/post");
        var payable = await GetAsync<PagedResult<SupplierPayableDto>>(client, $"/api/payables?supplierId={supplier.Id}&isOpen=true");
        var payableItem = Assert.Single(payable.Items);
        var partiallyAllocatedSupplierPayment = await CreateAsync<SupplierPaymentDto>(client, "/api/supplier-payments", new SupplierPaymentRequest { SupplierId = supplier.Id, CashBankAccountId = bank.Id, PaymentDate = date, Amount = 200, Allocations = [new SupplierPaymentAllocationRequest { OpenItemId = payableItem.OpenItemId, Amount = 100 }] }, JsonOptions);
        await PostExpectStatusAsync(client, $"/api/supplier-payments/{partiallyAllocatedSupplierPayment.Id}/post", HttpStatusCode.BadRequest);
        var supplierPayment = await CreateAsync<SupplierPaymentDto>(client, "/api/supplier-payments", new SupplierPaymentRequest { SupplierId = supplier.Id, CashBankAccountId = bank.Id, PaymentDate = date, Amount = 200, Allocations = [new SupplierPaymentAllocationRequest { OpenItemId = payableItem.OpenItemId, Amount = 200 }] }, JsonOptions);
        await PostAsync(client, $"/api/supplier-payments/{supplierPayment.Id}/post");

        var remainingReceivable = await GetAsync<PagedResult<OpenReceivableDto>>(client, $"/api/receivables?customerId={customer.Id}&isOpen=true");
        var remainingPayable = await GetAsync<PagedResult<SupplierPayableDto>>(client, $"/api/payables?supplierId={supplier.Id}&isOpen=true");
        Assert.Equal(600m, Assert.Single(remainingReceivable.Items).OutstandingAmount);
        Assert.Equal(300m, Assert.Single(remainingPayable.Items).OutstandingAmount);

        var saleJournal = await GetAsync<PagedResult<JournalEntryDto>>(client, $"/api/accounting/journals?search={Uri.EscapeDataString(sale.InvoiceNumber)}");
        var postedSaleJournal = Assert.Single(saleJournal.Items.Where(item => item.SourceDocumentId == sale.Id));
        Assert.Equal(JournalEntryStatus.Posted, postedSaleJournal.Status);
        Assert.Equal(1_000m, postedSaleJournal.TotalDebit);
        Assert.Equal(1_000m, postedSaleJournal.TotalCredit);

        var query = $"?fromDate={date:yyyy-MM-dd}&toDate={date:yyyy-MM-dd}";
        var ledger = await GetAsync<PagedResult<GeneralLedgerLineDto>>(client, $"/api/accounting/general-ledger{query}&pageSize=50");
        Assert.Equal(8, ledger.Items.Count);
        Assert.Contains(ledger.Items, item => item.SourceDocumentId == sale.Id && item.AccountId == accounts.Receivable.Id && item.Debit == 1_000m);
        Assert.Contains(ledger.Items, item => item.SourceDocumentId == supplierPayment.Id && item.AccountId == accounts.Bank.Id && item.Credit == 200m);

        var trialBalance = await GetAsync<TrialBalanceReportDto>(client, $"/api/financial-reports/trial-balance{query}");
        var incomeStatement = await GetAsync<IncomeStatementReportDto>(client, $"/api/financial-reports/income-statement{query}");
        var balanceSheet = await GetAsync<BalanceSheetReportDto>(client, $"/api/financial-reports/balance-sheet{query}");
        Assert.Equal(trialBalance.ClosingDebitTotal, trialBalance.ClosingCreditTotal);
        Assert.Equal(0m, trialBalance.Difference);
        Assert.Equal(1_000m, incomeStatement.Revenue.Total);
        Assert.Equal(0m, incomeStatement.OperatingExpenses.Total);
        Assert.Equal(1_000m, incomeStatement.NetProfit);
        Assert.Equal(1_300m, balanceSheet.TotalAssets);
        Assert.Equal(1_300m, balanceSheet.TotalLiabilitiesAndEquity);
        Assert.Equal(0m, balanceSheet.Difference);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        await factory.SeedAdministratorAsync();
        var client = factory.CreateClient();
        using var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = Authentication.AuthenticationApiFactory.AdministratorEmail, Password = Authentication.AuthenticationApiFactory.AdministratorPassword });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    private static async Task<(AccountDto Bank, AccountDto Receivable, AccountDto Revenue, AccountDto Inventory, AccountDto Payable)> CreateAccountsAsync(HttpClient client, string suffix)
    {
        var bank = await CreateAccountAsync(client, $"11{suffix[..4]}", "Cycle bank", AccountType.Asset, AccountRole.Bank);
        var receivable = await CreateAccountAsync(client, $"12{suffix[..4]}", "Cycle receivable", AccountType.Asset, AccountRole.AccountsReceivable);
        var inventory = await CreateAccountAsync(client, $"13{suffix[..4]}", "Cycle inventory", AccountType.Asset, AccountRole.Inventory);
        var payable = await CreateAccountAsync(client, $"21{suffix[..4]}", "Cycle payable", AccountType.Liability, AccountRole.AccountsPayable);
        var revenue = await CreateAccountAsync(client, $"41{suffix[..4]}", "Cycle revenue", AccountType.Revenue, AccountRole.SalesRevenue);
        await CreateAccountAsync(client, $"31{suffix[..4]}", "Cycle current-year earnings", AccountType.Equity, AccountRole.CurrentYearEarnings);
        return (bank, receivable, revenue, inventory, payable);
    }

    private static Task<AccountDto> CreateAccountAsync(HttpClient client, string code, string name, AccountType type, AccountRole role) =>
        CreateAsync<AccountDto>(client, "/api/accounting/chart-of-accounts", new AccountRequest { Code = code, Name = name, AccountType = type, AccountRole = role, IsPosting = true, IsActive = true }, JsonOptions);

    private static async Task<T> CreateAsync<T>(HttpClient client, string path, object request, JsonSerializerOptions? options = null)
    {
        using var response = await client.PostAsJsonAsync(path, request, options);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(options))!;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private static async Task PostAsync(HttpClient client, string path)
    {
        using var response = await client.PostAsync(path, null);
        response.EnsureSuccessStatusCode();
    }

    private static async Task PostExpectStatusAsync(HttpClient client, string path, HttpStatusCode expectedStatus)
    {
        using var response = await client.PostAsync(path, null);
        Assert.Equal(expectedStatus, response.StatusCode);
    }

    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
}
