using Erp.Domain.Common;

namespace Erp.Domain.Purchasing;

#pragma warning disable CS8618

public enum PurchaseInvoiceStatus
{
    Draft,
    Posted,
    Cancelled
}

public enum PurchaseInvoicePaymentStatus
{
    Unpaid,
    PartiallyPaid,
    Paid
}

public sealed class PurchaseInvoice : AggregateRoot
{
    private readonly List<PurchaseInvoiceLine> _lines = [];

    private PurchaseInvoice()
    {
    }

    public PurchaseInvoice(
        Guid companyId,
        Guid supplierId,
        DocumentNumber documentNumber,
        DateOnly invoiceDate,
        string currencyCode,
        DateOnly? dueDate = null,
        Guid? warehouseId = null,
        string? supplierReference = null,
        string? notes = null,
        Guid? createdByUserId = null,
        DateTimeOffset? createdAt = null,
        SourceReference? sourceReference = null,
        Guid id = default)
        : base(id)
    {
        ValidateHeader(companyId, supplierId, invoiceDate, dueDate);
        CompanyId = companyId;
        SupplierId = supplierId;
        DocumentNumber = documentNumber ?? throw new ArgumentNullException(nameof(documentNumber));
        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        WarehouseId = warehouseId;
        CurrencyCode = new Money(0, currencyCode).CurrencyCode;
        SupplierReference = NormalizeOptionalText(supplierReference);
        Notes = NormalizeOptionalText(notes);
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
        SourceReference = sourceReference;
        Status = PurchaseInvoiceStatus.Draft;
    }

    public Guid CompanyId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid? WarehouseId { get; private set; }
    public DocumentNumber DocumentNumber { get; private set; }
    public DateOnly InvoiceDate { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public string CurrencyCode { get; private set; }
    public string? SupplierReference { get; private set; }
    public string? Notes { get; private set; }
    public SourceReference? SourceReference { get; private set; }
    public PurchaseInvoiceStatus Status { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public DateTimeOffset? CreatedAt { get; private set; }
    public Guid? PostedByUserId { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }
    public IReadOnlyCollection<PurchaseInvoiceLine> Lines => _lines.AsReadOnly();
    public Money Subtotal => new(_lines.Sum(line => line.SubtotalAmount), CurrencyCode);
    public Money DiscountTotal => new(_lines.Sum(line => line.DiscountAmount), CurrencyCode);
    public Money TotalAmount => new(_lines.Sum(line => line.NetAmount), CurrencyCode);

    public void UpdateDraft(Guid supplierId, Guid? warehouseId, DateOnly invoiceDate, DateOnly? dueDate, string? supplierReference, string? notes)
    {
        EnsureDraft();
        ValidateHeader(CompanyId, supplierId, invoiceDate, dueDate);
        SupplierId = supplierId;
        WarehouseId = warehouseId;
        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        SupplierReference = NormalizeOptionalText(supplierReference);
        Notes = NormalizeOptionalText(notes);
    }

    public void ReplaceLines(IEnumerable<PurchaseInvoiceLine> lines)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(lines);
        var replacement = lines.ToArray();
        if (replacement.Any(line => !string.Equals(line.UnitCost.CurrencyCode, CurrencyCode, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Purchase line currency must match the purchase bill currency.", nameof(lines));
        }
        if (replacement.GroupBy(line => line.LineNumber).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Purchase line numbers must be unique.", nameof(lines));
        }

        _lines.Clear();
        _lines.AddRange(replacement.OrderBy(line => line.LineNumber));
    }

    public void MarkPosted(Guid postedByUserId, DateTimeOffset postedAt)
    {
        EnsureDraft();
        if (_lines.Count == 0) throw new InvalidOperationException("A purchase bill requires at least one line before posting.");
        if (postedByUserId == Guid.Empty) throw new ArgumentException("The posting user is required.", nameof(postedByUserId));
        Status = PurchaseInvoiceStatus.Posted;
        PostedByUserId = postedByUserId;
        PostedAt = postedAt;
    }

    private void EnsureDraft()
    {
        if (Status != PurchaseInvoiceStatus.Draft) throw new InvalidOperationException("Only draft purchase bills can be changed.");
    }

    private static void ValidateHeader(Guid companyId, Guid supplierId, DateOnly invoiceDate, DateOnly? dueDate)
    {
        if (companyId == Guid.Empty || supplierId == Guid.Empty) throw new ArgumentException("Company and supplier are required.");
        if (dueDate is not null && dueDate < invoiceDate) throw new ArgumentException("The due date cannot be before the bill date.", nameof(dueDate));
    }

    private static string? NormalizeOptionalText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class PurchaseInvoiceLine : Entity
{
    private PurchaseInvoiceLine()
    {
    }

    public PurchaseInvoiceLine(
        int lineNumber,
        string description,
        Quantity quantity,
        Money unitCost,
        Guid? productId = null,
        decimal discountPercentage = 0,
        TaxSnapshot? tax = null,
        string? memo = null,
        Guid? postingProfileId = null,
        Guid id = default)
        : base(id)
    {
        if (lineNumber <= 0) throw new ArgumentOutOfRangeException(nameof(lineNumber));
        if (quantity is null || quantity.Amount <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Purchase line quantity must be positive.");
        if (unitCost is null || unitCost.Amount < 0) throw new ArgumentOutOfRangeException(nameof(unitCost), "Purchase line costs cannot be negative.");
        if (discountPercentage is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(discountPercentage));

        LineNumber = lineNumber;
        ProductId = productId;
        Description = Money.RequireText(description, nameof(description));
        Quantity = quantity;
        UnitCost = unitCost;
        DiscountPercentage = discountPercentage;
        Tax = tax;
        Memo = string.IsNullOrWhiteSpace(memo) ? null : memo.Trim();
        PostingProfileId = postingProfileId;
    }

    public int LineNumber { get; private set; }
    public Guid? ProductId { get; private set; }
    public string Description { get; private set; }
    public Quantity Quantity { get; private set; }
    public Money UnitCost { get; private set; }
    public decimal DiscountPercentage { get; private set; }
    public TaxSnapshot? Tax { get; private set; }
    public string? Memo { get; private set; }
    public Guid? PostingProfileId { get; private set; }
    public decimal SubtotalAmount => RoundMoney(Quantity.Amount * UnitCost.Amount);
    public decimal DiscountAmount => RoundMoney(SubtotalAmount * DiscountPercentage / 100m);
    public decimal NetAmount => RoundMoney(SubtotalAmount - DiscountAmount);

    private static decimal RoundMoney(decimal amount) => decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
}

#pragma warning restore CS8618
