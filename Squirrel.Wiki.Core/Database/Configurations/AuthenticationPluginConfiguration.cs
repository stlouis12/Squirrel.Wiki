using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Configurations;

/// <summary>
/// Entity Framework configuration for AuthenticationPlugin
/// </summary>
public class AuthenticationPluginConfiguration : IEntityTypeConfiguration<AuthenticationPlugin>
{
    public void Configure(EntityTypeBuilder<AuthenticationPlugin> builder)
    {
        builder.ToTable("AuthenticationPlugins");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PluginId)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(p => p.PluginId)
            .IsUnique();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Version)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.IsEnabled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.IsConfigured)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.LoadOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(p => p.IsCorePlugin)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .IsRequired();

        // Relationships
        builder.HasMany(p => p.Settings)
            .WithOne(s => s.Plugin)
            .HasForeignKey(s => s.PluginId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
