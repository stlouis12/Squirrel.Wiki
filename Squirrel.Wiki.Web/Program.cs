using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Serilog;
using Squirrel.Wiki.Core.Configuration;
using Squirrel.Wiki.Core.Database;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Services.Infrastructure;
using Squirrel.Wiki.Web.Extensions;
using Squirrel.Wiki.Web.Resources;
using PathHelper = Squirrel.Wiki.Core.Services.Infrastructure.PathHelper;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/squirrel-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add MVC with localization
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(SharedResources));
    });

builder.Services.AddLocalization();
builder.Services.AddSingleton<IStringLocalizer>(sp =>
{
    var factory = sp.GetRequiredService<IStringLocalizerFactory>();
    return factory.Create(typeof(SharedResources));
});

// Configure AutoMapper
builder.Services.AddAutoMapper(config => { }, 
    typeof(Squirrel.Wiki.Core.Mapping.UserMappingProfile).Assembly);

// Add response caching
builder.Services.AddResponseCaching();

// Create MinimalConfigurationService for startup configuration
var startupLogger = LoggerFactory.Create(config => config.AddConsole()).CreateLogger<MinimalConfigurationService>();
var minimalConfig = new MinimalConfigurationService(startupLogger);

// Configure Kestrel server limits
builder.WebHost.ConfigureSquirrelWikiKestrel(startupLogger);

// Add session support for OIDC authentication
var sessionTimeoutMinutes = int.TryParse(minimalConfig.GetValue("SQUIRREL_SESSION_TIMEOUT_MINUTES"), out var timeout) && timeout > 0 
    ? timeout 
    : 480; // Default to 8 hours

Log.Information("Session timeout configured: {Minutes} minutes (from {Source})",
    sessionTimeoutMinutes,
    minimalConfig.GetSource("SQUIRREL_SESSION_TIMEOUT_MINUTES"));

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(sessionTimeoutMinutes);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// Get application data path
var appDataPath = minimalConfig.GetValue("SQUIRREL_APP_DATA_PATH", "App_Data");
var resolvedAppDataPath = PathHelper.ResolveAndEnsurePath(appDataPath);
Log.Information("Application data path: {AppDataPath} (from {Source})",
    resolvedAppDataPath,
    minimalConfig.GetSource("SQUIRREL_APP_DATA_PATH"));

// Configure Squirrel Wiki services using extension methods
builder.Services.AddSquirrelWikiRequestSizeLimits(startupLogger);
builder.Services.AddSquirrelWikiDatabase(minimalConfig, appDataPath, startupLogger);
builder.Services.AddSquirrelWikiCaching(minimalConfig, startupLogger);
builder.Services.AddSquirrelWikiAuthentication(startupLogger);

// Add HttpContextAccessor and UserContext
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext, UserContext>();

// Configure Data Protection
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<SquirrelDbContext>()
    .SetApplicationName("Squirrel.Wiki");
Log.Information("Data Protection configured with database storage");

// Register repositories and services
var pluginsPath = Path.Combine(AppContext.BaseDirectory, "Plugins");
builder.Services.AddSquirrelWikiRepositories();
builder.Services.AddSquirrelWikiServices(pluginsPath, startupLogger);

// Add health checks
builder.Services.AddSquirrelWikiHealthChecks(minimalConfig, startupLogger);

var app = builder.Build();

// Initialize database and get default language
var defaultLanguage = await app.InitializeDatabaseAsync(minimalConfig, appDataPath);

// Configure middleware pipeline
app.ConfigureSquirrelWikiMiddleware(defaultLanguage);

// Map endpoints
app.MapSquirrelWikiEndpoints();

try
{
    Log.Information("Starting Squirrel Wiki v1.0");
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
