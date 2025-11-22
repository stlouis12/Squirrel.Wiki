using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Configurations;

/// <summary>
/// Entity Framework configuration for AuthenticationPluginSetting
/// </summary>
public class AuthenticationPluginSettingConfiguration : IEntityTypeConfiguration<AuthenticationPluginSetting>
{
    public void Configure(EntityTypeBuilder<AuthenticationPluginSetting> builder)
    {
        builder.ToTable("AuthenticationPluginSettings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.PluginId)
            .IsRequired();

        builder.Property(s => s.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Value)
            .HasMaxLength(4000); // Allow large values

        builder.Property(s => s.IsFromEnvironment)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.EnvironmentVariableName)
            .HasMaxLength(200);

        builder.Property(s => s.IsSecret)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(s => s.PluginId);

        builder.HasIndex(s => new { s.PluginId, s.Key })
            .IsUnique();
    }
}
