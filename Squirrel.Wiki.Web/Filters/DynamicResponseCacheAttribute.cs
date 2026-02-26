using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Squirrel.Wiki.Core.Services;
using Squirrel.Wiki.Core.Services.Configuration;
using static Squirrel.Wiki.Core.Configuration.ConfigurationMetadataRegistry.ConfigurationKeys;

namespace Squirrel.Wiki.Web.Filters;

/// <summary>
/// Response cache attribute that reads cache duration from settings
/// Supports both database settings and environment variable overrides
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class DynamicResponseCacheAttribute : ActionFilterAttribute
{
    /// <summary>
    /// Optional cache profile name for specific scenarios
    /// </summary>
    public string? CacheProfile { get; set; }

    /// <summary>
    /// Whether to vary cache by query string parameters
    /// </summary>
    public string[]? VaryByQueryKeys { get; set; }

    /// <summary>
    /// Whether to vary cache by specific headers (e.g., "Cookie" for authentication)
    /// </summary>
    public string? VaryByHeader { get; set; }

    /// <summary>
    /// Cache location (Any, Client, None)
    /// </summary>
    public ResponseCacheLocation Location { get; set; } = ResponseCacheLocation.Any;

    /// <summary>
    /// Whether to prevent storing the response
    /// </summary>
    public bool NoStore { get; set; }

    /// <summary>
    /// Override duration in seconds (if > 0, ignores settings)
    /// Use this for special cases like error pages
    /// Set to -1 to use default settings behavior
    /// </summary>
    public int OverrideDuration { get; set; } = -1;

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Execute the action first
        var executedContext = await next();

        // Only apply caching to successful responses
        if (executedContext.Result is not StatusCodeResult statusResult || 
            (statusResult.StatusCode >= 200 && statusResult.StatusCode < 300))
        {
            await ApplyCacheHeadersAsync(context);
        }
    }

    private async Task ApplyCacheHeadersAsync(ActionExecutingContext context)
    {
        var response = context.HttpContext.Response;

        // Handle NoStore case (e.g., error pages)
        if (NoStore)
        {
            response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            response.Headers["Pragma"] = "no-cache";
            response.Headers["Expires"] = "0";
            return;
        }

        // Get settings service
        var settingsService = context.HttpContext.RequestServices
            .GetRequiredService<ISettingsService>();

        // Determine if caching is enabled
        bool enableCaching;
        int cacheDurationMinutes;

        if (OverrideDuration >= 0)
        {
            // Use override duration (for special cases)
            enableCaching = OverrideDuration > 0;
            cacheDurationMinutes = OverrideDuration / 60;
        }
        else
        {
            // Read from settings (which handles environment variable overrides)
            var enableCachingSetting = await settingsService.GetSettingAsync<bool?>(SQUIRREL_ENABLE_RESPONSE_CACHING);
            var cacheDurationSetting = await settingsService.GetSettingAsync<int?>(SQUIRREL_RESPONSE_CACHE_DURATION_MINUTES);

            // Use settings with defaults
            enableCaching = enableCachingSetting ?? true;  // Default to true
            cacheDurationMinutes = cacheDurationSetting ?? 5;  // Default to 5 minutes
        }

        if (!enableCaching)
        {
            // Caching disabled - set no-cache headers
            response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            response.Headers["Pragma"] = "no-cache";
            response.Headers["Expires"] = "0";
            return;
        }

        // Build Cache-Control header
        var cacheControlParts = new List<string>();

        // Add location
        switch (Location)
        {
            case ResponseCacheLocation.Any:
                cacheControlParts.Add("public");
                break;
            case ResponseCacheLocation.Client:
                cacheControlParts.Add("private");
                break;
            case ResponseCacheLocation.None:
                cacheControlParts.Add("no-cache");
                break;
        }

        // Add max-age
        var maxAgeSeconds = cacheDurationMinutes * 60;
        cacheControlParts.Add($"max-age={maxAgeSeconds}");

        // Set Cache-Control header
        response.Headers["Cache-Control"] = string.Join(", ", cacheControlParts);

        // Handle VaryByHeader (important for authentication)
        if (!string.IsNullOrEmpty(VaryByHeader))
        {
            response.Headers["Vary"] = VaryByHeader;
        }

        // Note: VaryByQueryKeys is handled by ASP.NET Core's response caching middleware
        // We set it in the context items for the middleware to pick up
        if (VaryByQueryKeys != null && VaryByQueryKeys.Length > 0)
        {
            context.HttpContext.Items["VaryByQueryKeys"] = VaryByQueryKeys;
        }
    }
}
