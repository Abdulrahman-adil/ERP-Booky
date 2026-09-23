namespace Erp.Application.Documents;

public sealed class DocumentAttachmentValidationException(string message) : Exception(message);

public sealed class DocumentAttachmentConflictException(string message) : Exception(message);
