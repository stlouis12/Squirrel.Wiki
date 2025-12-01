using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Squirrel.Wiki.Core.Database.Entities;
using FileEntity = Squirrel.Wiki.Core.Database.Entities.File;

namespace Squirrel.Wiki.Core.Database.Configurations;

public class FileConfiguration : IEntityTypeConfiguration<FileEntity>
{
    public void Configure(EntityTypeBuilder<FileEntity> builder)
    {
        builder.HasKey(f => f.Id);
        
        builder.Property(f => f.Id)
            .ValueGeneratedOnAdd();

        builder.Property(f => f.FileHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(f => f.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(f => f.FilePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(f => f.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(f => f.StorageProvider)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Local");

        builder.Property(f => f.UploadedBy)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(f => f.Visibility)
            .IsRequired()
            .HasDefaultValue(FileVisibility.Inherit);

        builder.Property(f => f.ThumbnailPath)
            .HasMaxLength(1000);

        builder.Property(f => f.CurrentVersion)
            .IsRequired()
            .HasDefaultValue(1);

        // Indexes
        builder.HasIndex(f => f.FileHash);

        builder.HasIndex(f => f.FileName);

        builder.HasIndex(f => f.FilePath)
            .IsUnique();

        builder.HasIndex(f => f.FolderId);

        builder.HasIndex(f => f.StorageProvider);

        builder.HasIndex(f => f.IsDeleted);

        builder.HasIndex(f => f.UploadedOn);

        // Relationships
        builder.HasOne(f => f.Folder)
            .WithMany(fo => fo.Files)
            .HasForeignKey(f => f.FolderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.Content)
            .WithMany(c => c.Files)
            .HasForeignKey(f => f.FileHash)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(f => f.Versions)
            .WithOne(v => v.File)
            .HasForeignKey(v => v.FileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
