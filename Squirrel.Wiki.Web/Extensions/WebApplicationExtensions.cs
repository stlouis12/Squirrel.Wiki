using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Configuration;
using Squirrel.Wiki.Core.Database;
using Squirrel.Wiki.Core.Services.Configuration;
using Squirrel.Wiki.Core.Services.Infrastructure;
using Squirrel.Wiki.Core.Services.Plugins;
using Squirrel.Wiki.Core.Services.Users;
using Squirrel.Wiki.Web.Middleware;
using System.Globalization;
using PathHelper = Squirrel.Wiki.Core.Services.Infrastructure.PathHelper;

namespace Squirrel.Wiki.Web.Extensions;

public static class WebApplicationExtensions
{
    public static async Task<string> InitializeDatabaseAsync(
        this WebApplication app,
        MinimalConfigurationService minimalConfig,
        string appDataPath)
    {
        string defaultLanguage = "en";

        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        try
        {
            var context = services.GetRequiredService<SquirrelDbContext>();
            var logger = services.GetRequiredService<ILogger<Program>>();

            // Get database initialization settings
            var autoMigrateStr = minimalConfig.GetValue("SQUIRREL_DATABASE_AUTO_MIGRATE", "true");
            var seedDataStr = minimalConfig.GetValue("SQUIRREL_DATABASE_SEED_DATA", "true");
            var autoMigrate = bool.Parse(autoMigrateStr);
            var seedData = bool.Parse(seedDataStr);

            if (autoMigrate)
            {
                logger.LogInformation("Applying database migrations...");
                await context.Database.MigrateAsync();
                logger.LogInformation("Database migrations applied successfully");
            }

            if (seedData)
            {
                var customSeedDataPath = minimalConfig.GetValue("SQUIRREL_SEED_DATA_FILE_PATH");

                if (!string.IsNullOrEmpty(customSeedDataPath))
                {
                    var resolvedSeedDataPath = PathHelper.ResolvePath(customSeedDataPath, appDataPath);

                    if (File.Exists(resolvedSeedDataPath))
                    {
                        logger.LogInformation("Using custom seed data file: {Path}", resolvedSeedDataPath);
                        await DatabaseSeeder.SeedAsync(context, logger, resolvedSeedDataPath);
                    }
                    else
                    {
                        logger.LogWarning("Custom seed data file not found: {Path}. Using default seed data.", resolvedSeedDataPath);
                        await DatabaseSeeder.SeedAsync(context, logger);
                    }
                }
                else
                {
                    await DatabaseSeeder.SeedAsync(context, logger);
                }
            }

            // Ensure admin user exists
            var adminBootstrap = services.GetRequiredService<IAdminBootstrapService>();
            logger.LogInformation("Checking for admin users...");
            await adminBootstrap.EnsureAdminExistsAsync();

            // Initialize plugin service
            var pluginService = services.GetRequiredService<IPluginService>();
            logger.LogInformation("Initializing plugin service...");
            await pluginService.InitializeAsync();
            logger.LogInformation("Plugin service initialized successfully");

            // Get default language
            var configService = services.GetRequiredService<IConfigurationService>();
            defaultLanguage = await configService.GetValueAsync<string>("SQUIRREL_DEFAULT_LANGUAGE") ?? "en";

            var source = configService.GetSource("SQUIRREL_DEFAULT_LANGUAGE");
            logger.LogInformation("Default language loaded from {Source}: {Language}", source, defaultLanguage);
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while initializing the database");
            throw;
        }

        return defaultLanguage;
    }

    public static WebApplication ConfigureSquirrelWikiMiddleware(this WebApplication app, string defaultLanguage)
    {
        // Configure request localization
        var languageMetadata = ConfigurationMetadataRegistry.GetMetadata("SQUIRREL_DEFAULT_LANGUAGE");
        var supportedLanguages = languageMetadata.Validation?.AllowedValues ?? new[] { "en" };
        var supportedCultures = supportedLanguages.Select(lang => new CultureInfo(lang)).ToArray();

        Log.Information("Supported cultures configured: {Cultures}", string.Join(", ", supportedLanguages));

        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(defaultLanguage),
            SupportedCultures = supportedCultures,
            SupportedUICultures = supportedCultures
        });

        // Configure HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        // Add global exception handler
        app.UseGlobalExceptionHandler();

        // Add response caching middleware
        app.UseResponseCaching();

        // Add security headers
        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
            context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            await next();
        });

        app.UseRouting();
        app.UseSerilogRequestLogging();
        
        // Add session middleware (must be before authentication)
        app.UseSession();
        
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }

    public static WebApplication MapSquirrelWikiEndpoints(this WebApplication app)
    {
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        // Map health check endpoints
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = (check) => check.Tags.Contains("ready")
        });

        Log.Information("Health check endpoints configured: /health, /health/ready");

        return app;
    }
}
