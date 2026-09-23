using System.Data;
using Erp.Application.MasterData;
using Erp.Application.Accounting;
using Erp.Application.Payments;
using Erp.Domain.Accounting;
using Erp.Domain.Common;
using Erp.Domain.MasterData;
using Erp.Domain.Organizations;
using Erp.Domain.Payments;
using Erp.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Erp.Infrastructure.Persistence;

public sealed class EfPaymentService(ErpDbContext dbContext, IAccountingService accountingService) : IPaymentService
{
    public async Task<PagedResult<CustomerPaymentDto>> GetPaymentsAsync(Guid companyId, CustomerPaymentQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(query);
        var payments = dbContext.Payments.AsNoTracking().Include(payment => payment.Allocations)
            .Where(payment => payment.CompanyId == companyId && payment.Direction == PaymentDirection.Incoming);

        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            var term = normalized.Search.Trim().ToLowerInvariant();
            var customerIds = dbContext.BusinessPartners.Where(customer => customer.CompanyId == companyId &&
                    (customer.Code.ToLower().Contains(term) || customer.LegalName.ToLower().Contains(term)))
                .Select(customer => customer.Id);
            var matchingPaymentIds = dbContext.Database.IsNpgsql()
                ? dbContext.Payments.FromSqlInterpolated($"""SELECT * FROM erp.payments WHERE "CompanyId" = {companyId} AND document_number ILIKE {"%" + term + "%"}""").Select(payment => payment.Id)
                : dbContext.Payments.Where(payment => payment.CompanyId == companyId && payment.DocumentNumber.Value.ToLower().Contains(term)).Select(payment => payment.Id);
            payments = payments.Where(payment => matchingPaymentIds.Contains(payment.Id) || customerIds.Contains(payment.BusinessPartnerId));
        }
        if (normalized.CustomerId.HasValue) payments = payments.Where(payment => payment.BusinessPartnerId == normalized.CustomerId.Value);
        if (normalized.Status.HasValue) payments = payments.Where(payment => payment.Status == normalized.Status.Value);
        if (normalized.CashBankAccountId.HasValue) payments = payments.Where(payment => payment.CashBankAccountId == normalized.CashBankAccountId.Value);
        if (normalized.FromDate.HasValue) payments = payments.Where(payment => payment.PaymentDate >= normalized.FromDate.Value);
        if (normalized.ToDate.HasValue) payments = payments.Where(payment => payment.PaymentDate <= normalized.ToDate.Value);

        var totalCount = await payments.CountAsync(cancellationToken);
        var ordered = normalized.SortBy?.Equals("number", StringComparison.OrdinalIgnoreCase) == true
            ? (normalized.SortDescending ? payments.OrderByDescending(payment => payment.DocumentNumber) : payments.OrderBy(payment => payment.DocumentNumber))
            : normalized.SortBy?.Equals("amount", StringComparison.OrdinalIgnoreCase) == true
                ? (normalized.SortDescending ? payments.OrderByDescending(payment => payment.Amount.Amount) : payments.OrderBy(payment => payment.Amount.Amount))
                : (normalized.SortDescending ? payments.OrderByDescending(payment => payment.PaymentDate).ThenByDescending(payment => payment.DocumentNumber) : payments.OrderBy(payment => payment.PaymentDate).ThenBy(payment => payment.DocumentNumber));
        var items = await ordered.Skip((normalized.PageNumber - 1) * normalized.PageSize).Take(normalized.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<CustomerPaymentDto>(await ToPaymentDtosAsync(items, cancellationToken), normalized.PageNumber, normalized.PageSize, totalCount);
    }

    public async Task<CustomerPaymentDto?> GetPaymentAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var payment = await dbContext.Payments.AsNoTracking().Include(item => item.Allocations)
            .SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id && item.Direction == PaymentDirection.Incoming, cancellationToken);
        return payment is null ? null : (await ToPaymentDtosAsync([payment], cancellationToken)).Single();
    }

    public async Task<CustomerPaymentDto> CreateDraftAsync(Guid companyId, Guid userId, CustomerPaymentInput input, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new PaymentValidationException("The authenticated user could not be identified.");
        var context = await ValidateDraftInputAsync(companyId, input, cancellationToken);
        var payment = new Payment(
            companyId,
            input.CustomerId,
            input.CashBankAccountId,
            PaymentDirection.Incoming,
            new DocumentNumber(await NextPaymentNumberAsync(input.PaymentDate, cancellationToken)),
            new Money(input.Amount, context.Company.BaseCurrencyCode),
            input.PaymentDate,
            input.ExternalReference,
            input.Notes,
            userId,
            DateTimeOffset.UtcNow);
        payment.ReplaceAllocations(CreateAllocations(payment.Id, input.Allocations, context.Company.BaseCurrencyCode));
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetPaymentAsync(companyId, payment.Id, cancellationToken))!;
    }

    public async Task<CustomerPaymentDto?> UpdateDraftAsync(Guid companyId, Guid id, CustomerPaymentInput input, CancellationToken cancellationToken = default)
    {
        var payment = await dbContext.Payments.Include(item => item.Allocations)
            .SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id && item.Direction == PaymentDirection.Incoming, cancellationToken);
        if (payment is null) return null;
        if (payment.Status != PaymentStatus.Draft) throw new PaymentConflictException("Posted payments cannot be edited. Use a future reversal or correction workflow.");

        var context = await ValidateDraftInputAsync(companyId, input, cancellationToken);
        dbContext.PaymentAllocations.RemoveRange(payment.Allocations.ToArray());
        payment.UpdateDraft(input.CustomerId, input.CashBankAccountId, new Money(input.Amount, context.Company.BaseCurrencyCode), input.PaymentDate, input.ExternalReference, input.Notes);
        payment.ReplaceAllocations(CreateAllocations(payment.Id, input.Allocations, context.Company.BaseCurrencyCode));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetPaymentAsync(companyId, id, cancellationToken);
    }

    public async Task<CustomerPaymentDto?> PostAsync(Guid companyId, Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new PaymentValidationException("The authenticated user could not be identified.");
        if (!dbContext.Database.IsRelational())
        {
            var found = await PostCoreAsync(companyId, id, userId, cancellationToken);
            return found ? await GetPaymentAsync(companyId, id, cancellationToken) : null;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var found = await PostCoreAsync(companyId, id, userId, cancellationToken);
            if (!found) return null;
            await transaction.CommitAsync(cancellationToken);
            return await GetPaymentAsync(companyId, id, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new PaymentConflictException("The receivable changed while this payment was posting. Refresh the payment and try again.");
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new PaymentConflictException("The receivable changed while this payment was posting. Refresh the payment and try again.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyCollection<OpenReceivableDto>> GetCustomerOpenReceivablesAsync(Guid companyId, Guid customerId, CancellationToken cancellationToken = default)
    {
        var items = await dbContext.OpenItems.AsNoTracking().Where(item => item.CompanyId == companyId && item.BusinessPartnerId == customerId && item.Type == OpenItemType.Receivable && item.SettledAmount.Amount < item.OriginalAmount.Amount)
            .OrderBy(item => item.DueDate).ThenBy(item => item.DocumentNumber).ToListAsync(cancellationToken);
        return await ToReceivableDtosAsync(items, cancellationToken);
    }

    public async Task<PagedResult<OpenReceivableDto>> GetReceivablesAsync(Guid companyId, ReceivableQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(query);
        var items = dbContext.OpenItems.AsNoTracking().Where(item => item.CompanyId == companyId && item.Type == OpenItemType.Receivable);
        if (normalized.CustomerId.HasValue) items = items.Where(item => item.BusinessPartnerId == normalized.CustomerId.Value);
        if (normalized.IsOpen.HasValue) items = normalized.IsOpen.Value ? items.Where(item => item.SettledAmount.Amount < item.OriginalAmount.Amount) : items.Where(item => item.SettledAmount.Amount == item.OriginalAmount.Amount);
        if (normalized.IsOverdue == true) items = items.Where(item => item.DueDate.HasValue && item.DueDate.Value < DateOnly.FromDateTime(DateTime.UtcNow) && item.SettledAmount.Amount < item.OriginalAmount.Amount);
        if (normalized.FromDate.HasValue) items = items.Where(item => item.DueDate.HasValue && item.DueDate.Value >= normalized.FromDate.Value);
        if (normalized.ToDate.HasValue) items = items.Where(item => item.DueDate.HasValue && item.DueDate.Value <= normalized.ToDate.Value);
        var totalCount = await items.CountAsync(cancellationToken);
        var page = await items.OrderBy(item => item.DueDate).ThenBy(item => item.DocumentNumber).Skip((normalized.PageNumber - 1) * normalized.PageSize).Take(normalized.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<OpenReceivableDto>(await ToReceivableDtosAsync(page, cancellationToken), normalized.PageNumber, normalized.PageSize, totalCount);
    }

    public async Task<InvoicePaymentSummaryDto?> GetInvoicePaymentSummaryAsync(Guid companyId, Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await dbContext.SalesInvoices.AsNoTracking().Include(item => item.Lines).SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == invoiceId && item.Status == SalesInvoiceStatus.Posted, cancellationToken);
        if (invoice is null) return null;
        var openItem = await dbContext.OpenItems.AsNoTracking().SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Type == OpenItemType.Receivable && item.SourceReference.AggregateId == invoiceId, cancellationToken);
        if (openItem is null) return new InvoicePaymentSummaryDto(invoiceId, null, invoice.TotalAmount.Amount, 0, invoice.TotalAmount.Amount, invoice.CurrencyCode, "Unpaid", []);
        var paymentIds = await dbContext.PaymentAllocations.AsNoTracking().Where(allocation => allocation.OpenItemId == openItem.Id).Select(allocation => allocation.PaymentId).Distinct().ToArrayAsync(cancellationToken);
        var payments = paymentIds.Length == 0 ? [] : await dbContext.Payments.AsNoTracking().Include(payment => payment.Allocations).Where(payment => paymentIds.Contains(payment.Id) && payment.Status == PaymentStatus.Posted).OrderByDescending(payment => payment.PaymentDate).ToListAsync(cancellationToken);
        var outstanding = openItem.OutstandingAmount.Amount;
        var status = outstanding == 0 ? "Paid" : outstanding < openItem.OriginalAmount.Amount ? "PartiallyPaid" : "Unpaid";
        return new InvoicePaymentSummaryDto(invoiceId, openItem.Id, openItem.OriginalAmount.Amount, openItem.SettledAmount.Amount, outstanding, openItem.OriginalAmount.CurrencyCode, status, await ToPaymentDtosAsync(payments, cancellationToken));
    }

    public async Task<IReadOnlyCollection<CashBankAccountDto>> GetCashBankAccountsAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await dbContext.CashBankAccounts.AsNoTracking().Where(account => account.CompanyId == companyId).OrderBy(account => account.Name)
            .Select(account => new CashBankAccountDto(account.Id, account.Name, account.AccountType, account.PostingAccountId, account.IsActive)).ToListAsync(cancellationToken);

    public async Task<CashBankAccountDto> CreateCashBankAccountAsync(Guid companyId, CashBankAccountInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) throw new PaymentValidationException("A cash or bank account name is required.");
        if (!await dbContext.Companies.AnyAsync(company => company.Id == companyId && company.IsActive, cancellationToken)) throw new PaymentValidationException("The selected company is unavailable.");
        if (input.PostingAccountId.HasValue && !await dbContext.Accounts.AnyAsync(account => account.Id == input.PostingAccountId.Value, cancellationToken)) throw new PaymentValidationException("The optional future posting account was not found.");
        var account = new CashBankAccount(companyId, input.Name, input.AccountType, input.PostingAccountId);
        dbContext.CashBankAccounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CashBankAccountDto(account.Id, account.Name, account.AccountType, account.PostingAccountId, account.IsActive);
    }

    public async Task<CashBankAccountDto?> UpdateCashBankAccountAsync(Guid companyId, Guid id, CashBankAccountUpdateInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) throw new PaymentValidationException("A cash or bank account name is required.");
        var account = await dbContext.CashBankAccounts.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (account is null) return null;

        if (!input.IsActive && account.IsActive && await dbContext.Payments.AnyAsync(payment => payment.CompanyId == companyId && payment.CashBankAccountId == id, cancellationToken))
        {
            throw new PaymentConflictException("A cash or bank account with payment history cannot be deactivated.");
        }

        account.Rename(input.Name);
        account.SetActive(input.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CashBankAccountDto(account.Id, account.Name, account.AccountType, account.PostingAccountId, account.IsActive);
    }

    private async Task<bool> PostCoreAsync(Guid companyId, Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments.Include(item => item.Allocations).SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id && item.Direction == PaymentDirection.Incoming, cancellationToken);
        if (payment is null) return false;
        if (payment.Status != PaymentStatus.Draft) throw new PaymentConflictException("Only draft payments can be posted.");
        var customer = await dbContext.BusinessPartners.Include(partner => partner.CustomerProfile).SingleOrDefaultAsync(partner => partner.CompanyId == companyId && partner.Id == payment.BusinessPartnerId, cancellationToken)
            ?? throw new PaymentValidationException("The selected customer was not found.");
        if (!customer.IsActive || customer.CustomerProfile is null) throw new PaymentValidationException("The selected customer is unavailable for payments.");
        var cashBankAccount = await dbContext.CashBankAccounts.SingleOrDefaultAsync(account => account.CompanyId == companyId && account.Id == payment.CashBankAccountId, cancellationToken)
            ?? throw new PaymentValidationException("The selected cash or bank account was not found.");
        if (!cashBankAccount.IsActive) throw new PaymentValidationException("The selected cash or bank account is inactive.");

        var openItemIds = payment.Allocations.Select(allocation => allocation.OpenItemId).ToArray();
        var openItems = openItemIds.Length == 0 ? new Dictionary<Guid, OpenItem>() : await dbContext.OpenItems.Where(item => item.CompanyId == companyId && item.BusinessPartnerId == payment.BusinessPartnerId && item.Type == OpenItemType.Receivable && openItemIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        if (openItems.Count != openItemIds.Length) throw new PaymentValidationException("One or more selected receivables are unavailable for this customer.");
        foreach (var allocation in payment.Allocations)
        {
            var openItem = openItems[allocation.OpenItemId];
            if (!string.Equals(openItem.OriginalAmount.CurrencyCode, payment.Amount.CurrencyCode, StringComparison.Ordinal)) throw new PaymentValidationException("Payment and receivable currencies must match.");
            if (allocation.Amount.Amount > openItem.OutstandingAmount.Amount) throw new PaymentValidationException($"Allocation for invoice '{openItem.DocumentNumber.Value}' exceeds its outstanding amount.");
        }
        if (payment.UnappliedAmount.Amount != 0) throw new PaymentValidationException("Fully allocate a customer payment before posting. Unapplied receipts require an advance-account workflow.");

        foreach (var allocation in payment.Allocations) openItems[allocation.OpenItemId].ApplySettlement(allocation.Amount);
        var postedAt = DateTimeOffset.UtcNow;
        payment.MarkPosted(userId, postedAt);
        var accountingTransaction = new AccountingTransaction(companyId, new SourceReference("Payments", payment.Id, "CustomerPaymentPosted"), payment.PaymentDate);
        dbContext.AccountingTransactions.Add(accountingTransaction);
        await dbContext.SaveChangesAsync(cancellationToken);
        await accountingService.PostOperationalTransactionIfConfiguredAsync(companyId, accountingTransaction.Id, userId, cancellationToken);
        return true;
    }

    private async Task<(Company Company, BusinessPartner Customer, CashBankAccount CashBankAccount)> ValidateDraftInputAsync(Guid companyId, CustomerPaymentInput input, CancellationToken cancellationToken)
    {
        if (input.CustomerId == Guid.Empty || input.CashBankAccountId == Guid.Empty) throw new PaymentValidationException("A customer and cash or bank destination are required.");
        if (input.Amount <= 0) throw new PaymentValidationException("Payment amounts must be positive.");
        if (input.Allocations.GroupBy(allocation => allocation.OpenItemId).Any(group => group.Key == Guid.Empty || group.Count() > 1) || input.Allocations.Any(allocation => allocation.Amount <= 0)) throw new PaymentValidationException("Each allocation requires a unique receivable and a positive amount.");
        var company = await dbContext.Companies.AsNoTracking().SingleOrDefaultAsync(item => item.Id == companyId && item.IsActive, cancellationToken) ?? throw new PaymentValidationException("The selected company is unavailable.");
        var customer = await dbContext.BusinessPartners.AsNoTracking().Include(partner => partner.CustomerProfile).SingleOrDefaultAsync(partner => partner.CompanyId == companyId && partner.Id == input.CustomerId, cancellationToken) ?? throw new PaymentValidationException("The selected customer was not found.");
        if (!customer.IsActive || customer.CustomerProfile is null) throw new PaymentValidationException("The selected customer is unavailable for payments.");
        var cashBankAccount = await dbContext.CashBankAccounts.AsNoTracking().SingleOrDefaultAsync(account => account.CompanyId == companyId && account.Id == input.CashBankAccountId, cancellationToken) ?? throw new PaymentValidationException("The selected cash or bank account was not found.");
        if (!cashBankAccount.IsActive) throw new PaymentValidationException("The selected cash or bank account is inactive.");
        if (input.Allocations.Sum(allocation => allocation.Amount) > input.Amount) throw new PaymentValidationException("Payment allocations cannot exceed the payment amount.");
        var openItemIds = input.Allocations.Select(allocation => allocation.OpenItemId).ToArray();
        if (openItemIds.Length > 0)
        {
            var openItems = await dbContext.OpenItems.AsNoTracking().Where(item => item.CompanyId == companyId && item.BusinessPartnerId == input.CustomerId && item.Type == OpenItemType.Receivable && openItemIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
            if (openItems.Count != openItemIds.Length || openItems.Values.Any(item => item.OriginalAmount.CurrencyCode != company.BaseCurrencyCode) || input.Allocations.Any(allocation => allocation.Amount > openItems[allocation.OpenItemId].OutstandingAmount.Amount)) throw new PaymentValidationException("One or more allocations exceed the currently outstanding customer receivables.");
        }
        return (company, customer, cashBankAccount);
    }

    private static IReadOnlyCollection<PaymentAllocation> CreateAllocations(Guid paymentId, IReadOnlyCollection<PaymentAllocationInput> allocations, string currencyCode) =>
        allocations.Select(allocation => new PaymentAllocation(paymentId, allocation.OpenItemId, new Money(allocation.Amount, currencyCode))).ToArray();

    private async Task<IReadOnlyCollection<CustomerPaymentDto>> ToPaymentDtosAsync(IReadOnlyCollection<Payment> payments, CancellationToken cancellationToken)
    {
        if (payments.Count == 0) return Array.Empty<CustomerPaymentDto>();
        var customerIds = payments.Select(payment => payment.BusinessPartnerId).Distinct().ToArray();
        var cashBankIds = payments.Select(payment => payment.CashBankAccountId).Distinct().ToArray();
        var userIds = payments.SelectMany(payment => new[] { payment.CreatedByUserId, payment.PostedByUserId }).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        var openItemIds = payments.SelectMany(payment => payment.Allocations).Select(allocation => allocation.OpenItemId).Distinct().ToArray();
        var customers = await dbContext.BusinessPartners.AsNoTracking().Where(item => customerIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var cashBankAccounts = await dbContext.CashBankAccounts.AsNoTracking().Where(item => cashBankIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var users = userIds.Length == 0 ? new Dictionary<Guid, string>() : await dbContext.Users.AsNoTracking().Where(item => userIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);
        var openItems = openItemIds.Length == 0 ? new Dictionary<Guid, OpenItem>() : await dbContext.OpenItems.AsNoTracking().Where(item => openItemIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var invoiceIds = openItems.Values.Where(item => string.Equals(item.SourceReference.Module, "Sales", StringComparison.OrdinalIgnoreCase)).Select(item => item.SourceReference.AggregateId).Distinct().ToArray();
        var invoices = invoiceIds.Length == 0 ? new Dictionary<Guid, SalesInvoice>() : await dbContext.SalesInvoices.AsNoTracking().Where(item => invoiceIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        return payments.Select(payment =>
        {
            var customer = customers[payment.BusinessPartnerId];
            var cashBank = cashBankAccounts[payment.CashBankAccountId];
            return new CustomerPaymentDto(
                payment.Id, payment.DocumentNumber.Value, payment.BusinessPartnerId, customer.Code, customer.LegalName, payment.CashBankAccountId, cashBank.Name, cashBank.AccountType,
                payment.Direction, payment.PaymentDate, payment.Amount.CurrencyCode, payment.Amount.Amount, payment.AllocatedAmount.Amount, payment.UnappliedAmount.Amount, payment.Status,
                payment.ExternalReference, payment.Notes,
                payment.CreatedByUserId.HasValue && users.TryGetValue(payment.CreatedByUserId.Value, out var createdBy) ? createdBy : null, payment.CreatedAt,
                payment.PostedByUserId.HasValue && users.TryGetValue(payment.PostedByUserId.Value, out var postedBy) ? postedBy : null, payment.PostedAt,
                payment.Allocations.Select(allocation =>
                {
                    var openItem = openItems[allocation.OpenItemId];
                    invoices.TryGetValue(openItem.SourceReference.AggregateId, out var invoice);
                    return new PaymentAllocationDto(allocation.Id, openItem.Id, invoice?.Id, openItem.DocumentNumber.Value, invoice?.InvoiceDate, openItem.DueDate, openItem.OriginalAmount.Amount, openItem.OutstandingAmount.Amount, allocation.Amount.Amount, allocation.Amount.CurrencyCode);
                }).ToArray());
        }).ToArray();
    }

    private async Task<IReadOnlyCollection<OpenReceivableDto>> ToReceivableDtosAsync(IReadOnlyCollection<OpenItem> openItems, CancellationToken cancellationToken)
    {
        if (openItems.Count == 0) return Array.Empty<OpenReceivableDto>();
        var customerIds = openItems.Select(item => item.BusinessPartnerId).Distinct().ToArray();
        var invoiceIds = openItems.Where(item => string.Equals(item.SourceReference.Module, "Sales", StringComparison.OrdinalIgnoreCase)).Select(item => item.SourceReference.AggregateId).Distinct().ToArray();
        var customers = await dbContext.BusinessPartners.AsNoTracking().Where(item => customerIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var invoices = invoiceIds.Length == 0 ? new Dictionary<Guid, SalesInvoice>() : await dbContext.SalesInvoices.AsNoTracking().Where(item => invoiceIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return openItems.Select(item =>
        {
            var customer = customers[item.BusinessPartnerId];
            invoices.TryGetValue(item.SourceReference.AggregateId, out var invoice);
            var outstanding = item.OutstandingAmount.Amount;
            return new OpenReceivableDto(item.Id, item.BusinessPartnerId, customer.Code, customer.LegalName, invoice?.Id, item.DocumentNumber.Value, invoice?.InvoiceDate, item.DueDate, item.OriginalAmount.Amount, item.SettledAmount.Amount, outstanding, item.OriginalAmount.CurrencyCode, outstanding == 0 ? "Paid" : outstanding < item.OriginalAmount.Amount ? "PartiallyPaid" : "Unpaid", item.DueDate.HasValue && item.DueDate.Value < today && outstanding > 0);
        }).ToArray();
    }

    private async Task<string> NextPaymentNumberAsync(DateOnly paymentDate, CancellationToken cancellationToken)
    {
        long nextValue;
        if (!dbContext.Database.IsRelational()) nextValue = await dbContext.Payments.CountAsync(cancellationToken) + 1;
        else
        {
            var connection = dbContext.Database.GetDbConnection();
            var closeConnection = connection.State != ConnectionState.Open;
            if (closeConnection) await dbContext.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT nextval('erp.customer_payment_number_sequence')";
                command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
                nextValue = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
            }
            finally
            {
                if (closeConnection) await dbContext.Database.CloseConnectionAsync();
            }
        }
        return $"CP-{paymentDate.Year}-{nextValue:D6}";
    }

    private static CustomerPaymentQuery Normalize(CustomerPaymentQuery query) => query with { PageNumber = Math.Max(query.PageNumber, 1), PageSize = Math.Clamp(query.PageSize, 1, 100) };

    private static ReceivableQuery Normalize(ReceivableQuery query) => query with { PageNumber = Math.Max(query.PageNumber, 1), PageSize = Math.Clamp(query.PageSize, 1, 100) };
}
