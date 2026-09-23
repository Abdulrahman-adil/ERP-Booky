using Erp.Domain.Common;

namespace Erp.Domain.Documents;

#pragma warning disable CS8618

public sealed class DocumentAttachment : Entity
{
    private DocumentAttachment()
    {
    }

    public DocumentAttachment(
        SourceReference sourceReference,
        string originalFileName,
        string storageKey,
        string contentType,
        long fileSize,
        Guid uploadedByUserId,
        DateTimeOffset uploadedAt,
        Guid id = default)
        : base(id)
    {
        if (fileSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fileSize), "Attachment file size must be positive.");
        }
        if (uploadedByUserId == Guid.Empty)
        {
            throw new ArgumentException("An uploading user is required.", nameof(uploadedByUserId));
        }

        SourceReference = sourceReference ?? throw new ArgumentNullException(nameof(sourceReference));
        OriginalFileName = Money.RequireText(originalFileName, nameof(originalFileName));
        StorageKey = Money.RequireText(storageKey, nameof(storageKey));
        ContentType = Money.RequireText(contentType, nameof(contentType));
        FileSize = fileSize;
        UploadedByUserId = uploadedByUserId;
        UploadedAt = uploadedAt;
    }

    public SourceReference SourceReference { get; private set; }

    public string OriginalFileName { get; private set; }

    public string StorageKey { get; private set; }

    public string ContentType { get; private set; }

    public long FileSize { get; private set; }

    public Guid UploadedByUserId { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }
}

#pragma warning restore CS8618
