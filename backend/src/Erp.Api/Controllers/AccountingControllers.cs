using System.Security.Claims;
using Erp.Api.Contracts.Accounting;
using Erp.Application.Accounting;
using Erp.Application.Authentication;
using Erp.Application.MasterData;
using Erp.Domain.Accounting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

[ApiController, Route("api/accounting/chart-of-accounts"), Tags("Chart of Accounts")]
public sealed class ChartOfAccountsController(IAccountingService accounting) : CompanyScopedControllerBase
{
    [HttpGet, Authorize(Policy = "permission:accounting.view")]
    public async Task<ActionResult<IReadOnlyCollection<AccountDto>>> List([FromQuery] AccountQueryRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await accounting.GetAccountsAsync(companyId, request.Search, request.AccountType, request.IsActive, cancellationToken));
    }

    [HttpPost, Authorize(Policy = "permission:chartofaccounts.manage")]
    public async Task<ActionResult<AccountDto>> Create(AccountRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var account = await accounting.CreateAccountAsync(companyId, request.ToInput(), cancellationToken);
        return CreatedAtAction(nameof(List), new { account.Id }, account);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = "permission:chartofaccounts.manage")]
    public async Task<ActionResult<AccountDto>> Update(Guid id, AccountRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var account = await accounting.UpdateAccountAsync(companyId, id, request.ToInput(), cancellationToken);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpDelete("{id:guid}"), Authorize(Policy = "permission:chartofaccounts.manage")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return await accounting.DeactivateAccountAsync(companyId, id, cancellationToken) ? NoContent() : NotFound();
    }
}

[ApiController, Route("api/accounting"), Tags("Accounting")]
public sealed class AccountingController(IAccountingService accounting) : CompanyScopedControllerBase
{
    [HttpGet("classes"), Authorize(Policy = "permission:accounting.view")]
    public async Task<ActionResult<IReadOnlyCollection<FinancialClassDto>>> Classes([FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await accounting.GetFinancialClassesAsync(companyId, isActive, cancellationToken));
    }

    [HttpPost("classes"), Authorize(Policy = "permission:accountingconfiguration.manage")]
    public async Task<ActionResult<FinancialClassDto>> CreateClass(FinancialClassRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await accounting.CreateFinancialClassAsync(companyId, request.ToInput(), cancellationToken));
    }

    [HttpPut("classes/{id:guid}"), Authorize(Policy = "permission:accountingconfiguration.manage")]
    public async Task<ActionResult<FinancialClassDto>> UpdateClass(Guid id, FinancialClassRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var financialClass = await accounting.UpdateFinancialClassAsync(companyId, id, request.ToInput(), cancellationToken);
        return financialClass is null ? NotFound() : Ok(financialClass);
    }

    [HttpGet("periods"), Authorize(Policy = "permission:accounting.view")]
    public async Task<ActionResult<IReadOnlyCollection<AccountingPeriodDto>>> Periods(CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await accounting.GetPeriodsAsync(companyId, cancellationToken));
    }

    [HttpPost("periods"), Authorize(Policy = "permission:accountingconfiguration.manage")]
    public async Task<ActionResult<AccountingPeriodDto>> CreatePeriod(AccountingPeriodRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await accounting.CreatePeriodAsync(companyId, request.ToInput(), cancellationToken));
    }

    [HttpPut("periods/{id:guid}/status"), Authorize(Policy = "permission:accountingconfiguration.manage")]
    public async Task<ActionResult<AccountingPeriodDto>> SetPeriodStatus(Guid id, PeriodStatusRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var period = await accounting.SetPeriodStatusAsync(companyId, id, request.Status, cancellationToken);
        return period is null ? NotFound() : Ok(period);
    }

    [HttpGet("posting-profiles"), Authorize(Policy = "permission:accounting.view")]
    public async Task<ActionResult<IReadOnlyCollection<PostingProfileDto>>> PostingProfiles(CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await accounting.GetPostingProfilesAsync(companyId, cancellationToken));
    }

    [HttpPost("posting-profiles"), Authorize(Policy = "permission:accountingconfiguration.manage")]
    public async Task<ActionResult<PostingProfileDto>> CreatePostingProfile(PostingProfileRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await accounting.SavePostingProfileAsync(companyId, null, request.ToInput(), cancellationToken));
    }

    [HttpPut("posting-profiles/{id:guid}"), Authorize(Policy = "permission:accountingconfiguration.manage")]
    public async Task<ActionResult<PostingProfileDto>> UpdatePostingProfile(Guid id, PostingProfileRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await accounting.SavePostingProfileAsync(companyId, id, request.ToInput(), cancellationToken));
    }

    [HttpGet("cash-bank-mappings"), Authorize(Policy = "permission:accounting.view")]
    public async Task<ActionResult<IReadOnlyCollection<CashBankGlMappingDto>>> CashBankMappings(CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await accounting.GetCashBankMappingsAsync(companyId, cancellationToken));
    }

    [HttpPut("cash-bank-mappings/{cashBankAccountId:guid}"), Authorize(Policy = "permission:accountingconfiguration.manage")]
    public async Task<ActionResult<CashBankGlMappingDto>> SetCashBankMapping(Guid cashBankAccountId, CashBankMappingRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var mapping = await accounting.SetCashBankMappingAsync(companyId, cashBankAccountId, request.PostingAccountId, cancellationToken);
        return mapping is null ? NotFound() : Ok(mapping);
    }

    [HttpGet("journals"), Authorize(Policy = "permission:journals.view")]
    public async Task<ActionResult<PagedResult<JournalEntryDto>>> Journals([FromQuery] JournalEntryListRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await accounting.GetJournalsAsync(companyId, request.ToQuery(), cancellationToken));
    }

    [HttpGet("journals/{id:guid}"), Authorize(Policy = "permission:journals.view")]
    public async Task<ActionResult<JournalEntryDto>> Journal(Guid id, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var journal = await accounting.GetJournalAsync(companyId, id, cancellationToken);
        return journal is null ? NotFound() : Ok(journal);
    }

    [HttpPost("journals"), Authorize(Policy = "permission:journals.manage")]
    public async Task<ActionResult<JournalEntryDto>> CreateJournal(JournalEntryRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId) || !TryUser(out var userId)) return Forbid();
        var journal = await accounting.CreateManualJournalAsync(companyId, userId, request.ToInput(), cancellationToken);
        return CreatedAtAction(nameof(Journal), new { journal.Id }, journal);
    }

    [HttpPut("journals/{id:guid}"), Authorize(Policy = "permission:journals.manage")]
    public async Task<ActionResult<JournalEntryDto>> UpdateJournal(Guid id, JournalEntryRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var journal = await accounting.UpdateManualJournalAsync(companyId, id, request.ToInput(), cancellationToken);
        return journal is null ? NotFound() : Ok(journal);
    }

    [HttpPost("journals/{id:guid}/post"), Authorize(Policy = "permission:journals.post")]
    public async Task<ActionResult<JournalEntryDto>> PostJournal(Guid id, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId) || !TryUser(out var userId)) return Forbid();
        var journal = await accounting.PostJournalAsync(companyId, id, userId, cancellationToken);
        return journal is null ? NotFound() : Ok(journal);
    }

    [HttpGet("general-ledger"), Authorize(Policy = "permission:accounting.view")]
    public async Task<ActionResult<PagedResult<GeneralLedgerLineDto>>> GeneralLedger([FromQuery] GeneralLedgerListRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await accounting.GetGeneralLedgerAsync(companyId, request.ToQuery(), cancellationToken));
    }

    [HttpGet("pending-transactions"), Authorize(Policy = "permission:accounting.view")]
    public async Task<ActionResult<IReadOnlyCollection<PendingAccountingTransactionDto>>> PendingTransactions(CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await accounting.GetPendingTransactionsAsync(companyId, cancellationToken));
    }

    [HttpPost("pending-transactions/post"), Authorize(Policy = "permission:accountingconfiguration.manage")]
    public async Task<ActionResult<PendingPostingResult>> PostPending(PendingPostingRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId) || !TryUser(out var userId)) return Forbid();
        return Ok(await accounting.PostPendingTransactionsAsync(companyId, userId, request.TransactionIds, cancellationToken));
    }
}
