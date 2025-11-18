using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Configurations;

public class PageContentConfiguration : IEntityTypeConfiguration<PageContent>
{
    public void Configure(EntityTypeBuilder<PageContent> builder)
    {
        builder.HasKey(pc => pc.Id);

        builder.Property(pc => pc.Text)
            .IsRequired();

        builder.Property(pc => pc.EditedBy)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(pc => pc.ChangeComment)
            .HasMaxLength(500);

        // Indexes
        builder.HasIndex(pc => pc.PageId);

        builder.HasIndex(pc => new { pc.PageId, pc.VersionNumber })
            .IsUnique();

        builder.HasIndex(pc => pc.EditedOn);

        // Relationships configured in PageConfiguration
    }
}
