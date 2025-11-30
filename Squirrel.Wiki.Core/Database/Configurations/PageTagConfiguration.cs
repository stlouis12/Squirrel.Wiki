using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Configurations;

public class PageTagConfiguration : IEntityTypeConfiguration<PageTag>
{
    public void Configure(EntityTypeBuilder<PageTag> builder)
    {
        // Composite primary key
        builder.HasKey(pt => new { pt.PageId, pt.TagId });

        // Indexes
        builder.HasIndex(pt => pt.TagId);

        // Relationships configured in Page and Tag configurations
    }
}
