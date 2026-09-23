using System.Security.Claims;
using Erp.Application.Authentication;
using Erp.Application.Documents;
using Erp.Api.Contracts.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

[ApiController, Route("api/documents"), Tags("Document Attachments")]
public sealed class DocumentAttachmentsController(IDocumentAttachmentService attachments) : CompanyScopedControllerBase
{
    [HttpGet("{documentType}/{documentId:guid}/attachments"), Authorize(Policy = "permission:documents.view")]
    public async Task<ActionResult<IReadOnlyCollection<DocumentAttachmentDto>>> List(string documentType, Guid documentId, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await attachments.GetAsync(companyId, documentType, documentId, cancellationToken));
    }

    [HttpPost("{documentType}/{documentId:guid}/attachments"), Consumes("multipart/form-data"), Authorize(Policy = "permission:documents.manage")]
    public async Task<ActionResult<DocumentAttachmentDto>> Upload(string documentType, Guid documentId, [FromForm] AttachmentUploadRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId) || !TryUser(out var userId)) return Forbid();
        var file = request.File;
        if (file is null) return BadRequest(new ProblemDetails { Title = "Select a file to upload.", Status = StatusCodes.Status400BadRequest });

        await using var content = file.OpenReadStream();
        var attachment = await attachments.UploadAsync(companyId, userId, documentType, documentId, file.FileName, file.ContentType, file.Length, content, cancellationToken);
        return CreatedAtAction(nameof(List), new { documentType, documentId }, attachment);
    }

    [HttpGet("{documentType}/{documentId:guid}/attachments/{attachmentId:guid}/download"), Authorize(Policy = "permission:documents.view")]
    public async Task<IActionResult> Download(string documentType, Guid documentId, Guid attachmentId, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var attachment = await attachments.DownloadAsync(companyId, documentType, documentId, attachmentId, cancellationToken);
        return attachment is null ? NotFound() : File(attachment.Content, attachment.ContentType, attachment.OriginalFileName);
    }

    [HttpDelete("{documentType}/{documentId:guid}/attachments/{attachmentId:guid}"), Authorize(Policy = "permission:documents.manage")]
    public async Task<IActionResult> Delete(string documentType, Guid documentId, Guid attachmentId, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return await attachments.DeleteAsync(companyId, documentType, documentId, attachmentId, cancellationToken) ? NoContent() : NotFound();
    }
}
