using Microsoft.EntityFrameworkCore;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database;

/// <summary>
/// Entity Framework Core database context for Squirrel Wiki
/// </summary>
public class SquirrelDbContext : DbContext
{
    public SquirrelDbContext(DbContextOptions<SquirrelDbContext> options)
        : base(options)
    {
    }

    public DbSet<Page> Pages { get; set; } = null!;
    public DbSet<PageContent> PageContents { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Tag> Tags { get; set; } = null!;
    public DbSet<PageTag> PageTags { get; set; } = null!;
    public DbSet<Menu> Menus { get; set; } = null!;
    public DbSet<SiteConfiguration> SiteConfigurations { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from the assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SquirrelDbContext).Assembly);

        // Configure table names with prefix
        modelBuilder.Entity<Page>().ToTable("squirrel_pages");
        modelBuilder.Entity<PageContent>().ToTable("squirrel_page_contents");
        modelBuilder.Entity<User>().ToTable("squirrel_users");
        modelBuilder.Entity<Category>().ToTable("squirrel_categories");
        modelBuilder.Entity<Tag>().ToTable("squirrel_tags");
        modelBuilder.Entity<PageTag>().ToTable("squirrel_page_tags");
        modelBuilder.Entity<Menu>().ToTable("squirrel_menus");
        modelBuilder.Entity<SiteConfiguration>().ToTable("squirrel_site_configurations");
    }
}
