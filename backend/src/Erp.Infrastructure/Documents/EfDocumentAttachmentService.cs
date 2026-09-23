using Erp.Application.Documents;
using Erp.Domain.Accounting;
using Erp.Domain.Common;
using Erp.Domain.Documents;
using Erp.Infrastructure.Persistence;
using Erp.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Documents;

public sealed class EfDocumentAttachmentService(ErpDbContext dbContext, IAttachmentStorage storage) : IDocumentAttachmentService
{
    private const long MaximumFileSize = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "text/plain",
        "text/csv",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };

    public async Task<IReadOnlyCollection<DocumentAttachmentDto>> GetAsync(Guid companyId, string documentType, Guid documentId, CancellationToken cancellationToken = default)
    {
        var source = await ResolveSourceAsync(companyId, documentType, documentId, cancellationToken);
        return await dbContext.DocumentAttachments.AsNoTracking()
            .Where(attachment => attachment.SourceReference.Module == source.Module
                && attachment.SourceReference.AggregateId == source.AggregateId
                && attachment.SourceReference.EventType == source.EventType)
            .OrderByDescending(attachment => attachment.UploadedAt)
            .Select(attachment => new DocumentAttachmentDto(
                attachment.Id,
                NormalizeDocumentType(documentType),
                documentId,
                attachment.OriginalFileName,
                attachment.ContentType,
                attachment.FileSize,
                attachment.UploadedAt,
                attachment.UploadedByUserId))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<DocumentAttachmentDto> UploadAsync(Guid companyId, Guid uploadedByUserId, string documentType, Guid documentId, string originalFileName, string contentType, long fileSize, Stream content, CancellationToken cancellationToken = default)
    {
        if (uploadedByUserId == Guid.Empty) throw new DocumentAttachmentValidationException("The authenticated user could not be identified.");
        ValidateUpload(originalFileName, contentType, fileSize);
        var source = await ResolveSourceAsync(companyId, documentType, documentId, cancellationToken);
        StoredAttachment? stored = null;

        try
        {
            stored = await storage.StoreAsync(content, originalFileName, contentType, cancellationToken);
            var attachment = new DocumentAttachment(source, Path.GetFileName(originalFileName), stored.StorageKey, stored.ContentType, stored.FileSize, uploadedByUserId, DateTimeOffset.UtcNow);
            dbContext.DocumentAttachments.Add(attachment);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToDto(attachment, NormalizeDocumentType(documentType), documentId);
        }
        catch
        {
            if (stored is not null) await storage.DeleteAsync(stored.StorageKey, cancellationToken);
            throw;
        }
    }

    public async Task<AttachmentContent?> DownloadAsync(Guid companyId, string documentType, Guid documentId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var source = await ResolveSourceAsync(companyId, documentType, documentId, cancellationToken);
        var attachment = await FindAttachmentAsync(source, attachmentId, cancellationToken);
        if (attachment is null) return null;
        var stream = await storage.OpenReadAsync(attachment.StorageKey, cancellationToken);
        if (stream is null) throw new DocumentAttachmentConflictException("The attachment file is no longer available.");
        return new AttachmentContent(attachment.OriginalFileName, attachment.ContentType, stream);
    }

    public async Task<bool> DeleteAsync(Guid companyId, string documentType, Guid documentId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var normalizedType = NormalizeDocumentType(documentType);
        if (!string.Equals(normalizedType, "Journal", StringComparison.Ordinal))
        {
            throw new DocumentAttachmentConflictException("Attachments can only be deleted from draft manual journals. Posted and operational documents retain their audit attachments.");
        }

        var journal = await dbContext.JournalEntries.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == documentId, cancellationToken);
        if (journal is null) throw new DocumentAttachmentValidationException("The journal entry was not found.");
        if (journal.Status != JournalEntryStatus.Draft) throw new DocumentAttachmentConflictException("Attachments cannot be deleted after a journal entry is posted.");

        var attachment = await FindAttachmentAsync(new SourceReference("Accounting", documentId, "ManualJournal"), attachmentId, cancellationToken);
        if (attachment is null) return false;
        await storage.DeleteAsync(attachment.StorageKey, cancellationToken);
        dbContext.DocumentAttachments.Remove(attachment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<DocumentAttachment?> FindAttachmentAsync(SourceReference source, Guid attachmentId, CancellationToken cancellationToken) =>
        await dbContext.DocumentAttachments.SingleOrDefaultAsync(attachment => attachment.Id == attachmentId
            && attachment.SourceReference.Module == source.Module
            && attachment.SourceReference.AggregateId == source.AggregateId
            && attachment.SourceReference.EventType == source.EventType, cancellationToken);

    private async Task<SourceReference> ResolveSourceAsync(Guid companyId, string documentType, Guid documentId, CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty || documentId == Guid.Empty) throw new DocumentAttachmentValidationException("A company and document are required.");

        return NormalizeDocumentType(documentType) switch
        {
            "Journal" when await dbContext.JournalEntries.AnyAsync(item => item.CompanyId == companyId && item.Id == documentId, cancellationToken)
                => new SourceReference("Accounting", documentId, "ManualJournal"),
            "SalesInvoice" when await dbContext.SalesInvoices.AnyAsync(item => item.CompanyId == companyId && item.Id == documentId, cancellationToken)
                => new SourceReference("Sales", documentId, "SalesInvoice"),
            "CustomerPayment" when await dbContext.Payments.AnyAsync(item => item.CompanyId == companyId && item.Id == documentId && item.Direction == PaymentDirection.Incoming, cancellationToken)
                => new SourceReference("Payments", documentId, "CustomerPayment"),
            "PurchaseBill" when await dbContext.PurchaseInvoices.AnyAsync(item => item.CompanyId == companyId && item.Id == documentId, cancellationToken)
                => new SourceReference("Purchasing", documentId, "PurchaseInvoice"),
            "SupplierPayment" when await dbContext.Payments.AnyAsync(item => item.CompanyId == companyId && item.Id == documentId && item.Direction == PaymentDirection.Outgoing, cancellationToken)
                => new SourceReference("Purchasing", documentId, "SupplierPayment"),
            _ => throw new DocumentAttachmentValidationException("The requested document was not found or is not supported for attachments.")
        };
    }

    private static void ValidateUpload(string originalFileName, string contentType, long fileSize)
    {
        if (string.IsNullOrWhiteSpace(originalFileName)) throw new DocumentAttachmentValidationException("A file name is required.");
        if (fileSize <= 0 || fileSize > MaximumFileSize) throw new DocumentAttachmentValidationException("Attachments must be between 1 byte and 10 MB.");
        if (!AllowedContentTypes.Contains(contentType)) throw new DocumentAttachmentValidationException("This file type is not allowed.");
    }

    private static string NormalizeDocumentType(string documentType) => documentType.Trim().ToLowerInvariant() switch
    {
        "journal" or "journalentry" => "Journal",
        "salesinvoice" or "sales-invoice" => "SalesInvoice",
        "customerpayment" or "customer-payment" => "CustomerPayment",
        "purchasebill" or "purchaseinvoice" or "purchase-bill" => "PurchaseBill",
        "supplierpayment" or "supplier-payment" => "SupplierPayment",
        _ => throw new DocumentAttachmentValidationException("The document type is not supported for attachments.")
    };

    private static DocumentAttachmentDto ToDto(DocumentAttachment attachment, string documentType, Guid documentId) =>
        new(attachment.Id, documentType, documentId, attachment.OriginalFileName, attachment.ContentType, attachment.FileSize, attachment.UploadedAt, attachment.UploadedByUserId);
}
