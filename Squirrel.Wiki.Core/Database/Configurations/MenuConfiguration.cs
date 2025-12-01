using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Configurations;

public class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> builder)
    {
        builder.HasKey(m => m.Id);
        
        builder.Property(m => m.Id)
            .ValueGeneratedOnAdd();

        builder.Property(m => m.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(m => m.MenuType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(m => m.Description)
            .HasMaxLength(500);

        builder.Property(m => m.Markup);

        builder.Property(m => m.ModifiedBy)
            .IsRequired()
            .HasMaxLength(255);

        // Indexes
        builder.HasIndex(m => new { m.MenuType, m.IsEnabled });

        builder.HasIndex(m => m.DisplayOrder);

        // Composite indexes for common query patterns with ordering
        builder.HasIndex(m => new { m.MenuType, m.IsEnabled, m.DisplayOrder });

        builder.HasIndex(m => new { m.MenuType, m.DisplayOrder });
    }
}
