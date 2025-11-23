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
    public DbSet<AuthenticationPlugin> AuthenticationPlugins { get; set; } = null!;
    public DbSet<AuthenticationPluginSetting> AuthenticationPluginSettings { get; set; } = null!;
    public DbSet<PluginAuditLog> PluginAuditLogs { get; set; } = null!;
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    /// <summary>
    /// Ensures all DateTime values are stored as UTC before saving to database
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Ensure all DateTime values are UTC before saving
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            foreach (var property in entry.Properties)
            {
                // Handle DateTime properties
                if (property.Metadata.ClrType == typeof(DateTime))
                {
                    if (property.CurrentValue is DateTime dateTime)
                    {
                        if (dateTime.Kind != DateTimeKind.Utc)
                        {
                            _logger?.LogWarning(
                                "Non-UTC DateTime detected in {EntityType}.{PropertyName}. Converting to UTC. Original Kind: {Kind}",
                                entry.Metadata.Name,
                                property.Metadata.Name,
                                dateTime.Kind);
                            
                            property.CurrentValue = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
                        }
                    }
                }
                // Handle nullable DateTime properties
                else if (property.Metadata.ClrType == typeof(DateTime?))
                {
                    if (property.CurrentValue is DateTime nullableDateTime)
                    {
                        if (nullableDateTime.Kind != DateTimeKind.Utc)
                        {
                            _logger?.LogWarning(
                                "Non-UTC DateTime detected in {EntityType}.{PropertyName}. Converting to UTC. Original Kind: {Kind}",
                                entry.Metadata.Name,
                                property.Metadata.Name,
                                nullableDateTime.Kind);
                            
                            property.CurrentValue = DateTime.SpecifyKind(nullableDateTime, DateTimeKind.Utc);
                        }
                    }
                }
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
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
        modelBuilder.Entity<AuthenticationPlugin>().ToTable("squirrel_authentication_plugins");
        modelBuilder.Entity<AuthenticationPluginSetting>().ToTable("squirrel_authentication_plugin_settings");
        modelBuilder.Entity<PluginAuditLog>().ToTable("squirrel_plugin_audit_logs");
        modelBuilder.Entity<DataProtectionKey>().ToTable("squirrel_data_protection_keys");
    }
}
