using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Description)
            .HasMaxLength(500);

        builder.Property(c => c.CreatedBy)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(c => c.ModifiedBy)
            .IsRequired()
            .HasMaxLength(255);

        // Indexes
        // Composite unique index: slug must be unique within the same parent
        // This allows "Getting Started" under both "Documentation" and "Tutorials"
        builder.HasIndex(c => new { c.ParentCategoryId, c.Slug })
            .IsUnique();

        builder.HasIndex(c => c.ParentCategoryId);

        builder.HasIndex(c => c.DisplayOrder);

        // Self-referencing relationship for hierarchy
        builder.HasOne(c => c.ParentCategory)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
