using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database;

/// <summary>
/// Entity Framework Core database context for Squirrel Wiki
/// </summary>
public class SquirrelDbContext : DbContext, IDataProtectionKeyContext
{
    private readonly ILogger<SquirrelDbContext>? _logger;

    public SquirrelDbContext(DbContextOptions<SquirrelDbContext> options)
        : base(options)
    {
    }

    public SquirrelDbContext(DbContextOptions<SquirrelDbContext> options, ILogger<SquirrelDbContext> logger)
        : base(options)
    {
        _logger = logger;
    }

    // DbSet properties
    public DbSet<Page> Pages { get; set; } = null!;
    public DbSet<PageContent> PageContents { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<UserRole> UserRoles { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Tag> Tags { get; set; } = null!;
    public DbSet<PageTag> PageTags { get; set; } = null!;
    public DbSet<Menu> Menus { get; set; } = null!;
    public DbSet<SiteConfiguration> SiteConfigurations { get; set; } = null!;
    public DbSet<Plugin> Plugins { get; set; } = null!;
    public DbSet<PluginSetting> PluginSettings { get; set; } = null!;
    public DbSet<PluginAuditLog> PluginAuditLogs { get; set; } = null!;
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
    public DbSet<Folder> Folders { get; set; } = null!;
    public DbSet<Entities.File> Files { get; set; } = null!;
    public DbSet<FileContent> FileContents { get; set; } = null!;
    public DbSet<FileVersion> FileVersions { get; set; } = null!;

    /// <summary>
    /// Ensures all DateTime values are stored as UTC before saving to database
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnsureDateTimesAreUtc();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Ensures all DateTime properties in tracked entities are UTC
    /// </summary>
    private void EnsureDateTimesAreUtc()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            ConvertDateTimePropertiesToUtc(entry);
        }
    }

    /// <summary>
    /// Converts all DateTime properties in an entity entry to UTC
    /// </summary>
    private void ConvertDateTimePropertiesToUtc(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        foreach (var property in entry.Properties)
        {
            if (IsDateTimeProperty(property.Metadata.ClrType))
            {
                ConvertPropertyToUtc(entry, property);
            }
        }
    }

    /// <summary>
    /// Checks if a type is DateTime or nullable DateTime
    /// </summary>
    private static bool IsDateTimeProperty(Type propertyType)
    {
        return propertyType == typeof(DateTime) || propertyType == typeof(DateTime?);
    }

    /// <summary>
    /// Converts a DateTime property value to UTC if needed
    /// </summary>
    private void ConvertPropertyToUtc(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry property)
    {
        if (property.CurrentValue is not DateTime dateTime)
        {
            return;
        }

        if (dateTime.Kind == DateTimeKind.Utc)
        {
            return;
        }

        LogNonUtcDateTimeWarning(entry, property, dateTime);
        property.CurrentValue = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
    }

    /// <summary>
    /// Logs a warning when a non-UTC DateTime is detected
    /// </summary>
    private void LogNonUtcDateTimeWarning(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry property,
        DateTime dateTime)
    {
        _logger?.LogWarning(
            "Non-UTC DateTime detected in {EntityType}.{PropertyName}. Converting to UTC. Original Kind: {Kind}",
            entry.Metadata.Name,
            property.Metadata.Name,
            dateTime.Kind);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from the assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SquirrelDbContext).Assembly);

        // Configure table names with prefix
        modelBuilder.Entity<Page>().ToTable("squirrel_pages");
        modelBuilder.Entity<PageContent>().ToTable("squirrel_page_contents");
        modelBuilder.Entity<User>().ToTable("squirrel_users");
        modelBuilder.Entity<UserRole>().ToTable("squirrel_user_roles");
        modelBuilder.Entity<Category>().ToTable("squirrel_categories");
        modelBuilder.Entity<Tag>().ToTable("squirrel_tags");
        modelBuilder.Entity<PageTag>().ToTable("squirrel_page_tags");
        modelBuilder.Entity<Menu>().ToTable("squirrel_menus");
        modelBuilder.Entity<SiteConfiguration>().ToTable("squirrel_site_configurations");
        modelBuilder.Entity<Plugin>().ToTable("squirrel_plugins");
        modelBuilder.Entity<PluginSetting>().ToTable("squirrel_plugin_settings");
        modelBuilder.Entity<PluginAuditLog>().ToTable("squirrel_plugin_audit_logs");
        modelBuilder.Entity<DataProtectionKey>().ToTable("squirrel_data_protection_keys");
        modelBuilder.Entity<Folder>().ToTable("squirrel_folders");
        modelBuilder.Entity<Entities.File>().ToTable("squirrel_files");
        modelBuilder.Entity<FileContent>().ToTable("squirrel_file_contents");
        modelBuilder.Entity<FileVersion>().ToTable("squirrel_file_versions");
    }
}
