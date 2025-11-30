using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Configuration;
using Squirrel.Wiki.Core.Database;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Events;
using Squirrel.Wiki.Core.Exceptions;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Services.Caching;
using Squirrel.Wiki.Core.Services.Categories;
using Squirrel.Wiki.Core.Services.Configuration;
using Squirrel.Wiki.Core.Services.Content;
using Squirrel.Wiki.Core.Services.Infrastructure;
using Squirrel.Wiki.Core.Services.Menus;
using Squirrel.Wiki.Core.Services.Pages;
using Squirrel.Wiki.Core.Services.Plugins;
using Squirrel.Wiki.Core.Services.Search;
using Squirrel.Wiki.Core.Services.Tags;
using Squirrel.Wiki.Core.Services.Users;
using Squirrel.Wiki.Web.Resources;
using PathHelper = Squirrel.Wiki.Core.Services.Infrastructure.PathHelper;

namespace Squirrel.Wiki.Web.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSquirrelWikiDatabase(
        this IServiceCollection services,
        MinimalConfigurationService minimalConfig,
        string appDataPath,
        ILogger logger)
    {
        var connectionString = minimalConfig.GetValue("SQUIRREL_DATABASE_CONNECTION_STRING");
        var databaseProvider = minimalConfig.GetValue("SQUIRREL_DATABASE_PROVIDER");

        logger.LogInformation("Database provider: {Provider} (from {Source})",
            databaseProvider,
            minimalConfig.HasValue("SQUIRREL_DATABASE_PROVIDER") ? "environment variable" : "appsettings.json");

        // For SQLite, resolve relative paths using the configured app data path
        if (databaseProvider.Equals("SQLite", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(connectionString))
        {
            var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
            var dataSource = builder.DataSource;

            if (!string.IsNullOrEmpty(dataSource) && !Path.IsPathRooted(dataSource))
            {
                var absolutePath = PathHelper.ResolvePath(dataSource, appDataPath);
                var directory = Path.GetDirectoryName(absolutePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    PathHelper.EnsureDirectoryExists(directory);
                    logger.LogInformation("Ensured database directory exists: {Directory}", directory);
                }

                builder.DataSource = absolutePath;
                connectionString = builder.ConnectionString;
                logger.LogInformation("SQLite database path resolved to: {Path}", absolutePath);
            }
        }

        services.AddDbContext<SquirrelDbContext>(options =>
        {
            switch (databaseProvider.ToLowerInvariant())
            {
                case "postgresql":
                    options.UseNpgsql(connectionString);
                    break;
                case "mysql":
                case "mariadb":
                    var serverVersion = ServerVersion.AutoDetect(connectionString);
                    options.UseMySql(connectionString, serverVersion);
                    break;
                case "sqlserver":
                    options.UseSqlServer(connectionString);
                    break;
                case "sqlite":
                    options.UseSqlite(connectionString);
                    break;
                default:
                    throw new ConfigurationException(
                        $"The configured database provider '{databaseProvider}' is not supported. Supported providers are: PostgreSQL, MySQL, MariaDB, SQLServer, SQLite.",
                        "UNSUPPORTED_DATABASE_PROVIDER"
                    ).WithContext("ConfiguredProvider", databaseProvider)
                     .WithContext("SupportedProviders", "PostgreSQL, MySQL, MariaDB, SQLServer, SQLite");
            }

            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        return services;
    }

    public static IServiceCollection AddSquirrelWikiCaching(
        this IServiceCollection services,
        MinimalConfigurationService minimalConfig,
        ILogger logger)
    {
        var cacheProvider = minimalConfig.GetValue("SQUIRREL_CACHE_PROVIDER", "Memory");
        logger.LogInformation("Configuring cache provider: {Provider} (from {Source})",
            cacheProvider,
            minimalConfig.GetSource("SQUIRREL_CACHE_PROVIDER"));

        if (cacheProvider.Equals("Redis", StringComparison.OrdinalIgnoreCase))
        {
            var redisConfiguration = minimalConfig.GetValue("SQUIRREL_REDIS_CONFIGURATION", "localhost:6379");
            var redisInstanceName = minimalConfig.GetValue("SQUIRREL_REDIS_INSTANCE_NAME", "Squirrel_");

            logger.LogInformation("Configuring Redis cache: {Configuration}, Instance: {Instance}",
                redisConfiguration, redisInstanceName);

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConfiguration;
                options.InstanceName = redisInstanceName;
            });
        }
        else
        {
            logger.LogInformation("Using in-memory distributed cache");
            services.AddDistributedMemoryCache();
        }

        return services;
    }

    public static IServiceCollection AddSquirrelWikiAuthentication(this IServiceCollection services, ILogger logger)
    {
        logger.LogInformation("Configuring cookie-based authentication");

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Cookie.Name = "Squirrel.Auth";
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
            options.AddPolicy("RequireEditor", policy => policy.RequireRole("Editor", "Admin"));
            options.AddPolicy("CanViewPage", policy =>
                policy.Requirements.Add(new Squirrel.Wiki.Core.Security.Authorization.PageAccessRequirement(
                    Squirrel.Wiki.Core.Security.Authorization.PageAccessType.View)));
            options.AddPolicy("CanEditPage", policy =>
                policy.Requirements.Add(new Squirrel.Wiki.Core.Security.Authorization.PageAccessRequirement(
                    Squirrel.Wiki.Core.Security.Authorization.PageAccessType.Edit)));
            options.AddPolicy("CanDeletePage", policy =>
                policy.Requirements.Add(new Squirrel.Wiki.Core.Security.Authorization.PageAccessRequirement(
                    Squirrel.Wiki.Core.Security.Authorization.PageAccessType.Delete)));
        });

        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
            Squirrel.Wiki.Core.Security.Authorization.PageAccessHandler>();

        return services;
    }

    public static IServiceCollection AddSquirrelWikiRepositories(this IServiceCollection services)
    {
        services.AddScoped<IPageRepository, PageRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IMenuRepository, MenuRepository>();
        services.AddScoped<Squirrel.Wiki.Core.Database.Repositories.IFileRepository, Squirrel.Wiki.Core.Database.Repositories.FileRepository>();
        services.AddScoped<Squirrel.Wiki.Core.Database.Repositories.IFolderRepository, Squirrel.Wiki.Core.Database.Repositories.FolderRepository>();

        return services;
    }

    public static IServiceCollection AddSquirrelWikiServices(this IServiceCollection services, string pluginsPath, ILogger logger)
    {
        // Configuration System
        services.AddScoped<Squirrel.Wiki.Core.Configuration.IConfigurationProvider, DefaultConfigurationProvider>();
        services.AddScoped<Squirrel.Wiki.Core.Configuration.IConfigurationProvider, EnvironmentVariableConfigurationProvider>();
        services.AddScoped<Squirrel.Wiki.Core.Configuration.IConfigurationProvider, DatabaseConfigurationProvider>();
        services.AddScoped<IConfigurationService, Squirrel.Wiki.Core.Configuration.ConfigurationService>();
        logger.LogInformation("ConfigurationService registered");

        // Core Services
        services.AddSingleton<ISlugGenerator, SlugGenerator>();
        services.AddScoped<ICacheService, CacheService>();
        services.AddScoped<ICacheInvalidationService, CacheInvalidationService>();

        // Event System
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventPublisher, Squirrel.Wiki.Core.Events.EventPublisher>();
        RegisterEventHandlers(services);
        logger.LogInformation("Event-based cache invalidation and search indexing system registered");

        // Content Services
        services.AddScoped<IMarkdownService, MarkdownService>();
        services.AddScoped<IPageContentService, PageContentService>();
        services.AddScoped<IPageRenderingService, PageRenderingService>();
        services.AddScoped<IPageLinkService, PageLinkService>();
        services.AddScoped<IPageService, PageService>();

        // Domain Services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ICategoryTreeBuilder, CategoryTreeBuilder>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IMenuService, MenuService>();

        // Search Services
        services.AddScoped<Squirrel.Wiki.Core.Services.Search.SearchService>();
        services.AddScoped<Squirrel.Wiki.Core.Services.Search.DatabaseSearchStrategy>();
        services.AddScoped<Squirrel.Wiki.Core.Services.Search.SearchStrategyService>();
        services.AddScoped<ISearchService, Squirrel.Wiki.Core.Services.Search.SearchStrategyService>();
        logger.LogInformation("Search services registered");

        // Infrastructure Services
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<ITimezoneService, TimezoneService>();
        services.AddScoped<IUrlTokenResolver, UrlTokenResolver>();
        services.AddScoped<FooterMarkupParser>();
        services.AddScoped<Squirrel.Wiki.Contracts.Storage.IFileStorageStrategy, Squirrel.Wiki.Core.Services.Infrastructure.LocalFileStorageStrategy>();
        services.AddScoped<Squirrel.Wiki.Core.Services.Files.IFileService, Squirrel.Wiki.Core.Services.Files.FileService>();
        services.AddScoped<Squirrel.Wiki.Core.Services.Files.IFolderService, Squirrel.Wiki.Core.Services.Files.FolderService>();
        logger.LogInformation("File management services registered");

        // Web Services
        services.AddScoped<Squirrel.Wiki.Web.Services.INotificationService, Squirrel.Wiki.Web.Services.TempDataNotificationService>();

        // Security Services
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<Squirrel.Wiki.Core.Security.IAuthenticationService, Squirrel.Wiki.Core.Security.AuthenticationService>();
        services.AddScoped<IAdminBootstrapService, AdminBootstrapService>();
        services.AddScoped<Squirrel.Wiki.Contracts.Authentication.IAuthenticationStrategy, LocalAuthenticationStrategy>();
        services.AddSingleton<ISecretEncryptionService, SecretEncryptionService>();
        services.AddScoped<Squirrel.Wiki.Core.Security.IAuthorizationService, Squirrel.Wiki.Core.Security.AuthorizationService>();

        // Plugin Services
        logger.LogInformation("Plugins path configured: {PluginsPath}", pluginsPath);
        services.AddSingleton<Squirrel.Wiki.Plugins.IPluginLoader, Squirrel.Wiki.Plugins.PluginLoader>();
        services.AddScoped<Squirrel.Wiki.Plugins.IPluginLifecycleManager, Squirrel.Wiki.Plugins.PluginLifecycleManager>();
        services.AddScoped<IPluginAuditService, PluginAuditService>();
        services.AddScoped<IPluginService>(sp =>
        {
            var context = sp.GetRequiredService<SquirrelDbContext>();
            var pluginLoader = sp.GetRequiredService<Squirrel.Wiki.Plugins.IPluginLoader>();
            var pluginLogger = sp.GetRequiredService<ILogger<PluginService>>();
            var cache = sp.GetRequiredService<ICacheService>();
            var eventPublisher = sp.GetRequiredService<IEventPublisher>();
            var encryptionService = sp.GetRequiredService<ISecretEncryptionService>();
            var auditService = sp.GetRequiredService<IPluginAuditService>();
            var userContext = sp.GetRequiredService<IUserContext>();
            var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
            var configuration = sp.GetRequiredService<IConfigurationService>();
            return new PluginService(context, pluginLoader, pluginLogger, cache, eventPublisher, encryptionService, auditService, userContext, httpContextAccessor, sp, pluginsPath, configuration);
        });

        return services;
    }

    public static IServiceCollection AddSquirrelWikiHealthChecks(
        this IServiceCollection services,
        MinimalConfigurationService minimalConfig,
        ILogger logger)
    {
        var healthChecksBuilder = services.AddHealthChecks();
        var databaseProvider = minimalConfig.GetValue("SQUIRREL_DATABASE_PROVIDER");
        var connectionString = minimalConfig.GetValue("SQUIRREL_DATABASE_CONNECTION_STRING");

        // Add database health check based on provider
        switch (databaseProvider.ToLowerInvariant())
        {
            case "postgresql":
                healthChecksBuilder.AddNpgSql(connectionString, name: "database", tags: new[] { "ready", "db" });
                logger.LogInformation("Added PostgreSQL health check");
                break;
            case "mysql":
            case "mariadb":
                healthChecksBuilder.AddMySql(connectionString, name: "database", tags: new[] { "ready", "db" });
                logger.LogInformation("Added MySQL/MariaDB health check");
                break;
            case "sqlserver":
                healthChecksBuilder.AddSqlServer(connectionString, name: "database", tags: new[] { "ready", "db" });
                logger.LogInformation("Added SQL Server health check");
                break;
            case "sqlite":
                healthChecksBuilder.AddDbContextCheck<SquirrelDbContext>(name: "database", tags: new[] { "ready", "db" });
                logger.LogInformation("Added SQLite health check");
                break;
        }

        // Add Redis health check if enabled
        var cacheProvider = minimalConfig.GetValue("SQUIRREL_CACHE_PROVIDER", "Memory");
        if (cacheProvider.Equals("Redis", StringComparison.OrdinalIgnoreCase))
        {
            var redisConfiguration = minimalConfig.GetValue("SQUIRREL_REDIS_CONFIGURATION", "localhost:6379");
            healthChecksBuilder.AddRedis(redisConfiguration, name: "redis", tags: new[] { "ready", "cache" });
            logger.LogInformation("Added Redis health check");
        }

        return services;
    }

    private static void RegisterEventHandlers(IServiceCollection services)
    {
        // Cache invalidation handlers
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Pages.PageCreatedEvent>,
            Squirrel.Wiki.Core.Events.Handlers.PageCacheInvalidationHandler>();
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Pages.PageUpdatedEvent>,
            Squirrel.Wiki.Core.Events.Handlers.PageCacheInvalidationHandler>();
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Pages.PageDeletedEvent>,
            Squirrel.Wiki.Core.Events.Handlers.PageCacheInvalidationHandler>();
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Categories.CategoryChangedEvent>,
            Squirrel.Wiki.Core.Events.Handlers.CategoryCacheInvalidationHandler>();
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Tags.TagChangedEvent>,
            Squirrel.Wiki.Core.Events.Handlers.TagCacheInvalidationHandler>();
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Menus.MenuChangedEvent>,
            Squirrel.Wiki.Core.Events.Handlers.MenuCacheInvalidationHandler>();

        // Page search indexing handlers
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Pages.PageCreatedEvent>,
            Squirrel.Wiki.Core.Events.Handlers.PageSearchIndexHandler>();
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Pages.PageUpdatedEvent>,
            Squirrel.Wiki.Core.Events.Handlers.PageSearchIndexHandler>();
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Pages.PageDeletedEvent>,
            Squirrel.Wiki.Core.Events.Handlers.PageSearchIndexHandler>();
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Search.PageIndexRequestedEvent>,
            Squirrel.Wiki.Core.Events.Handlers.PageIndexRequestedEventHandler>();
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Search.PagesIndexRequestedEvent>,
            Squirrel.Wiki.Core.Events.Handlers.PagesIndexRequestedEventHandler>();
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Search.PageRemovedFromIndexEvent>,
            Squirrel.Wiki.Core.Events.Handlers.PageRemovedFromIndexEventHandler>();
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Search.IndexRebuildRequestedEvent>,
            Squirrel.Wiki.Core.Events.Handlers.IndexRebuildRequestedEventHandler>();
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Search.IndexOptimizationRequestedEvent>,
            Squirrel.Wiki.Core.Events.Handlers.IndexOptimizationRequestedEventHandler>();
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Search.IndexClearRequestedEvent>,
            Squirrel.Wiki.Core.Events.Handlers.IndexClearRequestedEventHandler>();

        // File search indexing handlers
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Files.FileUploadedEvent>,
            Squirrel.Wiki.Core.Events.Handlers.FileSearchIndexHandler>();
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Files.FileUpdatedEvent>,
            Squirrel.Wiki.Core.Events.Handlers.FileSearchIndexHandler>();
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Files.FileDeletedEvent>,
            Squirrel.Wiki.Core.Events.Handlers.FileSearchIndexHandler>();
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Files.FileMovedEvent>,
            Squirrel.Wiki.Core.Events.Handlers.FileSearchIndexHandler>();
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Search.FileIndexRequestedEvent>,
            Squirrel.Wiki.Core.Events.Handlers.FileIndexRequestedEventHandler>();
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Search.FilesIndexRequestedEvent>,
            Squirrel.Wiki.Core.Events.Handlers.FilesIndexRequestedEventHandler>();
        services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Search.FileRemovedFromIndexEvent>,
            Squirrel.Wiki.Core.Events.Handlers.FileRemovedFromIndexEventHandler>();
    }
}
