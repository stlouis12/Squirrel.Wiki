using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Configurations;

public class FileVersionConfiguration : IEntityTypeConfiguration<FileVersion>
{
    public void Configure(EntityTypeBuilder<FileVersion> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.FileHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(v => v.CreatedBy)
            .IsRequired()
            .HasMaxLength(255);

        // Indexes
        builder.HasIndex(v => new { v.FileId, v.VersionNumber })
            .IsUnique();

        builder.HasIndex(v => v.FileHash);

        // Relationships
        builder.HasOne(v => v.File)
            .WithMany(f => f.Versions)
            .HasForeignKey(v => v.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.Content)
            .WithMany()
            .HasForeignKey(v => v.FileHash)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
