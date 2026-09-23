using System.Data;
using Erp.Application.Inventory;
using Erp.Application.MasterData;
using Erp.Application.Accounting;
using Erp.Application.Common;
using Erp.Application.Sales;
using Erp.Domain.Accounting;
using Erp.Domain.Common;
using Erp.Domain.Inventory;
using Erp.Domain.MasterData;
using Erp.Domain.Organizations;
using Erp.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Erp.Infrastructure.Persistence;

public sealed class EfSalesService(ErpDbContext dbContext, IInventoryService inventoryService, IAccountingService accountingService) : ISalesService
{
    public async Task<PagedResult<SalesInvoiceDto>> GetInvoicesAsync(Guid companyId, SalesInvoiceQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(query);
        var invoices = dbContext.SalesInvoices.AsNoTracking()
            .Include(invoice => invoice.Lines)
            .Where(invoice => invoice.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            var term = normalized.Search.Trim().ToLower();
            var customerIds = dbContext.BusinessPartners
                .Where(customer => customer.CompanyId == companyId && (customer.Code.ToLower().Contains(term) || customer.LegalName.ToLower().Contains(term)))
                .Select(customer => customer.Id);
            var matchingInvoiceIds = dbContext.Database.IsNpgsql()
                ? dbContext.SalesInvoices
                    .FromSqlInterpolated($"""SELECT * FROM erp.sales_invoices WHERE "CompanyId" = {companyId} AND document_number ILIKE {"%" + term + "%"}""")
                    .Select(invoice => invoice.Id)
                : dbContext.SalesInvoices
                    .Where(invoice => invoice.CompanyId == companyId && invoice.DocumentNumber.Value.ToLower().Contains(term))
                    .Select(invoice => invoice.Id);
            invoices = invoices.Where(invoice => matchingInvoiceIds.Contains(invoice.Id) || customerIds.Contains(invoice.CustomerId));
        }
        if (normalized.CustomerId.HasValue) invoices = invoices.Where(invoice => invoice.CustomerId == normalized.CustomerId.Value);
        if (normalized.Status.HasValue) invoices = invoices.Where(invoice => invoice.Status == normalized.Status.Value);
        if (normalized.FromDate.HasValue) invoices = invoices.Where(invoice => invoice.InvoiceDate >= normalized.FromDate.Value);
        if (normalized.ToDate.HasValue) invoices = invoices.Where(invoice => invoice.InvoiceDate <= normalized.ToDate.Value);

        var totalCount = await invoices.CountAsync(cancellationToken);
        var ordered = normalized.SortBy?.Equals("number", StringComparison.OrdinalIgnoreCase) == true
            ? (normalized.SortDescending ? invoices.OrderByDescending(invoice => invoice.DocumentNumber) : invoices.OrderBy(invoice => invoice.DocumentNumber))
            : normalized.SortBy?.Equals("dueDate", StringComparison.OrdinalIgnoreCase) == true
                ? (normalized.SortDescending ? invoices.OrderByDescending(invoice => invoice.DueDate) : invoices.OrderBy(invoice => invoice.DueDate))
                : (normalized.SortDescending ? invoices.OrderByDescending(invoice => invoice.InvoiceDate).ThenByDescending(invoice => invoice.DocumentNumber) : invoices.OrderBy(invoice => invoice.InvoiceDate).ThenBy(invoice => invoice.DocumentNumber));
        var items = await ordered.Skip((normalized.PageNumber - 1) * normalized.PageSize).Take(normalized.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<SalesInvoiceDto>(await ToDtosAsync(items, cancellationToken), normalized.PageNumber, normalized.PageSize, totalCount);
    }

    public async Task<SalesInvoiceDto?> GetInvoiceAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await dbContext.SalesInvoices.AsNoTracking()
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        return invoice is null ? null : (await ToDtosAsync([invoice], cancellationToken)).Single();
    }

    public async Task<SalesInvoiceDto> CreateDraftAsync(Guid companyId, Guid userId, SalesInvoiceInput input, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new SalesValidationException("The authenticated user could not be identified.");
        var context = await ValidateInputAsync(companyId, input, cancellationToken);
        var dueDate = input.DueDate ?? input.InvoiceDate.AddDays(context.Customer.PaymentTermsDays);
        var invoice = new SalesInvoice(
            companyId,
            input.CustomerId,
            new DocumentNumber(await NextInvoiceNumberAsync(input.InvoiceDate, cancellationToken)),
            input.InvoiceDate,
            context.Company.BaseCurrencyCode,
            dueDate,
            input.WarehouseId,
            input.Reference,
            input.Notes,
            userId,
            DateTimeOffset.UtcNow);
        invoice.ReplaceLines(CreateLines(input.Lines, context.Products, context.Company.BaseCurrencyCode));
        dbContext.SalesInvoices.Add(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetInvoiceAsync(companyId, invoice.Id, cancellationToken))!;
    }

    public async Task<SalesInvoiceDto?> UpdateDraftAsync(Guid companyId, Guid id, SalesInvoiceInput input, CancellationToken cancellationToken = default)
    {
        var invoice = await dbContext.SalesInvoices.Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (invoice is null) return null;
        if (invoice.Status != SalesInvoiceStatus.Draft) throw new SalesConflictException("Posted invoices cannot be edited. Create a correcting document in a future sales workflow.");

        var context = await ValidateInputAsync(companyId, input, cancellationToken);
        var dueDate = input.DueDate ?? input.InvoiceDate.AddDays(context.Customer.PaymentTermsDays);
        var existingLines = invoice.Lines.ToArray();
        dbContext.SalesInvoiceLines.RemoveRange(existingLines);
        invoice.UpdateDraft(input.CustomerId, input.WarehouseId, input.InvoiceDate, dueDate, input.Reference, input.Notes);
        invoice.ReplaceLines(CreateLines(input.Lines, context.Products, invoice.CurrencyCode));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetInvoiceAsync(companyId, id, cancellationToken);
    }

    public async Task<SalesInvoiceDto?> PostAsync(Guid companyId, Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new SalesValidationException("The authenticated user could not be identified.");

        if (!dbContext.Database.IsRelational())
        {
            var found = await PostCoreAsync(companyId, id, userId, cancellationToken);
            return found ? await GetInvoiceAsync(companyId, id, cancellationToken) : null;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var found = await PostCoreAsync(companyId, id, userId, cancellationToken);
            if (!found) return null;
            await transaction.CommitAsync(cancellationToken);
            return await GetInvoiceAsync(companyId, id, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<CustomerSalesSummaryDto?> GetCustomerSummaryAsync(Guid companyId, Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.BusinessPartners.AsNoTracking()
            .Include(partner => partner.CustomerProfile)
            .SingleOrDefaultAsync(partner => partner.CompanyId == companyId && partner.Id == customerId && partner.CustomerProfile != null, cancellationToken);
        if (customer is null) return null;

        var postedInvoices = await dbContext.SalesInvoices.AsNoTracking().Include(invoice => invoice.Lines)
            .Where(invoice => invoice.CompanyId == companyId && invoice.CustomerId == customerId && invoice.Status == SalesInvoiceStatus.Posted)
            .OrderByDescending(invoice => invoice.InvoiceDate).ThenByDescending(invoice => invoice.DocumentNumber)
            .ToListAsync(cancellationToken);
        var openItems = await dbContext.OpenItems.AsNoTracking()
            .Where(item => item.CompanyId == companyId && item.BusinessPartnerId == customerId && item.Type == OpenItemType.Receivable)
            .ToListAsync(cancellationToken);
        var currency = postedInvoices.FirstOrDefault()?.CurrencyCode;
        return new CustomerSalesSummaryDto(
            customerId,
            postedInvoices.Sum(invoice => invoice.TotalAmount.Amount),
            openItems.Sum(item => item.OutstandingAmount.Amount),
            currency,
            await ToDtosAsync(postedInvoices.Take(5).ToArray(), cancellationToken));
    }

    public async Task<SalesDashboardDto> GetDashboardAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var invoices = await dbContext.SalesInvoices.AsNoTracking().Include(invoice => invoice.Lines)
            .Where(invoice => invoice.CompanyId == companyId && invoice.Status == SalesInvoiceStatus.Posted)
            .OrderByDescending(invoice => invoice.InvoiceDate).ThenByDescending(invoice => invoice.DocumentNumber)
            .ToListAsync(cancellationToken);
        var openItems = await dbContext.OpenItems.AsNoTracking()
            .Where(item => item.CompanyId == companyId && item.Type == OpenItemType.Receivable)
            .ToListAsync(cancellationToken);
        var currency = invoices.FirstOrDefault()?.CurrencyCode;
        return new SalesDashboardDto(
            invoices.Sum(invoice => invoice.TotalAmount.Amount),
            openItems.Sum(item => item.OutstandingAmount.Amount),
            currency,
            await ToDtosAsync(invoices.Take(5).ToArray(), cancellationToken),
            BuildMonthlyTotals(invoices.Select(invoice => (invoice.InvoiceDate, invoice.TotalAmount.Amount))));
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

    private async Task<bool> PostCoreAsync(Guid companyId, Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var invoice = await dbContext.SalesInvoices.Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (invoice is null) return false;
        if (invoice.Status != SalesInvoiceStatus.Draft) throw new SalesConflictException("Only draft invoices can be posted.");
        if (invoice.Lines.Count == 0) throw new SalesValidationException("Add at least one invoice line before posting.");

        var customer = await dbContext.BusinessPartners.Include(partner => partner.CustomerProfile)
            .SingleOrDefaultAsync(partner => partner.CompanyId == companyId && partner.Id == invoice.CustomerId, cancellationToken)
            ?? throw new SalesValidationException("The selected customer was not found.");
        if (!customer.IsActive || customer.CustomerProfile is null) throw new SalesValidationException("The selected customer is unavailable for sales.");

        var productIds = invoice.Lines.Select(line => line.ProductId).ToArray();
        if (productIds.Any(productId => !productId.HasValue)) throw new SalesValidationException("Sales invoice lines must identify a product in this phase.");
        var products = await dbContext.Products.Where(product => product.CompanyId == companyId && productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);
        if (products.Count != productIds.Distinct().Count()) throw new SalesValidationException("One or more invoice products were not found.");

        var stockLines = invoice.Lines.Where(line => products[line.ProductId!.Value].IsStockTracked).OrderBy(line => line.ProductId).ToArray();
        Warehouse? warehouse = null;
        if (stockLines.Length > 0)
        {
            if (!invoice.WarehouseId.HasValue) throw new SalesValidationException("A warehouse is required before posting stock-controlled products.");
            warehouse = await dbContext.Warehouses.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == invoice.WarehouseId.Value, cancellationToken)
                ?? throw new SalesValidationException("The selected warehouse was not found.");
            if (!warehouse.IsActive) throw new SalesValidationException("The selected warehouse is inactive.");
        }

        foreach (var line in invoice.Lines)
        {
            var product = products[line.ProductId!.Value];
            if (!product.IsActive) throw new SalesValidationException($"Product '{product.Name}' is inactive.");
            if (!product.StockUnitOfMeasureId.HasValue || product.StockUnitOfMeasureId != line.Quantity.UnitOfMeasureId)
            {
                throw new SalesValidationException($"Product '{product.Name}' requires its configured unit of measure.");
            }
        }

        var postedAt = DateTimeOffset.UtcNow;
        invoice.MarkPosted(userId, postedAt);
        if (warehouse is not null)
        {
            foreach (var line in stockLines)
            {
                await inventoryService.RecordSystemStockOutAsync(companyId, userId, new InventorySystemStockOutInput(
                    line.ProductId!.Value,
                    warehouse.Id,
                    line.Quantity.Amount,
                    postedAt,
                    new SourceReference("Sales", invoice.Id, "SalesInvoicePosted"),
                    invoice.DocumentNumber.Value,
                    $"Sales invoice {invoice.DocumentNumber.Value}"), cancellationToken);
            }
        }

        dbContext.OpenItems.Add(new OpenItem(
            companyId,
            invoice.CustomerId,
            OpenItemType.Receivable,
            new SourceReference("Sales", invoice.Id, "SalesInvoicePosted"),
            invoice.DocumentNumber,
            invoice.TotalAmount,
            dueDate: invoice.DueDate));
        var accountingTransaction = new AccountingTransaction(companyId, new SourceReference("Sales", invoice.Id, "SalesInvoicePosted"), invoice.InvoiceDate);
        dbContext.AccountingTransactions.Add(accountingTransaction);
        await dbContext.SaveChangesAsync(cancellationToken);
        await accountingService.PostOperationalTransactionIfConfiguredAsync(companyId, accountingTransaction.Id, userId, cancellationToken);
        return true;
    }

    private async Task<(Company Company, BusinessPartner Customer, IReadOnlyDictionary<Guid, Product> Products)> ValidateInputAsync(Guid companyId, SalesInvoiceInput input, CancellationToken cancellationToken)
    {
        if (input.CustomerId == Guid.Empty) throw new SalesValidationException("A customer is required.");
        if (input.DueDate.HasValue && input.DueDate.Value < input.InvoiceDate) throw new SalesValidationException("The due date cannot be before the invoice date.");
        var company = await dbContext.Companies.AsNoTracking().SingleOrDefaultAsync(item => item.Id == companyId && item.IsActive, cancellationToken)
            ?? throw new SalesValidationException("The selected company is unavailable.");
        var customer = await dbContext.BusinessPartners.AsNoTracking().Include(partner => partner.CustomerProfile)
            .SingleOrDefaultAsync(partner => partner.CompanyId == companyId && partner.Id == input.CustomerId, cancellationToken)
            ?? throw new SalesValidationException("The selected customer was not found.");
        if (!customer.IsActive || customer.CustomerProfile is null) throw new SalesValidationException("The selected customer is unavailable for sales.");
        if (input.WarehouseId.HasValue && !await dbContext.Warehouses.AnyAsync(warehouse => warehouse.CompanyId == companyId && warehouse.Id == input.WarehouseId && warehouse.IsActive, cancellationToken))
        {
            throw new SalesValidationException("The selected active warehouse was not found.");
        }

        var lineInputs = input.Lines ?? Array.Empty<SalesInvoiceLineInput>();
        if (lineInputs.Any(line => line.ProductId == Guid.Empty || line.Quantity <= 0 || line.UnitPrice < 0 || line.DiscountPercentage is < 0 or > 100))
        {
            throw new SalesValidationException("Invoice lines require a product, positive quantity, non-negative price, and a valid discount.");
        }
        var productIds = lineInputs.Select(line => line.ProductId).Distinct().ToArray();
        var products = productIds.Length == 0
            ? new Dictionary<Guid, Product>()
            : await dbContext.Products.AsNoTracking().Where(product => product.CompanyId == companyId && productIds.Contains(product.Id)).ToDictionaryAsync(product => product.Id, cancellationToken);
        if (products.Count != productIds.Length) throw new SalesValidationException("One or more invoice products were not found.");
        foreach (var product in products.Values)
        {
            if (!product.IsActive || !product.StockUnitOfMeasureId.HasValue)
            {
                throw new SalesValidationException($"Product '{product.Name}' must be active and have a unit of measure before it can be invoiced.");
            }
        }

        return (company, customer, products);
    }

    private static IReadOnlyCollection<SalesInvoiceLine> CreateLines(IReadOnlyCollection<SalesInvoiceLineInput> inputs, IReadOnlyDictionary<Guid, Product> products, string currencyCode) =>
        inputs.Select((input, index) =>
        {
            var product = products[input.ProductId];
            return new SalesInvoiceLine(
                index + 1,
                string.IsNullOrWhiteSpace(input.Description) ? product.Name : input.Description,
                new Quantity(input.Quantity, product.StockUnitOfMeasureId!.Value),
                new Money(input.UnitPrice, currencyCode),
                productId: product.Id,
                discountPercentage: input.DiscountPercentage);
        }).ToArray();

    private async Task<IReadOnlyCollection<SalesInvoiceDto>> ToDtosAsync(IReadOnlyCollection<SalesInvoice> invoices, CancellationToken cancellationToken)
    {
        if (invoices.Count == 0) return Array.Empty<SalesInvoiceDto>();

        var customerIds = invoices.Select(invoice => invoice.CustomerId).Distinct().ToArray();
        var warehouseIds = invoices.Where(invoice => invoice.WarehouseId.HasValue).Select(invoice => invoice.WarehouseId!.Value).Distinct().ToArray();
        var userIds = invoices.SelectMany(invoice => new[] { invoice.CreatedByUserId, invoice.PostedByUserId }).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        var productIds = invoices.SelectMany(invoice => invoice.Lines).Where(line => line.ProductId.HasValue).Select(line => line.ProductId!.Value).Distinct().ToArray();
        var unitIds = invoices.SelectMany(invoice => invoice.Lines).Select(line => line.Quantity.UnitOfMeasureId).Distinct().ToArray();
        var invoiceIds = invoices.Select(invoice => invoice.Id).ToArray();

        var customers = await dbContext.BusinessPartners.AsNoTracking().Where(customer => customerIds.Contains(customer.Id)).ToDictionaryAsync(customer => customer.Id, cancellationToken);
        var warehouses = warehouseIds.Length == 0 ? new Dictionary<Guid, Warehouse>() : await dbContext.Warehouses.AsNoTracking().Where(warehouse => warehouseIds.Contains(warehouse.Id)).ToDictionaryAsync(warehouse => warehouse.Id, cancellationToken);
        var users = userIds.Length == 0 ? new Dictionary<Guid, string>() : await dbContext.Users.AsNoTracking().Where(user => userIds.Contains(user.Id)).ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        var products = productIds.Length == 0 ? new Dictionary<Guid, Product>() : await dbContext.Products.AsNoTracking().Where(product => productIds.Contains(product.Id)).ToDictionaryAsync(product => product.Id, cancellationToken);
        var units = unitIds.Length == 0 ? new Dictionary<Guid, string>() : await dbContext.UnitsOfMeasure.AsNoTracking().Where(unit => unitIds.Contains(unit.Id)).ToDictionaryAsync(unit => unit.Id, unit => unit.Name, cancellationToken);
        var openItems = await dbContext.OpenItems.AsNoTracking()
            .Where(item => item.CompanyId == invoices.First().CompanyId && item.Type == OpenItemType.Receivable && invoiceIds.Contains(item.SourceReference.AggregateId))
            .ToDictionaryAsync(item => item.SourceReference.AggregateId, cancellationToken);

        return invoices.Select(invoice =>
        {
            var customer = customers[invoice.CustomerId];
            warehouses.TryGetValue(invoice.WarehouseId ?? Guid.Empty, out var warehouse);
            var total = invoice.TotalAmount.Amount;
            var outstanding = invoice.Status == SalesInvoiceStatus.Posted && openItems.TryGetValue(invoice.Id, out var openItem) ? openItem.OutstandingAmount.Amount : 0m;
            SalesInvoicePaymentStatus? paymentStatus = invoice.Status == SalesInvoiceStatus.Posted
                ? outstanding == 0 ? SalesInvoicePaymentStatus.Paid : outstanding < total ? SalesInvoicePaymentStatus.PartiallyPaid : SalesInvoicePaymentStatus.Unpaid
                : null;
            return new SalesInvoiceDto(
                invoice.Id,
                invoice.DocumentNumber.Value,
                invoice.CustomerId,
                customer.Code,
                customer.LegalName,
                invoice.WarehouseId,
                warehouse?.Code,
                warehouse?.Name,
                invoice.InvoiceDate,
                invoice.DueDate,
                invoice.CurrencyCode,
                invoice.Status,
                paymentStatus,
                invoice.Subtotal.Amount,
                invoice.DiscountTotal.Amount,
                total,
                outstanding,
                invoice.Reference,
                invoice.Notes,
                invoice.CreatedByUserId.HasValue && users.TryGetValue(invoice.CreatedByUserId.Value, out var createdBy) ? createdBy : null,
                invoice.CreatedAt,
                invoice.PostedByUserId.HasValue && users.TryGetValue(invoice.PostedByUserId.Value, out var postedBy) ? postedBy : null,
                invoice.PostedAt,
                invoice.Lines.Select(line =>
                {
                    var product = products[line.ProductId!.Value];
                    return new SalesInvoiceLineDto(line.Id, line.LineNumber, product.Id, product.Sku, product.Name, line.Description, line.Quantity.Amount, line.Quantity.UnitOfMeasureId, units[line.Quantity.UnitOfMeasureId], line.UnitPrice.Amount, line.DiscountPercentage, line.DiscountAmount, line.NetAmount);
                }).ToArray());
        }).ToArray();
    }

    private async Task<string> NextInvoiceNumberAsync(DateOnly invoiceDate, CancellationToken cancellationToken)
    {
        long nextValue;
        if (!dbContext.Database.IsRelational())
        {
            nextValue = await dbContext.SalesInvoices.CountAsync(cancellationToken) + 1;
        }
        else
        {
            var connection = dbContext.Database.GetDbConnection();
            var closeConnection = connection.State != ConnectionState.Open;
            if (closeConnection) await dbContext.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT nextval('erp.sales_invoice_number_sequence')";
                command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
                nextValue = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
            }
            finally
            {
                if (closeConnection) await dbContext.Database.CloseConnectionAsync();
            }
        }

        return $"SI-{invoiceDate.Year}-{nextValue:D6}";
    }

    private static SalesInvoiceQuery Normalize(SalesInvoiceQuery query) => query with { PageNumber = Math.Max(query.PageNumber, 1), PageSize = Math.Clamp(query.PageSize, 1, 100) };
}
