using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Serilog;
using Squirrel.Wiki.Core.Configuration;
using Squirrel.Wiki.Core.Database;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Exceptions;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Services;
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

// Add response caching
builder.Services.AddResponseCaching();

// Configure search settings
builder.Services.Configure<Squirrel.Wiki.Core.Services.SearchSettings>(options =>
{
    options.IndexPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "SearchIndex");
});

// Configure database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var databaseProvider = builder.Configuration["Database:Provider"] ?? "PostgreSQL";

// For SQLite, resolve relative paths to be relative to the executable directory
if (databaseProvider.Equals("SQLite", StringComparison.OrdinalIgnoreCase) && 
    !string.IsNullOrEmpty(connectionString))
{
    // Parse the connection string to extract the Data Source
    var builder2 = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
    var dataSource = builder2.DataSource;
    
    // If the path is relative (doesn't start with / or contain :), make it relative to executable
    if (!string.IsNullOrEmpty(dataSource) && 
        !Path.IsPathRooted(dataSource))
    {
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

// Configure distributed caching
// Check environment variables first for cache provider configuration
var cacheProvider = Environment.GetEnvironmentVariable("SQUIRREL_CACHE_PROVIDER") 
    ?? builder.Configuration["Caching:Provider"] 
    ?? "Memory";

Log.Information("Configuring cache provider: {Provider}", cacheProvider);

if (cacheProvider.Equals("Redis", StringComparison.OrdinalIgnoreCase))
{
    var redisConfiguration = Environment.GetEnvironmentVariable("SQUIRREL_REDIS_CONFIGURATION")
        ?? builder.Configuration["Caching:Redis:Configuration"] 
        ?? "localhost:6379";
    
    var redisInstanceName = Environment.GetEnvironmentVariable("SQUIRREL_REDIS_INSTANCE_NAME")
        ?? builder.Configuration["Caching:Redis:InstanceName"] 
        ?? "Squirrel_";
    
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
    // Require actual roles for both development and production
    options.AddPolicy("RequireAdmin", policy => 
        policy.RequireRole("Admin"));
    
    options.AddPolicy("RequireEditor", policy =>
        policy.RequireRole("Editor", "Admin"));
});

// Add HttpContextAccessor for UserContext
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext, UserContext>();

// Configure Data Protection for encryption
// Use database storage for distributed deployments, file system for development
if (builder.Environment.IsDevelopment())
{
    // Development: Use file system (simpler for single-instance dev)
    var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtection-Keys");
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
        .SetApplicationName("Squirrel.Wiki");
    Log.Information("Data Protection configured with file system storage (development)");
}
else
{
    // Production: Use database storage for multi-instance deployments
    // Keys are automatically cached in memory by the Data Protection system
    builder.Services.AddDataProtection()
        .PersistKeysToDbContext<SquirrelDbContext>()
        .SetApplicationName("Squirrel.Wiki");
    Log.Information("Data Protection configured with database storage (production)");
}

// Register Repositories
builder.Services.AddScoped<IPageRepository, PageRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ITagRepository, TagRepository>();
builder.Services.AddScoped<IMenuRepository, MenuRepository>();

// Register Configuration Providers
builder.Services.AddSingleton<EnvironmentVariableProvider>();

// Register Services
builder.Services.AddSingleton<ISlugGenerator, SlugGenerator>();

// Register shared cache service (must be before services that depend on it)
builder.Services.AddScoped<ICacheService, CacheService>();

// Register cache invalidation service (must be before services that depend on it)
builder.Services.AddScoped<ICacheInvalidationService, CacheInvalidationService>();

// Register services that depend on cache services
builder.Services.AddScoped<IMarkdownService, MarkdownService>();
builder.Services.AddScoped<IPageService, PageService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

// Register CategoryTreeBuilder (now with integrated caching via BaseService)
builder.Services.AddScoped<ICategoryTreeBuilder, CategoryTreeBuilder>();

// Register TagService (now with integrated caching)
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<ISearchService, LuceneSearchService>();
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
builder.Services.AddScoped<IPluginAuditService, PluginAuditService>();
builder.Services.AddScoped<IPluginService>(sp =>
{
    var context = sp.GetRequiredService<SquirrelDbContext>();
    var pluginLoader = sp.GetRequiredService<Squirrel.Wiki.Plugins.IPluginLoader>();
    var logger = sp.GetRequiredService<ILogger<PluginService>>();
    var cache = sp.GetRequiredService<ICacheService>();
    var cacheInvalidation = sp.GetRequiredService<ICacheInvalidationService>();
    var encryptionService = sp.GetRequiredService<ISecretEncryptionService>();
    var auditService = sp.GetRequiredService<IPluginAuditService>();
    var userContext = sp.GetRequiredService<IUserContext>();
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
    var envProvider = sp.GetRequiredService<EnvironmentVariableProvider>();
    return new PluginService(context, pluginLoader, logger, cache, cacheInvalidation, encryptionService, auditService, userContext, httpContextAccessor, envProvider, pluginsPath);
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
        
        var autoMigrate = builder.Configuration.GetValue<bool>("Database:AutoMigrate");
        var seedData = builder.Configuration.GetValue<bool>("Database:SeedData");
        
        if (autoMigrate)
        {
            logger.LogInformation("Applying database migrations...");
            await context.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully");
        }
        
        if (seedData)
        {
            await DatabaseSeeder.SeedAsync(context, logger);
        }
        
        // Ensure admin user exists
        var adminBootstrap = services.GetRequiredService<IAdminBootstrapService>();
        logger.LogInformation("Checking for admin users...");
        await adminBootstrap.EnsureAdminExistsAsync();
        
        // Sync environment variables to database
        var settingsService = services.GetRequiredService<ISettingsService>();
        logger.LogInformation("Synchronizing environment variables to database...");
        await settingsService.SyncEnvironmentVariablesAsync();
        logger.LogInformation("Environment variable synchronization complete");
        
        // Initialize plugin service
        var pluginService = services.GetRequiredService<IPluginService>();
        logger.LogInformation("Initializing plugin service...");
        await pluginService.InitializeAsync();
        logger.LogInformation("Plugin service initialized successfully");
        
        // Get default language using EnvironmentVariableProvider pattern
        var languageEnvProvider = services.GetRequiredService<EnvironmentVariableProvider>();
        
        // Check if DefaultLanguage is set via environment variable
        if (languageEnvProvider.IsFromEnvironment("DefaultLanguage"))
        {
            defaultLanguage = languageEnvProvider.GetValue("DefaultLanguage") ?? "en";
            logger.LogInformation("Default language loaded from environment variable: {Language}", defaultLanguage);
        }
        else
        {
            // Fall back to database setting
            defaultLanguage = await settingsService.GetSettingAsync<string>("DefaultLanguage") ?? "en";
            logger.LogInformation("Default language loaded from database: {Language}", defaultLanguage);
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database");
        throw;
    }
}

// Configure request localization
var supportedCultures = new[]
{
    new CultureInfo("en"),
    new CultureInfo("es"),
    new CultureInfo("fr"),
    new CultureInfo("de"),
    new CultureInfo("it")
};

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
