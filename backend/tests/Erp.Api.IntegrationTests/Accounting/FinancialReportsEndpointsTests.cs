using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Erp.Api.Contracts.Accounting;
using Erp.Api.Contracts.Authentication;
using Erp.Application.Accounting;
using Erp.Application.MasterData;
using Erp.Domain.Accounting;

namespace Erp.Api.IntegrationTests.Accounting;

public sealed class FinancialReportsEndpointsTests(Authentication.AuthenticationApiFactory factory) : IClassFixture<Authentication.AuthenticationApiFactory>
{
    private static int _sequence;

    [Fact]
    public async Task Reports_use_only_posted_ledger_entries_and_export_valid_documents()
    {
        var client = await CreateAuthenticatedClientAsync();
        var setup = await CreateReportSetupAsync(client);
        var posted = await CreateJournalAsync(client, setup.EntryDate, setup.Bank.Id, setup.Sales.Id, 100m, "P11-POSTED");
        using var postResponse = await client.PostAsync($"/api/accounting/journals/{posted.Id}/post", null);
        postResponse.EnsureSuccessStatusCode();
        await CreateJournalAsync(client, setup.EntryDate, setup.Expense.Id, setup.Bank.Id, 75m, "P11-DRAFT");

        var query = $"?fromDate={setup.EntryDate}&toDate={setup.EntryDate}";
        var trialBalance = await GetAsync<TrialBalanceReportDto>(client, $"/api/financial-reports/trial-balance{query}");
        var incomeStatement = await GetAsync<IncomeStatementReportDto>(client, $"/api/financial-reports/income-statement{query}");
        var balanceSheet = await GetAsync<BalanceSheetReportDto>(client, $"/api/financial-reports/balance-sheet{query}");

        Assert.Equal(100m, trialBalance.ClosingDebitTotal);
        Assert.Equal(100m, trialBalance.ClosingCreditTotal);
        Assert.Equal(0m, trialBalance.Difference);
        Assert.Equal(100m, incomeStatement.NetProfit);
        Assert.Equal(100m, balanceSheet.TotalAssets);
        Assert.Equal(100m, balanceSheet.TotalLiabilitiesAndEquity);
        Assert.Equal(0m, balanceSheet.Difference);
        Assert.DoesNotContain(trialBalance.Rows, row => row.Code == setup.Expense.Code && row.ClosingDebit != 0);

        var ledger = await GetAsync<PagedResult<GeneralLedgerLineDto>>(client, $"/api/accounting/general-ledger?accountId={setup.Bank.Id}&fromDate={setup.EntryDate}&toDate={setup.EntryDate}");
        Assert.Single(ledger.Items);
        Assert.Equal(posted.JournalNumber, ledger.Items.Single().JournalNumber);

        using var xlsx = await client.GetAsync($"/api/financial-reports/trial-balance/export{query}&format=Xlsx");
        xlsx.EnsureSuccessStatusCode();
        var xlsxBytes = await xlsx.Content.ReadAsByteArrayAsync();
        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(xlsxBytes, 0, 2));

        using var pdf = await client.GetAsync($"/api/financial-reports/balance-sheet/export{query}&format=Pdf");
        pdf.EnsureSuccessStatusCode();
        var pdfBytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 4));
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

    private static async Task<(AccountDto Bank, AccountDto Sales, AccountDto Expense, string EntryDate)> CreateReportSetupAsync(HttpClient client)
    {
        var suffix = Interlocked.Increment(ref _sequence).ToString("D4");
        var entryDate = new DateOnly(2026, 9, 1).AddDays(_sequence * 2);
        var bank = await CreateAsync<AccountDto>(client, new AccountRequest { Code = $"11{suffix}", Name = "Report test bank", AccountType = AccountType.Asset, AccountRole = AccountRole.Bank, IsPosting = true });
        var sales = await CreateAsync<AccountDto>(client, new AccountRequest { Code = $"41{suffix}", Name = "Report test revenue", AccountType = AccountType.Revenue, AccountRole = AccountRole.SalesRevenue, IsPosting = true });
        var expense = await CreateAsync<AccountDto>(client, new AccountRequest { Code = $"61{suffix}", Name = "Report test expense", AccountType = AccountType.Expense, AccountRole = AccountRole.Expense, IsPosting = true });
        await CreateAsync<AccountDto>(client, new AccountRequest { Code = $"33{suffix}", Name = "Report test current earnings", AccountType = AccountType.Equity, AccountRole = AccountRole.CurrentYearEarnings, IsPosting = true });
        await CreateAsync<AccountingPeriodDto>(client, new AccountingPeriodRequest { Name = $"Report test {suffix}", StartDate = entryDate, EndDate = entryDate, Status = AccountingPeriodStatus.Open });
        return (bank, sales, expense, entryDate.ToString("yyyy-MM-dd"));
    }

    private static async Task<JournalEntryDto> CreateJournalAsync(HttpClient client, string entryDate, Guid debitAccountId, Guid creditAccountId, decimal amount, string reference)
    {
        using var response = await client.PostAsJsonAsync("/api/accounting/journals", new JournalEntryRequest
        {
            EntryDate = DateOnly.Parse(entryDate),
            Description = "Financial reporting test journal",
            Reference = reference,
            Lines = [new JournalEntryLineRequest { AccountId = debitAccountId, Debit = amount, Memo = "Debit line" }, new JournalEntryLineRequest { AccountId = creditAccountId, Credit = amount, Memo = "Credit line" }]
        }, JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JournalEntryDto>(JsonOptions))!;
    }

    private static async Task<T> CreateAsync<T>(HttpClient client, object request)
    {
        var path = request switch { AccountRequest => "/api/accounting/chart-of-accounts", AccountingPeriodRequest => "/api/accounting/periods", _ => throw new ArgumentOutOfRangeException(nameof(request)) };
        using var response = await client.PostAsJsonAsync(path, request, JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
}
