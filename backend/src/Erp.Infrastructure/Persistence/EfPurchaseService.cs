using System.Data;
using Erp.Application.Inventory;
using Erp.Application.MasterData;
using Erp.Application.Accounting;
using Erp.Application.Common;
using Erp.Application.Payments;
using Erp.Application.Purchasing;
using Erp.Domain.Accounting;
using Erp.Domain.Common;
using Erp.Domain.Inventory;
using Erp.Domain.MasterData;
using Erp.Domain.Organizations;
using Erp.Domain.Payments;
using Erp.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Erp.Infrastructure.Persistence;

public sealed class EfPurchaseService(ErpDbContext dbContext, IInventoryService inventoryService, IAccountingService accountingService) : IPurchaseService
{
    public async Task<PagedResult<PurchaseInvoiceDto>> GetBillsAsync(Guid companyId, PurchaseInvoiceQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = query with { PageNumber = Math.Max(1, query.PageNumber), PageSize = Math.Clamp(query.PageSize, 1, 100) };
        var bills = dbContext.PurchaseInvoices.AsNoTracking().Include(bill => bill.Lines).Where(bill => bill.CompanyId == companyId);
        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            var term = normalized.Search.Trim().ToLower();
            var supplierIds = dbContext.BusinessPartners.Where(partner => partner.CompanyId == companyId && (partner.Code.ToLower().Contains(term) || partner.LegalName.ToLower().Contains(term))).Select(partner => partner.Id);
            bills = bills.Where(bill => supplierIds.Contains(bill.SupplierId) || (bill.SupplierReference != null && bill.SupplierReference.ToLower().Contains(term)) || bill.DocumentNumber.Value.ToLower().Contains(term));
        }
        if (normalized.SupplierId.HasValue) bills = bills.Where(bill => bill.SupplierId == normalized.SupplierId.Value);
        if (normalized.Status.HasValue) bills = bills.Where(bill => bill.Status == normalized.Status.Value);
        if (normalized.FromDate.HasValue) bills = bills.Where(bill => bill.InvoiceDate >= normalized.FromDate.Value);
        if (normalized.ToDate.HasValue) bills = bills.Where(bill => bill.InvoiceDate <= normalized.ToDate.Value);

        var totalCount = await bills.CountAsync(cancellationToken);
        var ordered = normalized.SortBy?.Equals("number", StringComparison.OrdinalIgnoreCase) == true
            ? normalized.SortDescending ? bills.OrderByDescending(bill => bill.DocumentNumber) : bills.OrderBy(bill => bill.DocumentNumber)
            : normalized.SortBy?.Equals("dueDate", StringComparison.OrdinalIgnoreCase) == true
                ? normalized.SortDescending ? bills.OrderByDescending(bill => bill.DueDate) : bills.OrderBy(bill => bill.DueDate)
                : normalized.SortDescending ? bills.OrderByDescending(bill => bill.InvoiceDate).ThenByDescending(bill => bill.DocumentNumber) : bills.OrderBy(bill => bill.InvoiceDate).ThenBy(bill => bill.DocumentNumber);
        var results = await ordered.Skip((normalized.PageNumber - 1) * normalized.PageSize).Take(normalized.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<PurchaseInvoiceDto>(await ToBillDtosAsync(results, cancellationToken), normalized.PageNumber, normalized.PageSize, totalCount);
    }

    public async Task<PurchaseInvoiceDto?> GetBillAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var bill = await dbContext.PurchaseInvoices.AsNoTracking().Include(item => item.Lines).SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        return bill is null ? null : (await ToBillDtosAsync([bill], cancellationToken)).Single();
    }

    public async Task<PurchaseInvoiceDto> CreateDraftBillAsync(Guid companyId, Guid userId, PurchaseInvoiceInput input, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new PurchaseValidationException("The authenticated user could not be identified.");
        var context = await ValidateBillInputAsync(companyId, input, cancellationToken);
        var dueDate = input.DueDate ?? input.InvoiceDate.AddDays(context.Supplier.PaymentTermsDays);
        var bill = new PurchaseInvoice(companyId, input.SupplierId, new DocumentNumber(await NextNumberAsync("purchase_invoice_number_sequence", "PI", input.InvoiceDate, cancellationToken)), input.InvoiceDate, context.Company.BaseCurrencyCode, dueDate, input.WarehouseId, input.SupplierReference, input.Notes, userId, DateTimeOffset.UtcNow);
        bill.ReplaceLines(CreateBillLines(input.Lines, context.Products, context.Company.BaseCurrencyCode));
        dbContext.PurchaseInvoices.Add(bill);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetBillAsync(companyId, bill.Id, cancellationToken))!;
    }

    public async Task<PurchaseInvoiceDto?> UpdateDraftBillAsync(Guid companyId, Guid id, PurchaseInvoiceInput input, CancellationToken cancellationToken = default)
    {
        var bill = await dbContext.PurchaseInvoices.Include(item => item.Lines).SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (bill is null) return null;
        if (bill.Status != PurchaseInvoiceStatus.Draft) throw new PurchaseConflictException("Posted purchase bills cannot be edited. Create a correcting document in a future purchasing workflow.");

        var context = await ValidateBillInputAsync(companyId, input, cancellationToken);
        var dueDate = input.DueDate ?? input.InvoiceDate.AddDays(context.Supplier.PaymentTermsDays);
        dbContext.PurchaseInvoiceLines.RemoveRange(bill.Lines.ToArray());
        bill.UpdateDraft(input.SupplierId, input.WarehouseId, input.InvoiceDate, dueDate, input.SupplierReference, input.Notes);
        bill.ReplaceLines(CreateBillLines(input.Lines, context.Products, bill.CurrencyCode));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetBillAsync(companyId, id, cancellationToken);
    }

    public async Task<bool> DeleteDraftBillAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var bill = await dbContext.PurchaseInvoices
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (bill is null) return false;
        if (bill.Status != PurchaseInvoiceStatus.Draft)
        {
            throw new PurchaseConflictException("Only draft purchase bills can be deleted. Posted purchase bills must remain for audit history.");
        }

        dbContext.PurchaseInvoiceLines.RemoveRange(bill.Lines.ToArray());
        dbContext.PurchaseInvoices.Remove(bill);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PurchaseInvoiceDto?> PostBillAsync(Guid companyId, Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new PurchaseValidationException("The authenticated user could not be identified.");
        if (!dbContext.Database.IsRelational())
        {
            var found = await PostBillCoreAsync(companyId, id, userId, cancellationToken);
            return found ? await GetBillAsync(companyId, id, cancellationToken) : null;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var found = await PostBillCoreAsync(companyId, id, userId, cancellationToken);
            if (!found) return null;
            await transaction.CommitAsync(cancellationToken);
            return await GetBillAsync(companyId, id, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new PurchaseConflictException("The purchase bill changed while it was posting. Refresh and try again.");
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new PurchaseConflictException("The purchase bill changed while it was posting. Refresh and try again.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PagedResult<SupplierPayableDto>> GetPayablesAsync(Guid companyId, SupplierPayableQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = query with { PageNumber = Math.Max(1, query.PageNumber), PageSize = Math.Clamp(query.PageSize, 1, 100) };
        var payables = dbContext.OpenItems.AsNoTracking().Where(item => item.CompanyId == companyId && item.Type == OpenItemType.Payable);
        if (normalized.SupplierId.HasValue) payables = payables.Where(item => item.BusinessPartnerId == normalized.SupplierId.Value);
        if (normalized.IsOpen.HasValue) payables = normalized.IsOpen.Value ? payables.Where(item => item.SettledAmount.Amount < item.OriginalAmount.Amount) : payables.Where(item => item.SettledAmount.Amount == item.OriginalAmount.Amount);
        if (normalized.IsOverdue == true)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            payables = payables.Where(item => item.DueDate.HasValue && item.DueDate < today && item.SettledAmount.Amount < item.OriginalAmount.Amount);
        }
        if (normalized.FromDate.HasValue) payables = payables.Where(item => item.DueDate.HasValue && item.DueDate >= normalized.FromDate.Value);
        if (normalized.ToDate.HasValue) payables = payables.Where(item => item.DueDate.HasValue && item.DueDate <= normalized.ToDate.Value);
        var totalCount = await payables.CountAsync(cancellationToken);
        var items = await payables.OrderBy(item => item.DueDate).ThenBy(item => item.DocumentNumber).Skip((normalized.PageNumber - 1) * normalized.PageSize).Take(normalized.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<SupplierPayableDto>(await ToPayableDtosAsync(items, cancellationToken), normalized.PageNumber, normalized.PageSize, totalCount);
    }

    public async Task<IReadOnlyCollection<SupplierPayableDto>> GetSupplierOpenPayablesAsync(Guid companyId, Guid supplierId, CancellationToken cancellationToken = default)
    {
        var items = await dbContext.OpenItems.AsNoTracking().Where(item => item.CompanyId == companyId && item.BusinessPartnerId == supplierId && item.Type == OpenItemType.Payable && item.SettledAmount.Amount < item.OriginalAmount.Amount).OrderBy(item => item.DueDate).ThenBy(item => item.DocumentNumber).ToListAsync(cancellationToken);
        return await ToPayableDtosAsync(items, cancellationToken);
    }

    public async Task<PurchaseBillPaymentSummaryDto?> GetBillPaymentSummaryAsync(Guid companyId, Guid billId, CancellationToken cancellationToken = default)
    {
        var bill = await dbContext.PurchaseInvoices.AsNoTracking().Include(item => item.Lines).SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == billId && item.Status == PurchaseInvoiceStatus.Posted, cancellationToken);
        if (bill is null) return null;
        var item = await dbContext.OpenItems.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.CompanyId == companyId && candidate.Type == OpenItemType.Payable && candidate.SourceReference.AggregateId == billId, cancellationToken);
        if (item is null) return new PurchaseBillPaymentSummaryDto(billId, null, bill.TotalAmount.Amount, 0, bill.TotalAmount.Amount, bill.CurrencyCode, "Unpaid", []);
        var paymentIds = await dbContext.PaymentAllocations.AsNoTracking().Where(allocation => allocation.OpenItemId == item.Id).Select(allocation => allocation.PaymentId).Distinct().ToArrayAsync(cancellationToken);
        var payments = paymentIds.Length == 0 ? [] : await dbContext.Payments.AsNoTracking().Include(payment => payment.Allocations).Where(payment => paymentIds.Contains(payment.Id) && payment.Direction == PaymentDirection.Outgoing && payment.Status == PaymentStatus.Posted).OrderByDescending(payment => payment.PaymentDate).ToListAsync(cancellationToken);
        var outstanding = item.OutstandingAmount.Amount;
        return new PurchaseBillPaymentSummaryDto(billId, item.Id, item.OriginalAmount.Amount, item.SettledAmount.Amount, outstanding, item.OriginalAmount.CurrencyCode, StatusFor(item), await ToSupplierPaymentDtosAsync(payments, cancellationToken));
    }

    public async Task<PagedResult<SupplierPaymentDto>> GetSupplierPaymentsAsync(Guid companyId, SupplierPaymentQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = query with { PageNumber = Math.Max(1, query.PageNumber), PageSize = Math.Clamp(query.PageSize, 1, 100) };
        var payments = dbContext.Payments.AsNoTracking().Include(payment => payment.Allocations).Where(payment => payment.CompanyId == companyId && payment.Direction == PaymentDirection.Outgoing);
        if (normalized.SupplierId.HasValue) payments = payments.Where(payment => payment.BusinessPartnerId == normalized.SupplierId.Value);
        if (normalized.Status.HasValue) payments = payments.Where(payment => payment.Status == normalized.Status.Value);
        if (normalized.CashBankAccountId.HasValue) payments = payments.Where(payment => payment.CashBankAccountId == normalized.CashBankAccountId.Value);
        if (normalized.FromDate.HasValue) payments = payments.Where(payment => payment.PaymentDate >= normalized.FromDate.Value);
        if (normalized.ToDate.HasValue) payments = payments.Where(payment => payment.PaymentDate <= normalized.ToDate.Value);
        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            var term = normalized.Search.Trim().ToLower();
            var supplierIds = dbContext.BusinessPartners.Where(partner => partner.CompanyId == companyId && (partner.Code.ToLower().Contains(term) || partner.LegalName.ToLower().Contains(term))).Select(partner => partner.Id);
            payments = payments.Where(payment => supplierIds.Contains(payment.BusinessPartnerId) || (payment.ExternalReference != null && payment.ExternalReference.ToLower().Contains(term)) || payment.DocumentNumber.Value.ToLower().Contains(term));
        }
        var totalCount = await payments.CountAsync(cancellationToken);
        var items = await (normalized.SortDescending ? payments.OrderByDescending(payment => payment.PaymentDate).ThenByDescending(payment => payment.DocumentNumber) : payments.OrderBy(payment => payment.PaymentDate).ThenBy(payment => payment.DocumentNumber)).Skip((normalized.PageNumber - 1) * normalized.PageSize).Take(normalized.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<SupplierPaymentDto>(await ToSupplierPaymentDtosAsync(items, cancellationToken), normalized.PageNumber, normalized.PageSize, totalCount);
    }

    public async Task<SupplierPaymentDto?> GetSupplierPaymentAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var payment = await dbContext.Payments.AsNoTracking().Include(item => item.Allocations).SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id && item.Direction == PaymentDirection.Outgoing, cancellationToken);
        return payment is null ? null : (await ToSupplierPaymentDtosAsync([payment], cancellationToken)).Single();
    }

    public async Task<SupplierPaymentDto> CreateDraftSupplierPaymentAsync(Guid companyId, Guid userId, SupplierPaymentInput input, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new PurchaseValidationException("The authenticated user could not be identified.");
        var context = await ValidateSupplierPaymentInputAsync(companyId, input, cancellationToken);
        var payment = new Payment(companyId, input.SupplierId, input.CashBankAccountId, PaymentDirection.Outgoing, new DocumentNumber(await NextNumberAsync("supplier_payment_number_sequence", "SP", input.PaymentDate, cancellationToken)), new Money(input.Amount, context.Company.BaseCurrencyCode), input.PaymentDate, input.ExternalReference, input.Notes, userId, DateTimeOffset.UtcNow);
        payment.ReplaceAllocations(CreateAllocations(payment.Id, input.Allocations, context.Company.BaseCurrencyCode));
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetSupplierPaymentAsync(companyId, payment.Id, cancellationToken))!;
    }

    public async Task<SupplierPaymentDto?> UpdateDraftSupplierPaymentAsync(Guid companyId, Guid id, SupplierPaymentInput input, CancellationToken cancellationToken = default)
    {
        var payment = await dbContext.Payments.Include(item => item.Allocations).SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id && item.Direction == PaymentDirection.Outgoing, cancellationToken);
        if (payment is null) return null;
        if (payment.Status != PaymentStatus.Draft) throw new PurchaseConflictException("Posted supplier payments cannot be edited.");
        var context = await ValidateSupplierPaymentInputAsync(companyId, input, cancellationToken);
        dbContext.PaymentAllocations.RemoveRange(payment.Allocations.ToArray());
        payment.UpdateDraft(input.SupplierId, input.CashBankAccountId, new Money(input.Amount, context.Company.BaseCurrencyCode), input.PaymentDate, input.ExternalReference, input.Notes);
        payment.ReplaceAllocations(CreateAllocations(payment.Id, input.Allocations, context.Company.BaseCurrencyCode));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetSupplierPaymentAsync(companyId, id, cancellationToken);
    }

    public async Task<SupplierPaymentDto?> PostSupplierPaymentAsync(Guid companyId, Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new PurchaseValidationException("The authenticated user could not be identified.");
        if (!dbContext.Database.IsRelational())
        {
            var found = await PostSupplierPaymentCoreAsync(companyId, id, userId, cancellationToken);
            return found ? await GetSupplierPaymentAsync(companyId, id, cancellationToken) : null;
        }
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var found = await PostSupplierPaymentCoreAsync(companyId, id, userId, cancellationToken);
            if (!found) return null;
            await transaction.CommitAsync(cancellationToken);
            return await GetSupplierPaymentAsync(companyId, id, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new PurchaseConflictException("The payable changed while this supplier payment was posting. Refresh and try again.");
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new PurchaseConflictException("The payable changed while this supplier payment was posting. Refresh and try again.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<SupplierPurchaseSummaryDto?> GetSupplierSummaryAsync(Guid companyId, Guid supplierId, CancellationToken cancellationToken = default)
    {
        var supplier = await dbContext.BusinessPartners.AsNoTracking().Include(partner => partner.SupplierProfile).SingleOrDefaultAsync(partner => partner.CompanyId == companyId && partner.Id == supplierId && partner.SupplierProfile != null, cancellationToken);
        if (supplier is null) return null;
        var bills = await dbContext.PurchaseInvoices.AsNoTracking().Include(bill => bill.Lines).Where(bill => bill.CompanyId == companyId && bill.SupplierId == supplierId && bill.Status == PurchaseInvoiceStatus.Posted).OrderByDescending(bill => bill.InvoiceDate).ThenByDescending(bill => bill.DocumentNumber).ToListAsync(cancellationToken);
        var payables = await dbContext.OpenItems.AsNoTracking().Where(item => item.CompanyId == companyId && item.BusinessPartnerId == supplierId && item.Type == OpenItemType.Payable).ToListAsync(cancellationToken);
        var payments = await dbContext.Payments.AsNoTracking().Include(payment => payment.Allocations).Where(payment => payment.CompanyId == companyId && payment.BusinessPartnerId == supplierId && payment.Direction == PaymentDirection.Outgoing && payment.Status == PaymentStatus.Posted).OrderByDescending(payment => payment.PaymentDate).Take(5).ToListAsync(cancellationToken);
        return new SupplierPurchaseSummaryDto(supplierId, bills.Sum(bill => bill.TotalAmount.Amount), payables.Sum(item => item.OutstandingAmount.Amount), bills.FirstOrDefault()?.CurrencyCode, await ToBillDtosAsync(bills.Take(5).ToArray(), cancellationToken), await ToSupplierPaymentDtosAsync(payments, cancellationToken));
    }

    public async Task<PurchaseDashboardDto> GetDashboardAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var allPostedBills = await dbContext.PurchaseInvoices.AsNoTracking().Include(bill => bill.Lines).Where(bill => bill.CompanyId == companyId && bill.Status == PurchaseInvoiceStatus.Posted).OrderByDescending(bill => bill.InvoiceDate).ThenByDescending(bill => bill.DocumentNumber).ToListAsync(cancellationToken);
        var bills = allPostedBills.Take(5).ToArray();
        var payments = await dbContext.Payments.AsNoTracking().Include(payment => payment.Allocations).Where(payment => payment.CompanyId == companyId && payment.Direction == PaymentDirection.Outgoing && payment.Status == PaymentStatus.Posted).OrderByDescending(payment => payment.PaymentDate).Take(5).ToListAsync(cancellationToken);
        var payableTotal = await dbContext.OpenItems.Where(item => item.CompanyId == companyId && item.Type == OpenItemType.Payable).SumAsync(item => (decimal?)item.OriginalAmount.Amount - item.SettledAmount.Amount, cancellationToken) ?? 0;
        return new PurchaseDashboardDto(allPostedBills.Sum(bill => bill.TotalAmount.Amount), payableTotal, allPostedBills.FirstOrDefault()?.CurrencyCode, await ToBillDtosAsync(bills, cancellationToken), await ToSupplierPaymentDtosAsync(payments, cancellationToken), BuildMonthlyTotals(allPostedBills.Select(bill => (bill.InvoiceDate, bill.TotalAmount.Amount))));
    }

    private static IReadOnlyCollection<DashboardPeriodTotalDto> BuildMonthlyTotals(IEnumerable<(DateOnly Date, decimal Amount)> amounts)
    {
        var currentMonth = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var totals = amounts
            .Where(item => item.Date >= currentMonth.AddMonths(-5) && item.Date < currentMonth.AddMonths(1))
            .GroupBy(item => new DateOnly(item.Date.Year, item.Date.Month, 1))
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));
        return Enumerable.Range(0, 6)
            .Select(offset => currentMonth.AddMonths(offset - 5))
            .Select(month => new DashboardPeriodTotalDto(month, totals.GetValueOrDefault(month)))
            .ToArray();
    }

    private async Task<bool> PostBillCoreAsync(Guid companyId, Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var bill = await dbContext.PurchaseInvoices.Include(item => item.Lines).SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (bill is null) return false;
        if (bill.Status != PurchaseInvoiceStatus.Draft) throw new PurchaseConflictException("Only draft purchase bills can be posted.");
        if (bill.Lines.Count == 0) throw new PurchaseValidationException("Add at least one purchase line before posting.");
        var supplier = await dbContext.BusinessPartners.Include(partner => partner.SupplierProfile).SingleOrDefaultAsync(partner => partner.CompanyId == companyId && partner.Id == bill.SupplierId, cancellationToken) ?? throw new PurchaseValidationException("The selected supplier was not found.");
        if (!supplier.IsActive || supplier.SupplierProfile is null) throw new PurchaseValidationException("The selected supplier is unavailable for purchases.");
        if (bill.Lines.Any(line => !line.ProductId.HasValue)) throw new PurchaseValidationException("Purchase bill lines must identify a product in this phase.");
        var productIds = bill.Lines.Select(line => line.ProductId!.Value).Distinct().ToArray();
        var products = await dbContext.Products.Where(product => product.CompanyId == companyId && productIds.Contains(product.Id)).ToDictionaryAsync(product => product.Id, cancellationToken);
        if (products.Count != productIds.Length) throw new PurchaseValidationException("One or more purchase products were not found.");
        var stockLines = bill.Lines.Where(line => products[line.ProductId!.Value].IsStockTracked).OrderBy(line => line.ProductId).ToArray();
        Warehouse? warehouse = null;
        if (stockLines.Length > 0)
        {
            if (!bill.WarehouseId.HasValue) throw new PurchaseValidationException("A warehouse is required before posting stock-controlled products.");
            warehouse = await dbContext.Warehouses.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == bill.WarehouseId.Value, cancellationToken) ?? throw new PurchaseValidationException("The selected warehouse was not found.");
            if (!warehouse.IsActive) throw new PurchaseValidationException("The selected warehouse is inactive.");
        }
        foreach (var line in bill.Lines)
        {
            var product = products[line.ProductId!.Value];
            if (!product.IsActive || !product.StockUnitOfMeasureId.HasValue || product.StockUnitOfMeasureId != line.Quantity.UnitOfMeasureId) throw new PurchaseValidationException($"Product '{product.Name}' must be active and use its configured stock unit of measure.");
        }

        var postedAt = DateTimeOffset.UtcNow;
        bill.MarkPosted(userId, postedAt);
        if (warehouse is not null)
        {
            foreach (var line in stockLines)
            {
                await inventoryService.RecordSystemStockInAsync(companyId, userId, new InventorySystemStockInInput(line.ProductId!.Value, warehouse.Id, line.Quantity.Amount, postedAt, new SourceReference("Purchasing", bill.Id, "PurchaseInvoicePosted"), line.NetAmount, bill.CurrencyCode, bill.DocumentNumber.Value, $"Purchase bill {bill.DocumentNumber.Value}"), cancellationToken);
            }
        }
        dbContext.OpenItems.Add(new OpenItem(companyId, bill.SupplierId, OpenItemType.Payable, new SourceReference("Purchasing", bill.Id, "PurchaseInvoicePosted"), bill.DocumentNumber, bill.TotalAmount, dueDate: bill.DueDate));
        var accountingTransaction = new AccountingTransaction(companyId, new SourceReference("Purchasing", bill.Id, "PurchaseInvoicePosted"), bill.InvoiceDate);
        dbContext.AccountingTransactions.Add(accountingTransaction);
        await dbContext.SaveChangesAsync(cancellationToken);
        await accountingService.PostOperationalTransactionIfConfiguredAsync(companyId, accountingTransaction.Id, userId, cancellationToken);
        return true;
    }

    private async Task<bool> PostSupplierPaymentCoreAsync(Guid companyId, Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments.Include(item => item.Allocations).SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id && item.Direction == PaymentDirection.Outgoing, cancellationToken);
        if (payment is null) return false;
        if (payment.Status != PaymentStatus.Draft) throw new PurchaseConflictException("Only draft supplier payments can be posted.");
        var supplier = await dbContext.BusinessPartners.Include(partner => partner.SupplierProfile).SingleOrDefaultAsync(partner => partner.CompanyId == companyId && partner.Id == payment.BusinessPartnerId, cancellationToken) ?? throw new PurchaseValidationException("The selected supplier was not found.");
        if (!supplier.IsActive || supplier.SupplierProfile is null) throw new PurchaseValidationException("The selected supplier is unavailable for payments.");
        var cashBank = await dbContext.CashBankAccounts.SingleOrDefaultAsync(account => account.CompanyId == companyId && account.Id == payment.CashBankAccountId, cancellationToken) ?? throw new PurchaseValidationException("The selected cash or bank account was not found.");
        if (!cashBank.IsActive) throw new PurchaseValidationException("The selected cash or bank account is inactive.");
        var openItemIds = payment.Allocations.Select(allocation => allocation.OpenItemId).ToArray();
        var items = openItemIds.Length == 0 ? new Dictionary<Guid, OpenItem>() : await dbContext.OpenItems.Where(item => item.CompanyId == companyId && item.BusinessPartnerId == payment.BusinessPartnerId && item.Type == OpenItemType.Payable && openItemIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        if (items.Count != openItemIds.Length) throw new PurchaseValidationException("One or more selected payables are unavailable for this supplier.");
        foreach (var allocation in payment.Allocations)
        {
            var item = items[allocation.OpenItemId];
            if (!string.Equals(item.OriginalAmount.CurrencyCode, payment.Amount.CurrencyCode, StringComparison.Ordinal) || allocation.Amount.Amount > item.OutstandingAmount.Amount) throw new PurchaseValidationException($"Allocation for purchase bill '{item.DocumentNumber.Value}' exceeds its outstanding amount or uses the wrong currency.");
        }
        if (payment.UnappliedAmount.Amount != 0) throw new PurchaseValidationException("Fully allocate a supplier payment before posting. Unapplied payments require an advance-payment workflow.");
        foreach (var allocation in payment.Allocations) items[allocation.OpenItemId].ApplySettlement(allocation.Amount);
        payment.MarkPosted(userId, DateTimeOffset.UtcNow);
        var accountingTransaction = new AccountingTransaction(companyId, new SourceReference("Purchasing", payment.Id, "SupplierPaymentPosted"), payment.PaymentDate);
        dbContext.AccountingTransactions.Add(accountingTransaction);
        await dbContext.SaveChangesAsync(cancellationToken);
        await accountingService.PostOperationalTransactionIfConfiguredAsync(companyId, accountingTransaction.Id, userId, cancellationToken);
        return true;
    }

    private async Task<(Company Company, BusinessPartner Supplier, IReadOnlyDictionary<Guid, Product> Products)> ValidateBillInputAsync(Guid companyId, PurchaseInvoiceInput input, CancellationToken cancellationToken)
    {
        if (input.SupplierId == Guid.Empty) throw new PurchaseValidationException("A supplier is required.");
        if (input.DueDate.HasValue && input.DueDate < input.InvoiceDate) throw new PurchaseValidationException("The due date cannot be before the bill date.");
        var company = await dbContext.Companies.AsNoTracking().SingleOrDefaultAsync(item => item.Id == companyId && item.IsActive, cancellationToken) ?? throw new PurchaseValidationException("The selected company is unavailable.");
        var supplier = await dbContext.BusinessPartners.AsNoTracking().Include(partner => partner.SupplierProfile).SingleOrDefaultAsync(partner => partner.CompanyId == companyId && partner.Id == input.SupplierId, cancellationToken) ?? throw new PurchaseValidationException("The selected supplier was not found.");
        if (!supplier.IsActive || supplier.SupplierProfile is null) throw new PurchaseValidationException("The selected supplier is unavailable for purchases.");
        if (input.WarehouseId.HasValue && !await dbContext.Warehouses.AnyAsync(warehouse => warehouse.CompanyId == companyId && warehouse.Id == input.WarehouseId && warehouse.IsActive, cancellationToken)) throw new PurchaseValidationException("The selected active warehouse was not found.");
        var lines = input.Lines ?? Array.Empty<PurchaseInvoiceLineInput>();
        if (lines.Any(line => line.ProductId == Guid.Empty || line.Quantity <= 0 || line.UnitCost < 0 || line.DiscountPercentage is < 0 or > 100)) throw new PurchaseValidationException("Purchase lines require a product, positive quantity, non-negative unit cost, and a valid discount.");
        var productIds = lines.Select(line => line.ProductId).Distinct().ToArray();
        var products = productIds.Length == 0 ? new Dictionary<Guid, Product>() : await dbContext.Products.AsNoTracking().Where(product => product.CompanyId == companyId && productIds.Contains(product.Id)).ToDictionaryAsync(product => product.Id, cancellationToken);
        if (products.Count != productIds.Length) throw new PurchaseValidationException("One or more purchase products were not found.");
        foreach (var product in products.Values)
        {
            if (!product.IsActive || !product.StockUnitOfMeasureId.HasValue) throw new PurchaseValidationException($"Product '{product.Name}' must be active and have a unit of measure before it can be purchased.");
        }
        return (company, supplier, products);
    }

    private async Task<(Company Company, BusinessPartner Supplier)> ValidateSupplierPaymentInputAsync(Guid companyId, SupplierPaymentInput input, CancellationToken cancellationToken)
    {
        if (input.SupplierId == Guid.Empty || input.CashBankAccountId == Guid.Empty || input.Amount <= 0) throw new PurchaseValidationException("A supplier, cash or bank destination, and positive amount are required.");
        if (input.Allocations.GroupBy(item => item.OpenItemId).Any(group => group.Key == Guid.Empty || group.Count() > 1) || input.Allocations.Any(item => item.Amount <= 0) || input.Allocations.Sum(item => item.Amount) > input.Amount) throw new PurchaseValidationException("Each allocation must be unique and positive, and total allocations cannot exceed the payment amount.");
        var company = await dbContext.Companies.AsNoTracking().SingleOrDefaultAsync(item => item.Id == companyId && item.IsActive, cancellationToken) ?? throw new PurchaseValidationException("The selected company is unavailable.");
        var supplier = await dbContext.BusinessPartners.AsNoTracking().Include(partner => partner.SupplierProfile).SingleOrDefaultAsync(partner => partner.CompanyId == companyId && partner.Id == input.SupplierId, cancellationToken) ?? throw new PurchaseValidationException("The selected supplier was not found.");
        if (!supplier.IsActive || supplier.SupplierProfile is null) throw new PurchaseValidationException("The selected supplier is unavailable for payments.");
        if (!await dbContext.CashBankAccounts.AnyAsync(account => account.CompanyId == companyId && account.Id == input.CashBankAccountId && account.IsActive, cancellationToken)) throw new PurchaseValidationException("The selected active cash or bank account was not found.");
        var itemIds = input.Allocations.Select(item => item.OpenItemId).ToArray();
        if (itemIds.Length > 0)
        {
            var items = await dbContext.OpenItems.AsNoTracking().Where(item => item.CompanyId == companyId && item.BusinessPartnerId == input.SupplierId && item.Type == OpenItemType.Payable && itemIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
            if (items.Count != itemIds.Length || items.Values.Any(item => item.OriginalAmount.CurrencyCode != company.BaseCurrencyCode) || input.Allocations.Any(allocation => allocation.Amount > items[allocation.OpenItemId].OutstandingAmount.Amount)) throw new PurchaseValidationException("One or more allocations exceed the currently outstanding supplier payables.");
        }
        return (company, supplier);
    }

    private static IReadOnlyCollection<PurchaseInvoiceLine> CreateBillLines(IReadOnlyCollection<PurchaseInvoiceLineInput> inputs, IReadOnlyDictionary<Guid, Product> products, string currencyCode) => inputs.Select((input, index) =>
    {
        var product = products[input.ProductId];
        return new PurchaseInvoiceLine(index + 1, string.IsNullOrWhiteSpace(input.Description) ? product.Name : input.Description, new Quantity(input.Quantity, product.StockUnitOfMeasureId!.Value), new Money(input.UnitCost, currencyCode), product.Id, input.DiscountPercentage, memo: input.Memo);
    }).ToArray();

    private static IReadOnlyCollection<PaymentAllocation> CreateAllocations(Guid paymentId, IReadOnlyCollection<PaymentAllocationInput> allocations, string currencyCode) => allocations.Select(allocation => new PaymentAllocation(paymentId, allocation.OpenItemId, new Money(allocation.Amount, currencyCode))).ToArray();

    private async Task<IReadOnlyCollection<PurchaseInvoiceDto>> ToBillDtosAsync(IReadOnlyCollection<PurchaseInvoice> bills, CancellationToken cancellationToken)
    {
        if (bills.Count == 0) return Array.Empty<PurchaseInvoiceDto>();
        var supplierIds = bills.Select(bill => bill.SupplierId).Distinct().ToArray();
        var warehouseIds = bills.Where(bill => bill.WarehouseId.HasValue).Select(bill => bill.WarehouseId!.Value).Distinct().ToArray();
        var userIds = bills.SelectMany(bill => new[] { bill.CreatedByUserId, bill.PostedByUserId }).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        var productIds = bills.SelectMany(bill => bill.Lines).Where(line => line.ProductId.HasValue).Select(line => line.ProductId!.Value).Distinct().ToArray();
        var unitIds = bills.SelectMany(bill => bill.Lines).Select(line => line.Quantity.UnitOfMeasureId).Distinct().ToArray();
        var billIds = bills.Select(bill => bill.Id).ToArray();
        var suppliers = await dbContext.BusinessPartners.AsNoTracking().Where(partner => supplierIds.Contains(partner.Id)).ToDictionaryAsync(partner => partner.Id, cancellationToken);
        var warehouses = warehouseIds.Length == 0 ? new Dictionary<Guid, Warehouse>() : await dbContext.Warehouses.AsNoTracking().Where(warehouse => warehouseIds.Contains(warehouse.Id)).ToDictionaryAsync(warehouse => warehouse.Id, cancellationToken);
        var users = userIds.Length == 0 ? new Dictionary<Guid, string>() : await dbContext.Users.AsNoTracking().Where(user => userIds.Contains(user.Id)).ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        var products = productIds.Length == 0 ? new Dictionary<Guid, Product>() : await dbContext.Products.AsNoTracking().Where(product => productIds.Contains(product.Id)).ToDictionaryAsync(product => product.Id, cancellationToken);
        var units = unitIds.Length == 0 ? new Dictionary<Guid, string>() : await dbContext.UnitsOfMeasure.AsNoTracking().Where(unit => unitIds.Contains(unit.Id)).ToDictionaryAsync(unit => unit.Id, unit => unit.Name, cancellationToken);
        var openItems = await dbContext.OpenItems.AsNoTracking().Where(item => item.CompanyId == bills.First().CompanyId && item.Type == OpenItemType.Payable && billIds.Contains(item.SourceReference.AggregateId)).ToDictionaryAsync(item => item.SourceReference.AggregateId, cancellationToken);
        return bills.Select(bill =>
        {
            var supplier = suppliers[bill.SupplierId];
            warehouses.TryGetValue(bill.WarehouseId ?? Guid.Empty, out var warehouse);
            var total = bill.TotalAmount.Amount;
            var outstanding = bill.Status == PurchaseInvoiceStatus.Posted && openItems.TryGetValue(bill.Id, out var item) ? item.OutstandingAmount.Amount : 0m;
            PurchaseInvoicePaymentStatus? status = bill.Status == PurchaseInvoiceStatus.Posted ? outstanding == 0 ? PurchaseInvoicePaymentStatus.Paid : outstanding < total ? PurchaseInvoicePaymentStatus.PartiallyPaid : PurchaseInvoicePaymentStatus.Unpaid : null;
            return new PurchaseInvoiceDto(bill.Id, bill.DocumentNumber.Value, bill.SupplierId, supplier.Code, supplier.LegalName, bill.WarehouseId, warehouse?.Code, warehouse?.Name, bill.InvoiceDate, bill.DueDate, bill.CurrencyCode, bill.Status, status, bill.Subtotal.Amount, bill.DiscountTotal.Amount, total, outstanding, bill.SupplierReference, bill.Notes, bill.CreatedByUserId.HasValue && users.TryGetValue(bill.CreatedByUserId.Value, out var createdBy) ? createdBy : null, bill.CreatedAt, bill.PostedByUserId.HasValue && users.TryGetValue(bill.PostedByUserId.Value, out var postedBy) ? postedBy : null, bill.PostedAt, bill.Lines.Select(line => { var product = products[line.ProductId!.Value]; return new PurchaseInvoiceLineDto(line.Id, line.LineNumber, product.Id, product.Sku, product.Name, line.Description, line.Quantity.Amount, line.Quantity.UnitOfMeasureId, units[line.Quantity.UnitOfMeasureId], line.UnitCost.Amount, line.DiscountPercentage, line.DiscountAmount, line.NetAmount, line.Memo); }).ToArray());
        }).ToArray();
    }

    private async Task<IReadOnlyCollection<SupplierPayableDto>> ToPayableDtosAsync(IReadOnlyCollection<OpenItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0) return Array.Empty<SupplierPayableDto>();
        var supplierIds = items.Select(item => item.BusinessPartnerId).Distinct().ToArray();
        var billIds = items.Where(item => item.SourceReference.Module == "Purchasing").Select(item => item.SourceReference.AggregateId).Distinct().ToArray();
        var suppliers = await dbContext.BusinessPartners.AsNoTracking().Where(partner => supplierIds.Contains(partner.Id)).ToDictionaryAsync(partner => partner.Id, cancellationToken);
        var bills = billIds.Length == 0 ? new Dictionary<Guid, PurchaseInvoice>() : await dbContext.PurchaseInvoices.AsNoTracking().Where(bill => billIds.Contains(bill.Id)).ToDictionaryAsync(bill => bill.Id, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return items.Select(item => { var supplier = suppliers[item.BusinessPartnerId]; bills.TryGetValue(item.SourceReference.AggregateId, out var bill); return new SupplierPayableDto(item.Id, item.BusinessPartnerId, supplier.Code, supplier.LegalName, bill?.Id, item.DocumentNumber.Value, bill?.SupplierReference, bill?.InvoiceDate, item.DueDate, item.OriginalAmount.Amount, item.SettledAmount.Amount, item.OutstandingAmount.Amount, item.OriginalAmount.CurrencyCode, StatusFor(item), item.DueDate.HasValue && item.DueDate < today && item.OutstandingAmount.Amount > 0); }).ToArray();
    }

    private async Task<IReadOnlyCollection<SupplierPaymentDto>> ToSupplierPaymentDtosAsync(IReadOnlyCollection<Payment> payments, CancellationToken cancellationToken)
    {
        if (payments.Count == 0) return Array.Empty<SupplierPaymentDto>();
        var supplierIds = payments.Select(payment => payment.BusinessPartnerId).Distinct().ToArray();
        var accountIds = payments.Select(payment => payment.CashBankAccountId).Distinct().ToArray();
        var userIds = payments.SelectMany(payment => new[] { payment.CreatedByUserId, payment.PostedByUserId }).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        var openItemIds = payments.SelectMany(payment => payment.Allocations).Select(allocation => allocation.OpenItemId).Distinct().ToArray();
        var suppliers = await dbContext.BusinessPartners.AsNoTracking().Where(partner => supplierIds.Contains(partner.Id)).ToDictionaryAsync(partner => partner.Id, cancellationToken);
        var accounts = await dbContext.CashBankAccounts.AsNoTracking().Where(account => accountIds.Contains(account.Id)).ToDictionaryAsync(account => account.Id, cancellationToken);
        var users = userIds.Length == 0 ? new Dictionary<Guid, string>() : await dbContext.Users.AsNoTracking().Where(user => userIds.Contains(user.Id)).ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        var items = openItemIds.Length == 0 ? new Dictionary<Guid, OpenItem>() : await dbContext.OpenItems.AsNoTracking().Where(item => openItemIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var billIds = items.Values.Where(item => item.SourceReference.Module == "Purchasing").Select(item => item.SourceReference.AggregateId).Distinct().ToArray();
        var bills = billIds.Length == 0 ? new Dictionary<Guid, PurchaseInvoice>() : await dbContext.PurchaseInvoices.AsNoTracking().Where(bill => billIds.Contains(bill.Id)).ToDictionaryAsync(bill => bill.Id, cancellationToken);
        return payments.Select(payment =>
        {
            var supplier = suppliers[payment.BusinessPartnerId];
            var account = accounts[payment.CashBankAccountId];
            return new SupplierPaymentDto(payment.Id, payment.DocumentNumber.Value, payment.BusinessPartnerId, supplier.Code, supplier.LegalName, payment.CashBankAccountId, account.Name, account.AccountType.ToString(), payment.PaymentDate, payment.Amount.CurrencyCode, payment.Amount.Amount, payment.AllocatedAmount.Amount, payment.UnappliedAmount.Amount, payment.Status, payment.ExternalReference, payment.Notes, payment.CreatedByUserId.HasValue && users.TryGetValue(payment.CreatedByUserId.Value, out var createdBy) ? createdBy : null, payment.CreatedAt, payment.PostedByUserId.HasValue && users.TryGetValue(payment.PostedByUserId.Value, out var postedBy) ? postedBy : null, payment.PostedAt, payment.Allocations.Select(allocation => { var item = items[allocation.OpenItemId]; bills.TryGetValue(item.SourceReference.AggregateId, out var bill); return new SupplierPaymentAllocationDto(allocation.Id, item.Id, bill?.Id, item.DocumentNumber.Value, bill?.SupplierReference, bill?.InvoiceDate, item.DueDate, item.OriginalAmount.Amount, item.OutstandingAmount.Amount, allocation.Amount.Amount, allocation.Amount.CurrencyCode); }).ToArray());
        }).ToArray();
    }

    private async Task<string> NextNumberAsync(string sequenceName, string prefix, DateOnly date, CancellationToken cancellationToken)
    {
        long value;
        if (!dbContext.Database.IsRelational()) value = sequenceName == "purchase_invoice_number_sequence" ? await dbContext.PurchaseInvoices.CountAsync(cancellationToken) + 1 : await dbContext.Payments.CountAsync(payment => payment.Direction == PaymentDirection.Outgoing, cancellationToken) + 1;
        else
        {
            var connection = dbContext.Database.GetDbConnection();
            var close = connection.State != ConnectionState.Open;
            if (close) await dbContext.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"SELECT nextval('erp.{sequenceName}')";
                command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
                value = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
            }
            finally { if (close) await dbContext.Database.CloseConnectionAsync(); }
        }
        return $"{prefix}-{date.Year}-{value:D6}";
    }

    private static string StatusFor(OpenItem item) => item.OutstandingAmount.Amount == 0 ? "Paid" : item.OutstandingAmount.Amount < item.OriginalAmount.Amount ? "PartiallyPaid" : "Unpaid";
}
