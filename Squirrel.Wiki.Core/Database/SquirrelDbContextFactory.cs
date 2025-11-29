using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Squirrel.Wiki.Core.Database;

/// <summary>
/// Design-time factory for creating SquirrelDbContext instances during migrations
/// </summary>
public class SquirrelDbContextFactory : IDesignTimeDbContextFactory<SquirrelDbContext>
{
    public SquirrelDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SquirrelDbContext>();
        
        // Use SQLite for design-time (migrations)
        // The actual connection string doesn't matter for generating migrations
        optionsBuilder.UseSqlite("Data Source=squirrel_wiki.db");
        
        return new SquirrelDbContext(optionsBuilder.Options);
    }
}
