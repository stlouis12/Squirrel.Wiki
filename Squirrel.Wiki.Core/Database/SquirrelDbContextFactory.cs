using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MySql.EntityFrameworkCore.Extensions;
using static Squirrel.Wiki.Core.Configuration.ConfigurationMetadataRegistry.ConfigurationKeys;

namespace Squirrel.Wiki.Core.Database;

/// <summary>
/// Design-time factory for creating SquirrelDbContext instances during migrations.
/// Uses SQUIRREL_DATABASE_PROVIDER environment variable to determine which provider to use.
/// </summary>
public class SquirrelDbContextFactory : IDesignTimeDbContextFactory<SquirrelDbContext>
{
    public SquirrelDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SquirrelDbContext>();
        
        // Use SQUIRREL_DATABASE_PROVIDER environment variable to determine which provider
        // Default to PostgreSQL if not specified
        var provider = Environment.GetEnvironmentVariable(SQUIRREL_DATABASE_PROVIDER) ?? "PostgreSQL";
        
        switch (provider.ToLowerInvariant())
        {
            case "postgresql":
                optionsBuilder.UseNpgsql(
                    "Host=localhost;Database=squirrel_wiki;Username=postgres;Password=postgres",
                    npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                        npgsqlOptions.MigrationsAssembly("Squirrel.Wiki.EF.PostgreSql");
                    });
                break;
                
            case "sqlite":
                optionsBuilder.UseSqlite(
                    "Data Source=squirrel_wiki.db",
                    sqliteOptions => sqliteOptions.MigrationsAssembly("Squirrel.Wiki.EF.Sqlite"));
                break;
                
            case "mysql":
            case "mariadb":
                optionsBuilder.UseMySQL(
                    "Server=localhost;Database=squirrel_wiki;User=root;Password=password;",
                    mysqlOptions => mysqlOptions.MigrationsAssembly("Squirrel.Wiki.EF.MySql"));
                break;
                
            case "sqlserver":
                optionsBuilder.UseSqlServer(
                    "Server=localhost;Database=squirrel_wiki;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;",
                    sqlServerOptions =>
                    {
                        sqlServerOptions.MigrationsHistoryTable("__EFMigrationsHistory", "dbo");
                        sqlServerOptions.MigrationsAssembly("Squirrel.Wiki.EF.SqlServer");
                    });
                break;
                
            default:
                // Default to PostgreSQL
                optionsBuilder.UseNpgsql(
                    "Host=localhost;Database=squirrel_wiki;Username=postgres;Password=postgres",
                    npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                        npgsqlOptions.MigrationsAssembly("Squirrel.Wiki.EF.PostgreSql");
                    });
                break;
        }
        
        return new SquirrelDbContext(optionsBuilder.Options);
    }
}
