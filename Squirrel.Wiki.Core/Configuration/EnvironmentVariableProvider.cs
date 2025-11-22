using Microsoft.Extensions.Logging;

namespace Squirrel.Wiki.Core.Configuration;

/// <summary>
/// Provides access to application settings from environment variables with fallback to default values
/// </summary>
public class EnvironmentVariableProvider
{
    private readonly ILogger<EnvironmentVariableProvider> _logger;
    private static readonly List<EnvironmentVariableMapping> Mappings = new()
    {
        // General Settings
        new EnvironmentVariableMapping("SiteName", "SQUIRREL_SITE_NAME", "Squirrel Wiki"),
        new EnvironmentVariableMapping("SiteUrl", "SQUIRREL_SITE_URL", ""),
        new EnvironmentVariableMapping("DefaultLanguage", "SQUIRREL_DEFAULT_LANGUAGE", "en"),
        new EnvironmentVariableMapping("TimeZone", "SQUIRREL_TIMEZONE", "UTC"),
        
        // Security Settings
        new EnvironmentVariableMapping("AllowAnonymousReading", "SQUIRREL_ALLOW_ANONYMOUS_READING", "false"),
        new EnvironmentVariableMapping("SessionTimeoutMinutes", "SQUIRREL_SESSION_TIMEOUT_MINUTES", "480"),
        new EnvironmentVariableMapping("MaxLoginAttempts", "SQUIRREL_MAX_LOGIN_ATTEMPTS", "5"),
        new EnvironmentVariableMapping("AccountLockDurationMinutes", "SQUIRREL_ACCOUNT_LOCK_DURATION_MINUTES", "30"),
        
        // Content Settings
        new EnvironmentVariableMapping("DefaultPageTemplate", "SQUIRREL_DEFAULT_PAGE_TEMPLATE", ""),
        new EnvironmentVariableMapping("EnableMarkdownExtensions", "SQUIRREL_ENABLE_MARKDOWN_EXTENSIONS", "false"),
        new EnvironmentVariableMapping("MaxPageTitleLength", "SQUIRREL_MAX_PAGE_TITLE_LENGTH", "200"),
        new EnvironmentVariableMapping("EnablePageVersioning", "SQUIRREL_ENABLE_PAGE_VERSIONING", "false"),
        
        // Search Settings
        new EnvironmentVariableMapping("SearchResultsPerPage", "SQUIRREL_SEARCH_RESULTS_PER_PAGE", "20"),
        new EnvironmentVariableMapping("EnableFuzzySearch", "SQUIRREL_ENABLE_FUZZY_SEARCH", "false"),
        new EnvironmentVariableMapping("SearchMinimumLength", "SQUIRREL_SEARCH_MINIMUM_LENGTH", "3"),
        
        // Performance Settings
        new EnvironmentVariableMapping("EnableCaching", "SQUIRREL_ENABLE_CACHING", "false"),
        new EnvironmentVariableMapping("CacheDurationMinutes", "SQUIRREL_CACHE_DURATION_MINUTES", "60"),
        new EnvironmentVariableMapping("EnableCompression", "SQUIRREL_ENABLE_COMPRESSION", "false")
    };

    public EnvironmentVariableProvider(ILogger<EnvironmentVariableProvider> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets all environment variable mappings
    /// </summary>
    public IReadOnlyList<EnvironmentVariableMapping> GetMappings() => Mappings.AsReadOnly();

    /// <summary>
    /// Gets the value for a setting key from environment variable or default
    /// </summary>
    /// <param name="settingKey">The setting key</param>
    /// <returns>The value from environment variable or default, or null if not found</returns>
    public string? GetValue(string settingKey)
    {
        var mapping = Mappings.FirstOrDefault(m => m.SettingKey == settingKey);
        if (mapping == null)
        {
            return null;
        }

        var envValue = Environment.GetEnvironmentVariable(mapping.EnvironmentVariableName);

        if (envValue != null)
        {
            var displayValue = IsSecret(mapping.EnvironmentVariableName) 
                ? new string('*', envValue.Length) 
                : envValue;
            
            _logger.LogInformation(
                "Loaded setting '{SettingKey}' from environment variable '{EnvVar}' = {Value}",
                settingKey, mapping.EnvironmentVariableName, displayValue);
            
            return envValue;
        }

        if (!string.IsNullOrEmpty(mapping.DefaultValue))
        {
            _logger.LogDebug(
                "Using default value for setting '{SettingKey}' (env var '{EnvVar}' not set) = {Value}",
                settingKey, mapping.EnvironmentVariableName, mapping.DefaultValue);
        }

        return mapping.DefaultValue;
    }

    /// <summary>
    /// Checks if a setting has an environment variable defined
    /// </summary>
    /// <param name="settingKey">The setting key</param>
    /// <returns>True if environment variable is set</returns>
    public bool IsFromEnvironment(string settingKey)
    {
        var mapping = Mappings.FirstOrDefault(m => m.SettingKey == settingKey);
        if (mapping == null)
        {
            return false;
        }

        var envValue = Environment.GetEnvironmentVariable(mapping.EnvironmentVariableName);
        return envValue != null;
    }

    /// <summary>
    /// Gets the environment variable name for a setting key
    /// </summary>
    /// <param name="settingKey">The setting key</param>
    /// <returns>The environment variable name, or null if not found</returns>
    public string? GetEnvironmentVariableName(string settingKey)
    {
        return Mappings.FirstOrDefault(m => m.SettingKey == settingKey)?.EnvironmentVariableName;
    }

    /// <summary>
    /// Gets all settings that are currently loaded from environment variables
    /// </summary>
    /// <returns>Dictionary of setting keys and their environment variable values</returns>
    public Dictionary<string, string> GetAllEnvironmentSettings()
    {
        var result = new Dictionary<string, string>();

        foreach (var mapping in Mappings)
        {
            var envValue = Environment.GetEnvironmentVariable(mapping.EnvironmentVariableName);
            if (envValue != null)
            {
                result[mapping.SettingKey] = envValue;
            }
        }

        _logger.LogInformation("Found {Count} settings from environment variables", result.Count);
        return result;
    }

    /// <summary>
    /// Determines if an environment variable should be treated as a secret
    /// </summary>
    private static bool IsSecret(string environmentVariableName)
    {
        var secretSuffixes = new[] { "_PASSWORD", "_PASS", "_SECRET", "_KEY", "_TOKEN" };
        return secretSuffixes.Any(suffix => 
            environmentVariableName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Represents a mapping between a setting key and an environment variable
/// </summary>
public class EnvironmentVariableMapping
{
    public string SettingKey { get; }
    public string EnvironmentVariableName { get; }
    public string DefaultValue { get; }

    public EnvironmentVariableMapping(string settingKey, string environmentVariableName, string defaultValue)
    {
        SettingKey = settingKey;
        EnvironmentVariableName = environmentVariableName;
        DefaultValue = defaultValue;
    }
}
