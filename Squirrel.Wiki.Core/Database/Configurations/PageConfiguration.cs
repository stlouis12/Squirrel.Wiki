using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Configurations;

public class PageConfiguration : IEntityTypeConfiguration<Page>
{
    public void Configure(EntityTypeBuilder<Page> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Title)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(p => p.Slug)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(p => p.CreatedBy)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(p => p.ModifiedBy)
            .IsRequired()
            .HasMaxLength(255);

        // Indexes
        builder.HasIndex(p => p.Slug)
            .IsUnique();

        builder.HasIndex(p => p.Title);

        builder.HasIndex(p => p.CategoryId);

        builder.HasIndex(p => p.IsDeleted);

        // Composite indexes for common query patterns
        builder.HasIndex(p => new { p.CategoryId, p.IsDeleted, p.Title });

        builder.HasIndex(p => new { p.IsDeleted, p.Title });

        builder.HasIndex(p => new { p.CreatedBy, p.IsDeleted, p.CreatedOn });

        builder.HasIndex(p => new { p.ModifiedBy, p.IsDeleted, p.ModifiedOn });

        // Relationships
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Pages)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(p => p.Contents)
            .WithOne(pc => pc.Page)
            .HasForeignKey(pc => pc.PageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.PageTags)
            .WithOne(pt => pt.Page)
            .HasForeignKey(pt => pt.PageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
