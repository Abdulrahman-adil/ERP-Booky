using System.ComponentModel.DataAnnotations;

namespace Erp.Api.Contracts.Documents;

public sealed class AttachmentUploadRequest
{
    [Required]
    public IFormFile? File { get; init; }
}
