using Erp.Domain.Common;
using Erp.Domain.Documents;
using Erp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Persistence.Configurations;

internal sealed class DocumentAttachmentConfiguration : IEntityTypeConfiguration<DocumentAttachment>
{
    public void Configure(EntityTypeBuilder<DocumentAttachment> builder)
    {
        builder.ConfigureEntity("document_attachments");
        builder.Property(attachment => attachment.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(attachment => attachment.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(attachment => attachment.ContentType).HasMaxLength(255).IsRequired();
        builder.Property(attachment => attachment.FileSize).IsRequired();
        builder.Property(attachment => attachment.UploadedByUserId).IsRequired();
        builder.Property(attachment => attachment.UploadedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(attachment => attachment.StorageKey).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(attachment => attachment.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(attachment => attachment.SourceReference, reference => reference.ConfigureSourceReference(false));
        builder.Navigation(attachment => attachment.SourceReference).IsRequired();
    }
}
