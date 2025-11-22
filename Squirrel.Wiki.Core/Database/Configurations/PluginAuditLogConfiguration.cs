using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Configurations;

/// <summary>
/// Entity Framework configuration for PluginAuditLog
/// </summary>
public class PluginAuditLogConfiguration : IEntityTypeConfiguration<PluginAuditLog>
{
    public void Configure(EntityTypeBuilder<PluginAuditLog> builder)
    {
        builder.ToTable("PluginAuditLogs");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.PluginIdentifier)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.PluginName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Operation)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(p => p.Username)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(p => p.Changes)
            .HasColumnType("text");

        builder.Property(p => p.IpAddress)
            .HasMaxLength(45); // IPv6 max length

        builder.Property(p => p.UserAgent)
            .HasMaxLength(500);

        builder.Property(p => p.Timestamp)
            .IsRequired();

        builder.Property(p => p.Success)
            .IsRequired();

        builder.Property(p => p.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(p => p.Notes)
            .HasMaxLength(1000);

        // Relationships
        builder.HasOne(p => p.Plugin)
            .WithMany()
            .HasForeignKey(p => p.PluginId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes for common queries
        builder.HasIndex(p => p.PluginId);
        builder.HasIndex(p => p.UserId);
        builder.HasIndex(p => p.Timestamp);
        builder.HasIndex(p => p.Operation);
        builder.HasIndex(p => new { p.PluginId, p.Timestamp });
    }
}
