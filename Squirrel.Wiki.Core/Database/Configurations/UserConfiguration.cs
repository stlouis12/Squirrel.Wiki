using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        
        builder.Property(u => u.Id)
            .ValueGeneratedOnAdd();

        // Authentication
        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(255);

        builder.Property(u => u.ExternalId)
            .HasMaxLength(255);

        builder.Property(u => u.Provider)
            .IsRequired()
            .HasConversion<int>();

        // Profile
        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(u => u.FirstName)
            .HasMaxLength(100);

        builder.Property(u => u.LastName)
            .HasMaxLength(100);

        // Status
        builder.Property(u => u.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.IsLocked)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.FailedLoginAttempts)
            .IsRequired()
            .HasDefaultValue(0);

        // Password Reset
        builder.Property(u => u.PasswordResetToken)
            .HasMaxLength(255);

        // Timestamps
        builder.Property(u => u.CreatedOn)
            .IsRequired();

        // Indexes
        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.HasIndex(u => u.Username)
            .IsUnique();

        builder.HasIndex(u => u.ExternalId);

        builder.HasIndex(u => u.Provider);

        // Composite indexes for role queries with sorting
        builder.HasIndex(u => new { u.IsAdmin, u.Username });

        builder.HasIndex(u => new { u.IsEditor, u.Username });

        builder.HasIndex(u => new { u.IsActive, u.Username });

        // Relationships
        builder.HasMany(u => u.UserRoles)
            .WithOne(ur => ur.User)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
