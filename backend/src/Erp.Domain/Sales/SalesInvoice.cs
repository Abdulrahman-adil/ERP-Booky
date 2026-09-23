using Erp.Domain.Common;

namespace Erp.Domain.Sales;

#pragma warning disable CS8618

public enum SalesInvoiceStatus
{
    Draft,
    Posted,
    Cancelled
}

public enum SalesInvoicePaymentStatus
{
    Unpaid,
    PartiallyPaid,
    Paid
}

public sealed class SalesInvoice : AggregateRoot
{
    private readonly List<SalesInvoiceLine> _lines = [];

    private SalesInvoice()
    {
    }

    public SalesInvoice(
        Guid companyId,
        Guid customerId,
        DocumentNumber documentNumber,
        DateOnly invoiceDate,
        string currencyCode,
        DateOnly? dueDate = null,
        Guid? warehouseId = null,
        string? reference = null,
        string? notes = null,
        Guid? createdByUserId = null,
        DateTimeOffset? createdAt = null,
        SourceReference? sourceReference = null,
        Guid id = default)
        : base(id)
    {
        ValidateHeader(companyId, customerId, invoiceDate, dueDate);

        CompanyId = companyId;
        CustomerId = customerId;
        DocumentNumber = documentNumber ?? throw new ArgumentNullException(nameof(documentNumber));
        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        WarehouseId = warehouseId;
        CurrencyCode = new Money(0, currencyCode).CurrencyCode;
        Reference = NormalizeOptionalText(reference);
        Notes = NormalizeOptionalText(notes);
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
        SourceReference = sourceReference;
        Status = SalesInvoiceStatus.Draft;
    }

    public Guid CompanyId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid? WarehouseId { get; private set; }

    public DocumentNumber DocumentNumber { get; private set; }

    public DateOnly InvoiceDate { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public string CurrencyCode { get; private set; }

    public string? Reference { get; private set; }

    public string? Notes { get; private set; }

    public SourceReference? SourceReference { get; private set; }

    public SalesInvoiceStatus Status { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public DateTimeOffset? CreatedAt { get; private set; }

    public Guid? PostedByUserId { get; private set; }

    public DateTimeOffset? PostedAt { get; private set; }

    public IReadOnlyCollection<SalesInvoiceLine> Lines => _lines.AsReadOnly();

    public Money Subtotal => new(_lines.Sum(line => line.SubtotalAmount), CurrencyCode);

    public Money DiscountTotal => new(_lines.Sum(line => line.DiscountAmount), CurrencyCode);

    public Money TotalAmount => new(_lines.Sum(line => line.NetAmount), CurrencyCode);

    public void UpdateDraft(Guid customerId, Guid? warehouseId, DateOnly invoiceDate, DateOnly? dueDate, string? reference, string? notes)
    {
        EnsureDraft();
        ValidateHeader(CompanyId, customerId, invoiceDate, dueDate);

        CustomerId = customerId;
        WarehouseId = warehouseId;
        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        Reference = NormalizeOptionalText(reference);
        Notes = NormalizeOptionalText(notes);
    }

    public void ReplaceLines(IEnumerable<SalesInvoiceLine> lines)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(lines);

        var replacement = lines.ToList();
        if (replacement.Any(line => !string.Equals(line.UnitPrice.CurrencyCode, CurrencyCode, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Invoice line currency must match the invoice currency.", nameof(lines));
        }
        if (replacement.GroupBy(line => line.LineNumber).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Invoice line numbers must be unique.", nameof(lines));
        }

        _lines.Clear();
        _lines.AddRange(replacement.OrderBy(line => line.LineNumber));
    }

    public void AddLine(SalesInvoiceLine line)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(line);
        if (!string.Equals(line.UnitPrice.CurrencyCode, CurrencyCode, StringComparison.Ordinal))
        {
            throw new ArgumentException("Invoice line currency must match the invoice currency.", nameof(line));
        }
        if (_lines.Any(existingLine => existingLine.LineNumber == line.LineNumber))
        {
            throw new ArgumentException("Invoice line numbers must be unique.", nameof(line));
        }

        _lines.Add(line);
    }

    public void MarkPosted(Guid postedByUserId, DateTimeOffset postedAt)
    {
        EnsureDraft();
        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("An invoice requires at least one line before posting.");
        }
        if (postedByUserId == Guid.Empty)
        {
            throw new ArgumentException("The posting user is required.", nameof(postedByUserId));
        }

        Status = SalesInvoiceStatus.Posted;
        PostedByUserId = postedByUserId;
        PostedAt = postedAt;
    }

    private void EnsureDraft()
    {
        if (Status != SalesInvoiceStatus.Draft)
        {
            throw new InvalidOperationException("Only draft invoices can be changed.");
        }
    }

    private static void ValidateHeader(Guid companyId, Guid customerId, DateOnly invoiceDate, DateOnly? dueDate)
    {
        if (companyId == Guid.Empty || customerId == Guid.Empty)
        {
            throw new ArgumentException("Company and customer are required.");
        }
        if (dueDate is not null && dueDate < invoiceDate)
        {
            throw new ArgumentException("The due date cannot be before the invoice date.", nameof(dueDate));
        }
    }

    private static string? NormalizeOptionalText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class SalesInvoiceLine : Entity
{
    private SalesInvoiceLine()
    {
    }

    public SalesInvoiceLine(
        int lineNumber,
        string description,
        Quantity quantity,
        Money unitPrice,
        TaxSnapshot? tax = null,
        Guid? productId = null,
        decimal discountPercentage = 0,
        Guid id = default)
        : base(id)
    {
        if (lineNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber));
        }
        if (quantity is null || quantity.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Invoice line quantity must be positive.");
        }
        if (unitPrice is null || unitPrice.Amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Invoice line prices cannot be negative.");
        }
        if (discountPercentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(discountPercentage));
        }

        LineNumber = lineNumber;
        ProductId = productId;
        Description = Money.RequireText(description, nameof(description));
        Quantity = quantity;
        UnitPrice = unitPrice;
        Tax = tax;
        DiscountPercentage = discountPercentage;
    }

    public int LineNumber { get; private set; }

    public Guid? ProductId { get; private set; }

    public string Description { get; private set; }

    public Quantity Quantity { get; private set; }

    public Money UnitPrice { get; private set; }

    public TaxSnapshot? Tax { get; private set; }

    public decimal DiscountPercentage { get; private set; }

    public decimal SubtotalAmount => RoundMoney(Quantity.Amount * UnitPrice.Amount);

    public decimal DiscountAmount => RoundMoney(SubtotalAmount * DiscountPercentage / 100m);

    public decimal NetAmount => RoundMoney(SubtotalAmount - DiscountAmount);

    private static decimal RoundMoney(decimal amount) => decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
}

#pragma warning restore CS8618
