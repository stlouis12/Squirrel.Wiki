using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Configurations;

public class FileContentConfiguration : IEntityTypeConfiguration<FileContent>
{
    public void Configure(EntityTypeBuilder<FileContent> builder)
    {
        builder.HasKey(c => c.FileHash);

        builder.Property(c => c.FileHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(c => c.StoragePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(c => c.StorageProvider)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Local");

        builder.Property(c => c.ReferenceCount)
            .IsRequired()
            .HasDefaultValue(0);

        // Indexes
        builder.HasIndex(c => c.StorageProvider);

        // Relationships
        builder.HasMany(c => c.Files)
            .WithOne(f => f.Content)
            .HasForeignKey(f => f.FileHash)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
