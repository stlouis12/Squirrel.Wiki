using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Configurations;

public class SiteConfigurationConfiguration : IEntityTypeConfiguration<SiteConfiguration>
{
    public void Configure(EntityTypeBuilder<SiteConfiguration> builder)
    {
        builder.HasKey(sc => sc.Id);

        builder.Property(sc => sc.Key)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(sc => sc.Value)
            .IsRequired();

        builder.Property(sc => sc.ModifiedBy)
            .IsRequired()
            .HasMaxLength(255);

        // Indexes
        builder.HasIndex(sc => sc.Key)
            .IsUnique();
    }
}
