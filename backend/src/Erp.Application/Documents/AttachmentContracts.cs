namespace Erp.Application.Documents;

public sealed record StoredAttachment(string StorageKey, string ContentType, long FileSize);

public sealed record DocumentAttachmentDto(
    Guid Id,
    string DocumentType,
    Guid DocumentId,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    DateTimeOffset UploadedAt,
    Guid UploadedByUserId);

public sealed record AttachmentContent(string OriginalFileName, string ContentType, Stream Content);

public interface IDocumentAttachmentService
{
    Task<IReadOnlyCollection<DocumentAttachmentDto>> GetAsync(Guid companyId, string documentType, Guid documentId, CancellationToken cancellationToken = default);
    Task<DocumentAttachmentDto> UploadAsync(Guid companyId, Guid uploadedByUserId, string documentType, Guid documentId, string originalFileName, string contentType, long fileSize, Stream content, CancellationToken cancellationToken = default);
    Task<AttachmentContent?> DownloadAsync(Guid companyId, string documentType, Guid documentId, Guid attachmentId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid companyId, string documentType, Guid documentId, Guid attachmentId, CancellationToken cancellationToken = default);
}

public interface IAttachmentStorage
{
    Task<StoredAttachment> StoreAsync(Stream content, string originalFileName, string contentType, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
