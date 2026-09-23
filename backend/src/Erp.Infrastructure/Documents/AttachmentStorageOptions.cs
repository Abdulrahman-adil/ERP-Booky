namespace Erp.Infrastructure.Documents;

public sealed class AttachmentStorageOptions
{
    public const string SectionName = "Attachments";

    public string StoragePath { get; init; } = "App_Data/attachments";
}
