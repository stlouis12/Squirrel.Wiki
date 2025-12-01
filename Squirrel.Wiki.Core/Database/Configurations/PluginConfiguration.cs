using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Configurations;

/// <summary>
/// Entity Framework configuration for Plugin
/// </summary>
public class PluginConfiguration : IEntityTypeConfiguration<Plugin>
{
    public void Configure(EntityTypeBuilder<Plugin> builder)
    {
        builder.ToTable("Plugins");

        builder.HasKey(p => p.Id);
        
        builder.Property(p => p.Id)
            .ValueGeneratedOnAdd();

        builder.Property(p => p.PluginId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Version)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.PluginType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.IsEnabled)
            .IsRequired();

        builder.Property(p => p.IsConfigured)
            .IsRequired();

        builder.Property(p => p.LoadOrder)
            .IsRequired();

        builder.Property(p => p.IsCorePlugin)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .IsRequired();

        // Create unique index on PluginId
        builder.HasIndex(p => p.PluginId)
            .IsUnique();

        // Relationships
        builder.HasMany(p => p.Settings)
            .WithOne(s => s.Plugin)
            .HasForeignKey(s => s.PluginId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
