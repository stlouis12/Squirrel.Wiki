using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Configurations;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.HasKey(ur => ur.Id);

        builder.Property(ur => ur.UserId)
            .IsRequired();

        builder.Property(ur => ur.Role)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(ur => ur.AssignedOn)
            .IsRequired();

        builder.Property(ur => ur.AssignedBy)
            .IsRequired()
            .HasMaxLength(100);

        // Indexes
        builder.HasIndex(ur => ur.UserId);

        builder.HasIndex(ur => new { ur.UserId, ur.Role })
            .IsUnique(); // Prevent duplicate role assignments

        // Relationships configured in UserConfiguration
    }
}
