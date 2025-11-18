using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Squirrel.Wiki.Core.Database;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Services;

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
builder.Services.AddControllersWithViews();

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
            throw new InvalidOperationException($"Unsupported database provider: {databaseProvider}");
    }

    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Configure distributed caching
var cachingProvider = builder.Configuration["Caching:Provider"] ?? "Memory";
if (cachingProvider.Equals("Redis", StringComparison.OrdinalIgnoreCase))
{
    var redisConfiguration = builder.Configuration["Caching:Redis:Configuration"];
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConfiguration;
        options.InstanceName = "Squirrel_";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

// Configure authentication based on environment
var useOidc = builder.Configuration.GetValue<bool>("Authentication:UseOpenIdConnect");

if (useOidc && !builder.Environment.IsDevelopment())
{
    // Production: OpenID Connect
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = "oidc";
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "Squirrel.Auth";
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    })
    .AddOpenIdConnect("oidc", options =>
    {
        options.Authority = builder.Configuration["Authentication:OpenIdConnect:Authority"];
        options.ClientId = builder.Configuration["Authentication:OpenIdConnect:ClientId"];
        options.ClientSecret = builder.Configuration["Authentication:OpenIdConnect:ClientSecret"];
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        
        // Map claims
        options.TokenValidationParameters.RoleClaimType = "role";
        options.TokenValidationParameters.NameClaimType = "name";
    });
}
else
{
    // Development: Simple cookie authentication
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
}

builder.Services.AddAuthorization(options =>
{
    // In development, allow all authenticated users (or even anonymous) to act as editors
    // In production, require actual roles
    if (builder.Environment.IsDevelopment())
    {
        options.AddPolicy("RequireAdmin", policy => 
            policy.RequireAssertion(context => true)); // Allow all in dev
        
        options.AddPolicy("RequireEditor", policy =>
            policy.RequireAssertion(context => true)); // Allow all in dev
    }
    else
    {
        options.AddPolicy("RequireAdmin", policy => 
            policy.RequireRole("Admin"));
        
        options.AddPolicy("RequireEditor", policy =>
            policy.RequireRole("Editor", "Admin"));
    }
});

// Add HttpContextAccessor for UserContext
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext, UserContext>();

// Register Repositories
builder.Services.AddScoped<IPageRepository, PageRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ITagRepository, TagRepository>();
builder.Services.AddScoped<IMenuRepository, MenuRepository>();

// Register Services
builder.Services.AddScoped<IMarkdownService, MarkdownService>();
builder.Services.AddScoped<IPageService, PageService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<ISearchService, LuceneSearchService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<FooterMarkupParser>();

// Add health checks
builder.Services.AddHealthChecks();

// Add API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Initialize database (apply migrations and seed data)
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
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database");
        throw;
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

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
