using System.Net;
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

public sealed class AccountingJournalEndpointsTests(Authentication.AuthenticationApiFactory factory) : IClassFixture<Authentication.AuthenticationApiFactory>
{
    private static int _periodSequence;

    [Fact]
    public async Task Balanced_draft_posts_to_two_immutable_general_ledger_lines()
    {
        var client = await CreateAuthenticatedClientAsync();
        var setup = await CreateJournalSetupAsync(client, AccountingPeriodStatus.Open);
        var request = CreateJournalRequest(setup.Bank.Id, setup.Expense.Id, setup.EntryDate, "P10-RABT-JOURNAL-POST", 200m, 200m);

        var journal = await CreateJournalAsync(client, request);

        Assert.Equal(JournalEntryStatus.Draft, journal.Status);
        Assert.Equal(200m, journal.TotalDebit);
        Assert.Equal(200m, journal.TotalCredit);

        using var postResponse = await client.PostAsync($"/api/accounting/journals/{journal.Id}/post", null);
        postResponse.EnsureSuccessStatusCode();
        var posted = await postResponse.Content.ReadFromJsonAsync<JournalEntryDto>(JsonOptions);

        Assert.NotNull(posted);
        Assert.Equal(JournalEntryStatus.Posted, posted.Status);

        using var ledgerResponse = await client.GetAsync("/api/accounting/general-ledger");
        ledgerResponse.EnsureSuccessStatusCode();
        var ledger = await ledgerResponse.Content.ReadFromJsonAsync<PagedResult<GeneralLedgerLineDto>>(JsonOptions);

        Assert.Equal(2, ledger!.Items.Count(line => line.JournalNumber == posted.JournalNumber));

        using var updateResponse = await client.PutAsJsonAsync($"/api/accounting/journals/{posted.Id}", request, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);
    }

    [Fact]
    public async Task Unbalanced_journal_is_rejected_without_creating_a_draft()
    {
        var client = await CreateAuthenticatedClientAsync();
        var setup = await CreateJournalSetupAsync(client, AccountingPeriodStatus.Open);
        var request = CreateJournalRequest(setup.Bank.Id, setup.Expense.Id, setup.EntryDate, "P10-RABT-JOURNAL-UNBALANCED", 200m, 190m);

        using var createResponse = await client.PostAsJsonAsync("/api/accounting/journals", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);

        using var journalsResponse = await client.GetAsync("/api/accounting/journals?search=P10-RABT-JOURNAL-UNBALANCED");
        journalsResponse.EnsureSuccessStatusCode();
        var journals = await journalsResponse.Content.ReadFromJsonAsync<PagedResult<JournalEntryDto>>(JsonOptions);

        Assert.Empty(journals!.Items);
    }

    [Fact]
    public async Task Journal_search_matches_a_journal_document_number()
    {
        var client = await CreateAuthenticatedClientAsync();
        var setup = await CreateJournalSetupAsync(client, AccountingPeriodStatus.Open);
        var journal = await CreateJournalAsync(client, CreateJournalRequest(setup.Bank.Id, setup.Expense.Id, setup.EntryDate, "P10-RABT-JOURNAL-SEARCH", 200m, 200m));

        using var response = await client.GetAsync($"/api/accounting/journals?search={Uri.EscapeDataString(journal.JournalNumber)}");

        response.EnsureSuccessStatusCode();
        var journals = await response.Content.ReadFromJsonAsync<PagedResult<JournalEntryDto>>(JsonOptions);
        Assert.Contains(journals!.Items, item => item.Id == journal.Id);
    }

    [Fact]
    public async Task Closed_period_journal_can_not_be_posted()
    {
        var client = await CreateAuthenticatedClientAsync();
        var setup = await CreateJournalSetupAsync(client, AccountingPeriodStatus.Closed);
        var journal = await CreateJournalAsync(client, CreateJournalRequest(setup.Bank.Id, setup.Expense.Id, setup.EntryDate, "P10-RABT-JOURNAL-CLOSED", 200m, 200m));

        using var postResponse = await client.PostAsync($"/api/accounting/journals/{journal.Id}/post", null);

        Assert.Equal(HttpStatusCode.Conflict, postResponse.StatusCode);
        var problem = await postResponse.Content.ReadFromJsonAsync<ApiProblem>();
        Assert.Contains("not open", problem!.Title, StringComparison.OrdinalIgnoreCase);
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
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    private static async Task<(AccountDto Bank, AccountDto Expense, DateOnly EntryDate)> CreateJournalSetupAsync(HttpClient client, AccountingPeriodStatus periodStatus)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var periodStart = new DateOnly(2026, 8, 1).AddDays(Interlocked.Increment(ref _periodSequence) * 2);
        var bank = await CreateAsync<AccountDto>(client, "/api/accounting/chart-of-accounts", new AccountRequest
        {
            Code = $"11{suffix[..4]}",
            Name = "Test bank",
            AccountType = AccountType.Asset,
            AccountRole = AccountRole.Bank,
            IsPosting = true,
            IsActive = true
        });
        var expense = await CreateAsync<AccountDto>(client, "/api/accounting/chart-of-accounts", new AccountRequest
        {
            Code = $"64{suffix[..4]}",
            Name = "Test expense",
            AccountType = AccountType.Expense,
            AccountRole = AccountRole.Expense,
            IsPosting = true,
            IsActive = true
        });
        await CreateAsync<AccountingPeriodDto>(client, "/api/accounting/periods", new AccountingPeriodRequest
        {
            Name = $"P10 test {suffix}",
            StartDate = periodStart,
            EndDate = periodStart.AddDays(1),
            Status = periodStatus
        });

        return (bank, expense, periodStart);
    }

    private static JournalEntryRequest CreateJournalRequest(Guid bankId, Guid expenseId, DateOnly entryDate, string reference, decimal debit, decimal credit) => new()
    {
        EntryDate = entryDate,
        Description = "Office expenses journal",
        Reference = reference,
        Lines =
        [
            new JournalEntryLineRequest { AccountId = expenseId, Debit = debit, Credit = 0, Memo = "Office expenses" },
            new JournalEntryLineRequest { AccountId = bankId, Debit = 0, Credit = credit, Memo = "Bank payment" }
        ]
    };

    private static async Task<JournalEntryDto> CreateJournalAsync(HttpClient client, JournalEntryRequest request)
    {
        using var response = await client.PostAsJsonAsync("/api/accounting/journals", request, JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JournalEntryDto>(JsonOptions))!;
    }

    private static async Task<T> CreateAsync<T>(HttpClient client, string path, object request)
    {
        using var response = await client.PostAsJsonAsync(path, request, JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private sealed record ApiProblem(string Title);

    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
