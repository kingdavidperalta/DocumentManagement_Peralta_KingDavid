using DocumentManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentManagement.Api.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.HasKey(document => document.Id);

        builder.Property(document => document.OriginalFileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(document => document.StoredFileName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(document => document.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(document => document.FileSize)
            .IsRequired();

        builder.Property(document => document.UploadedAt)
            .IsRequired();

        builder.HasIndex(document => document.StoredFileName)
            .IsUnique();
    }
}