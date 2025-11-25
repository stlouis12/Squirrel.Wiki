namespace Squirrel.Wiki.Core.Configuration;

/// <summary>
/// General site configuration settings
/// </summary>
public class GeneralConfiguration
{
    public string SiteName { get; set; } = "Squirrel Wiki";
    public string SiteUrl { get; set; } = "";
    public string DefaultLanguage { get; set; } = "en";
    public string TimeZone { get; set; } = "UTC";
}

/// <summary>
/// Security and authentication configuration settings
/// </summary>
public class SecurityConfiguration
{
    public bool AllowAnonymousReading { get; set; } = false;
    public int SessionTimeoutMinutes { get; set; } = 480;
    public int MaxLoginAttempts { get; set; } = 5;
    public int AccountLockDurationMinutes { get; set; } = 30;
}

/// <summary>
/// Content management configuration settings
/// </summary>
public class ContentConfiguration
{
    public string DefaultPageTemplate { get; set; } = "";
    public int MaxPageTitleLength { get; set; } = 200;
    public bool EnablePageVersioning { get; set; } = false;
}

/// <summary>
/// Search functionality configuration settings
/// </summary>
public class SearchConfiguration
{
    public int SearchResultsPerPage { get; set; } = 20;
    public bool EnableFuzzySearch { get; set; } = false;
    public int SearchMinimumLength { get; set; } = 3;
}

/// <summary>
/// Performance and caching configuration settings
/// </summary>
public class PerformanceConfiguration
{
    public bool EnableCaching { get; set; } = false;
    public int CacheDurationMinutes { get; set; } = 60;
}
