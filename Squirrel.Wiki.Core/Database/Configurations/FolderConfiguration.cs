using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Configurations;

public class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        builder.HasKey(f => f.Id);
        
        builder.Property(f => f.Id)
            .ValueGeneratedOnAdd();

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(f => f.Slug)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(f => f.CreatedBy)
            .IsRequired()
            .HasMaxLength(255);

        // Indexes
        builder.HasIndex(f => f.Slug);

        builder.HasIndex(f => f.ParentFolderId);

        builder.HasIndex(f => f.IsDeleted);

        // Relationships
        builder.HasOne(f => f.ParentFolder)
            .WithMany(f => f.SubFolders)
            .HasForeignKey(f => f.ParentFolderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(f => f.Files)
            .WithOne(file => file.Folder)
            .HasForeignKey(file => file.FolderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
