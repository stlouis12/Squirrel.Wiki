using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Configurations;

/// <summary>
/// Entity Framework configuration for PluginSetting
/// </summary>
public class PluginSettingConfiguration : IEntityTypeConfiguration<PluginSetting>
{
    public void Configure(EntityTypeBuilder<PluginSetting> builder)
    {
        builder.ToTable("PluginSettings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.PluginId)
            .IsRequired();

        builder.Property(s => s.Key)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Value)
            .HasMaxLength(4000);

        builder.Property(s => s.IsFromEnvironment)
            .IsRequired();

        builder.Property(s => s.EnvironmentVariableName)
            .HasMaxLength(200);

        builder.Property(s => s.IsSecret)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        // Create composite unique index on PluginId + Key
        builder.HasIndex(s => new { s.PluginId, s.Key })
            .IsUnique();

        // Relationships
        builder.HasOne(s => s.Plugin)
            .WithMany(p => p.Settings)
            .HasForeignKey(s => s.PluginId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
