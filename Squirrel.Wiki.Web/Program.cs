using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Serilog;
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
using Squirrel.Wiki.Web.Middleware;
using Squirrel.Wiki.Web.Resources;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/squirrel-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(SharedResources));
    });

// Configure localization 
builder.Services.AddLocalization();

// Register the shared string localizer for dependency injection
builder.Services.AddSingleton<IStringLocalizer>(sp =>
{
    var factory = sp.GetRequiredService<IStringLocalizerFactory>();
    return factory.Create(typeof(SharedResources));
});

// Configure AutoMapper
builder.Services.AddAutoMapper(typeof(Squirrel.Wiki.Core.Mapping.UserMappingProfile).Assembly);

// Add response caching
builder.Services.AddResponseCaching();

// Create MinimalConfigurationService for startup configuration
// This service only reads from environment variables and is used before the database exists
var startupLogger = LoggerFactory.Create(config => config.AddConsole()).CreateLogger<MinimalConfigurationService>();
var minimalConfig = new MinimalConfigurationService(startupLogger);

// Get application data path (useful for containerized deployments)
var appDataPath = minimalConfig.GetValue("SQUIRREL_APP_DATA_PATH", "App_Data");
var resolvedAppDataPath = Path.IsPathRooted(appDataPath) 
    ? appDataPath 
    : Path.Combine(AppContext.BaseDirectory, appDataPath);

Log.Information("Application data path: {AppDataPath} (from {Source})", 
    resolvedAppDataPath, 
    minimalConfig.GetSource("SQUIRREL_APP_DATA_PATH"));

// Ensure App_Data directory exists
if (!Directory.Exists(resolvedAppDataPath))
{
    Directory.CreateDirectory(resolvedAppDataPath);
    Log.Information("Created application data directory: {Directory}", resolvedAppDataPath);
}

// Configure search settings using the resolved app data path
builder.Services.Configure<Squirrel.Wiki.Core.Services.Search.SearchSettings>(options =>
{
    options.IndexPath = Path.Combine(resolvedAppDataPath, "SearchIndex");
});

// Configure database
// Priority: Environment Variables → defaults from ConfigurationMetadataRegistry
var connectionString = minimalConfig.GetValue("SQUIRREL_DATABASE_CONNECTION_STRING");
var databaseProvider = minimalConfig.GetValue("SQUIRREL_DATABASE_PROVIDER");

Log.Information("Database provider: {Provider} (from {Source})", 
    databaseProvider, 
    minimalConfig.HasValue("SQUIRREL_DATABASE_PROVIDER") ? "environment variable" : "appsettings.json");

// For SQLite, resolve relative paths using the configured app data path
if (databaseProvider.Equals("SQLite", StringComparison.OrdinalIgnoreCase) && 
    !string.IsNullOrEmpty(connectionString))
{
    // Parse the connection string to extract the Data Source
    var builder2 = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
    var dataSource = builder2.DataSource;
    
    // If the path is relative (doesn't start with / or contain :), make it relative to app data path
    if (!string.IsNullOrEmpty(dataSource) && 
        !Path.IsPathRooted(dataSource))
    {
        // If the data source starts with "App_Data/", use the resolved app data path
        if (dataSource.StartsWith("App_Data/", StringComparison.OrdinalIgnoreCase) ||
            dataSource.StartsWith("App_Data\\", StringComparison.OrdinalIgnoreCase))
        {
            // Replace App_Data with the resolved app data path
            var relativePath = dataSource.Substring("App_Data/".Length);
            var absolutePath = Path.Combine(resolvedAppDataPath, relativePath);
            
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Log.Information("Created database directory: {Directory}", directory);
            }
            
            builder2.DataSource = absolutePath;
            connectionString = builder2.ConnectionString;
            Log.Information("SQLite database path resolved to: {Path}", absolutePath);
        }
        else
        {
            // For other relative paths, make them relative to the executable directory
            var absolutePath = Path.Combine(AppContext.BaseDirectory, dataSource);
            
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Log.Information("Created database directory: {Directory}", directory);
            }
            
            builder2.DataSource = absolutePath;
            connectionString = builder2.ConnectionString;
            Log.Information("SQLite database path resolved to: {Path}", absolutePath);
        }
    }
}

builder.Services.AddDbContext<SquirrelDbContext>(options =>
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

    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Configure distributed caching using MinimalConfigurationService
// NOTE: We use MinimalConfigurationService here because it only reads from environment variables
// and has no dependencies on the database (which doesn't exist yet at startup).
// These settings match the defaults in ConfigurationMetadataRegistry.

var cacheProvider = minimalConfig.GetValue("SQUIRREL_CACHE_PROVIDER", "Memory");
Log.Information("Configuring cache provider: {Provider} (from {Source})", 
    cacheProvider, 
    minimalConfig.GetSource("SQUIRREL_CACHE_PROVIDER"));

if (cacheProvider.Equals("Redis", StringComparison.OrdinalIgnoreCase))
{
    var redisConfiguration = minimalConfig.GetValue("SQUIRREL_REDIS_CONFIGURATION", "localhost:6379");
    var redisInstanceName = minimalConfig.GetValue("SQUIRREL_REDIS_INSTANCE_NAME", "Squirrel_");
    
    Log.Information("Configuring Redis cache: {Configuration}, Instance: {Instance}", 
        redisConfiguration, redisInstanceName);
    
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConfiguration;
        options.InstanceName = redisInstanceName;
    });
}
else
{
    Log.Information("Using in-memory distributed cache");
    builder.Services.AddDistributedMemoryCache();
}

// Configure authentication - Cookie-based authentication
// Note: OIDC and other authentication methods are now handled via plugins
Log.Information("Configuring cookie-based authentication");

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
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

builder.Services.AddAuthorization(options =>
{
    // Role-based policies
    options.AddPolicy("RequireAdmin", policy => 
        policy.RequireRole("Admin"));
    
    options.AddPolicy("RequireEditor", policy =>
        policy.RequireRole("Editor", "Admin"));
    
    // Resource-based policies for page access
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

// Register authorization handlers
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, 
    Squirrel.Wiki.Core.Security.Authorization.PageAccessHandler>();

// Add HttpContextAccessor for UserContext
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext, UserContext>();

// Configure Data Protection for encryption
// Use database storage for all environments (supports multi-instance deployments)
// Keys are automatically cached in memory by the Data Protection system
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<SquirrelDbContext>()
    .SetApplicationName("Squirrel.Wiki");
Log.Information("Data Protection configured with database storage");

// Register Repositories
builder.Services.AddScoped<IPageRepository, PageRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ITagRepository, TagRepository>();
builder.Services.AddScoped<IMenuRepository, MenuRepository>();

// Register Configuration System
builder.Services.AddScoped<Squirrel.Wiki.Core.Configuration.IConfigurationProvider, DefaultConfigurationProvider>();
builder.Services.AddScoped<Squirrel.Wiki.Core.Configuration.IConfigurationProvider, EnvironmentVariableConfigurationProvider>();
builder.Services.AddScoped<Squirrel.Wiki.Core.Configuration.IConfigurationProvider, DatabaseConfigurationProvider>();
builder.Services.AddScoped<IConfigurationService, Squirrel.Wiki.Core.Configuration.ConfigurationService>();
Log.Information("ConfigurationService registered");

// Register Services
builder.Services.AddSingleton<ISlugGenerator, SlugGenerator>();

// Register shared cache service (must be before services that depend on it)
builder.Services.AddScoped<ICacheService, CacheService>();

// Register cache invalidation service (must be before services that depend on it)
builder.Services.AddScoped<ICacheInvalidationService, CacheInvalidationService>();

// Register Event System
builder.Services.AddScoped<Squirrel.Wiki.Core.Events.IEventPublisher, Squirrel.Wiki.Core.Events.EventPublisher>();

// Register Event Handlers for cache invalidation
builder.Services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Pages.PageCreatedEvent>, 
    Squirrel.Wiki.Core.Events.Handlers.PageCacheInvalidationHandler>();
builder.Services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Pages.PageUpdatedEvent>, 
    Squirrel.Wiki.Core.Events.Handlers.PageCacheInvalidationHandler>();
builder.Services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Pages.PageDeletedEvent>, 
    Squirrel.Wiki.Core.Events.Handlers.PageCacheInvalidationHandler>();
builder.Services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Categories.CategoryChangedEvent>, 
    Squirrel.Wiki.Core.Events.Handlers.CategoryCacheInvalidationHandler>();
builder.Services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Tags.TagChangedEvent>, 
    Squirrel.Wiki.Core.Events.Handlers.TagCacheInvalidationHandler>();
builder.Services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Menus.MenuChangedEvent>, 
    Squirrel.Wiki.Core.Events.Handlers.MenuCacheInvalidationHandler>();

Log.Information("Event-based cache invalidation system registered");

// Register Event Handlers for search indexing
builder.Services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Pages.PageCreatedEvent>, 
    Squirrel.Wiki.Core.Events.Handlers.PageSearchIndexHandler>();
builder.Services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Pages.PageUpdatedEvent>, 
    Squirrel.Wiki.Core.Events.Handlers.PageSearchIndexHandler>();
builder.Services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Pages.PageDeletedEvent>, 
    Squirrel.Wiki.Core.Events.Handlers.PageSearchIndexHandler>();
builder.Services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Search.PageIndexRequestedEvent>, 
    Squirrel.Wiki.Core.Events.Handlers.PageIndexRequestedEventHandler>();
builder.Services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Search.PagesIndexRequestedEvent>, 
    Squirrel.Wiki.Core.Events.Handlers.PagesIndexRequestedEventHandler>();
builder.Services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Search.PageRemovedFromIndexEvent>, 
    Squirrel.Wiki.Core.Events.Handlers.PageRemovedFromIndexEventHandler>();
builder.Services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Search.IndexRebuildRequestedEvent>, 
    Squirrel.Wiki.Core.Events.Handlers.IndexRebuildRequestedEventHandler>();
builder.Services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Search.IndexOptimizationRequestedEvent>, 
    Squirrel.Wiki.Core.Events.Handlers.IndexOptimizationRequestedEventHandler>();
builder.Services.AddScoped<Squirrel.Wiki.Core.Events.IEventHandler<Squirrel.Wiki.Core.Events.Search.IndexClearRequestedEvent>, 
    Squirrel.Wiki.Core.Events.Handlers.IndexClearRequestedEventHandler>();

Log.Information("Event-based search indexing system registered");

// Register services that depend on cache services
builder.Services.AddScoped<IMarkdownService, MarkdownService>();

// Register new page-related services (from refactoring)
builder.Services.AddScoped<IPageContentService, PageContentService>();
builder.Services.AddScoped<IPageRenderingService, PageRenderingService>();
builder.Services.AddScoped<IPageLinkService, PageLinkService>();
builder.Services.AddScoped<IPageService, PageService>();

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

// Register CategoryTreeBuilder (now with integrated caching via BaseService)
builder.Services.AddScoped<ICategoryTreeBuilder, CategoryTreeBuilder>();

// Register TagService (now with integrated caching)
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IMenuService, MenuService>();

// Register Search Services
builder.Services.AddScoped<Squirrel.Wiki.Core.Services.Search.DatabaseSearchStrategy>();
builder.Services.AddScoped<ISearchService, Squirrel.Wiki.Core.Services.Search.SearchStrategyService>();
Log.Information("Search services registered (DatabaseSearchStrategy, SearchStrategyService)");

builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<ITimezoneService, TimezoneService>();
builder.Services.AddScoped<IUrlTokenResolver, UrlTokenResolver>();
builder.Services.AddScoped<FooterMarkupParser>();

// Register Web Services
builder.Services.AddScoped<Squirrel.Wiki.Web.Services.INotificationService, Squirrel.Wiki.Web.Services.TempDataNotificationService>();

// Register Authentication Services
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<Squirrel.Wiki.Core.Security.IAuthenticationService, Squirrel.Wiki.Core.Security.AuthenticationService>();
builder.Services.AddScoped<IAdminBootstrapService, AdminBootstrapService>();

// Register core authentication strategies
builder.Services.AddScoped<Squirrel.Wiki.Contracts.Authentication.IAuthenticationStrategy, LocalAuthenticationStrategy>();

// Register Security Services
builder.Services.AddSingleton<ISecretEncryptionService, SecretEncryptionService>();
builder.Services.AddScoped<Squirrel.Wiki.Core.Security.IAuthorizationService, Squirrel.Wiki.Core.Security.AuthorizationService>();

// Register Plugin Services
// Use AppContext.BaseDirectory to get the actual running directory (bin/Debug/net8.0 or bin/Release/net8.0)
// instead of ContentRootPath which points to the project source directory
var pluginsPath = Path.Combine(AppContext.BaseDirectory, "Plugins");
Log.Information("Plugins path configured: {PluginsPath}", pluginsPath);
builder.Services.AddSingleton<Squirrel.Wiki.Plugins.IPluginLoader, Squirrel.Wiki.Plugins.PluginLoader>();
builder.Services.AddScoped<Squirrel.Wiki.Plugins.IPluginLifecycleManager, Squirrel.Wiki.Plugins.PluginLifecycleManager>();
builder.Services.AddScoped<IPluginAuditService, PluginAuditService>();
builder.Services.AddScoped<IPluginService>(sp =>
{
    var context = sp.GetRequiredService<SquirrelDbContext>();
    var pluginLoader = sp.GetRequiredService<Squirrel.Wiki.Plugins.IPluginLoader>();
    var logger = sp.GetRequiredService<ILogger<PluginService>>();
    var cache = sp.GetRequiredService<ICacheService>();
    var eventPublisher = sp.GetRequiredService<IEventPublisher>();
    var encryptionService = sp.GetRequiredService<ISecretEncryptionService>();
    var auditService = sp.GetRequiredService<IPluginAuditService>();
    var userContext = sp.GetRequiredService<IUserContext>();
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
    var configuration = sp.GetRequiredService<IConfigurationService>();
    return new PluginService(context, pluginLoader, logger, cache, eventPublisher, encryptionService, auditService, userContext, httpContextAccessor, sp, pluginsPath, configuration);
});


// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Initialize database (apply migrations and seed data)
string defaultLanguage = "en";
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<SquirrelDbContext>();
        var logger = services.GetRequiredService<ILogger<Program>>();
        
        // Get database initialization settings from MinimalConfigurationService
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
            // Check for custom seed data file path (useful for containerized deployments)
            var customSeedDataPath = minimalConfig.GetValue("SQUIRREL_SEED_DATA_FILE_PATH");
            
            if (!string.IsNullOrEmpty(customSeedDataPath))
            {
                // Resolve relative paths
                var resolvedSeedDataPath = Path.IsPathRooted(customSeedDataPath)
                    ? customSeedDataPath
                    : Path.Combine(AppContext.BaseDirectory, customSeedDataPath);
                
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
                // Use default embedded seed data
                await DatabaseSeeder.SeedAsync(context, logger);
            }
        }
        
        // Ensure admin user exists
        var adminBootstrap = services.GetRequiredService<IAdminBootstrapService>();
        logger.LogInformation("Checking for admin users...");
        await adminBootstrap.EnsureAdminExistsAsync();
        
        // Sync environment variables to database
        var settingsService = services.GetRequiredService<ISettingsService>();
        
        // Initialize plugin service
        var pluginService = services.GetRequiredService<IPluginService>();
        logger.LogInformation("Initializing plugin service...");
        await pluginService.InitializeAsync();
        logger.LogInformation("Plugin service initialized successfully");
        
        // Get default language using IConfigurationService
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
}

// Configure request localization
// Get supported languages from configuration metadata
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

// Configure the HTTP request pipeline
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

// Add global exception handler (before routing)
app.UseGlobalExceptionHandler();

// Add response caching middleware (required for VaryByQueryKeys)
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHealthChecks("/health");

try
{
    Log.Information("Starting Squirrel Wiki v3.0");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
