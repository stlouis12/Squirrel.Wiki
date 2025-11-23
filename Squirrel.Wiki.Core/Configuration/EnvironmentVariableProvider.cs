using Microsoft.Extensions.Logging;

namespace Squirrel.Wiki.Core.Configuration;

/// <summary>
/// Provides access to application settings from environment variables with fallback to default values
/// </summary>
public class EnvironmentVariableProvider
{
    private readonly ILogger<EnvironmentVariableProvider> _logger;
    
    // Validation constraints for settings
    private static readonly Dictionary<string, SettingConstraints> Constraints = new()
    {
        // Numeric constraints
        { "SessionTimeoutMinutes", new SettingConstraints { MinValue = 30, MaxValue = 20160 } },
        { "MaxLoginAttempts", new SettingConstraints { MinValue = 3, MaxValue = 10 } },
        { "AccountLockDurationMinutes", new SettingConstraints { MinValue = 5, MaxValue = 60 } },
        { "MaxPageTitleLength", new SettingConstraints { MinValue = 25, MaxValue = 200 } },
        { "CacheDurationMinutes", new SettingConstraints { MinValue = 5, MaxValue = 120 } },
        
        // Dropdown/enum constraints
        { "DefaultLanguage", new SettingConstraints { AllowedValues = new[] { "en", "es", "fr", "de", "it" } } },
        { "CacheProvider", new SettingConstraints { AllowedValues = new[] { "Memory", "Redis" } } },
        
        // URL constraints
        { "SiteUrl", new SettingConstraints { MustBeUrl = true } }
    };
    
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
        new EnvironmentVariableMapping("MaxPageTitleLength", "SQUIRREL_MAX_PAGE_TITLE_LENGTH", "200"),
        new EnvironmentVariableMapping("EnablePageVersioning", "SQUIRREL_ENABLE_PAGE_VERSIONING", "false"),
        
        // Search Settings
        new EnvironmentVariableMapping("SearchResultsPerPage", "SQUIRREL_SEARCH_RESULTS_PER_PAGE", "20"),
        new EnvironmentVariableMapping("EnableFuzzySearch", "SQUIRREL_ENABLE_FUZZY_SEARCH", "false"),
        new EnvironmentVariableMapping("SearchMinimumLength", "SQUIRREL_SEARCH_MINIMUM_LENGTH", "3"),
        
        // Performance Settings
        new EnvironmentVariableMapping("EnableCaching", "SQUIRREL_ENABLE_CACHING", "false"),
        new EnvironmentVariableMapping("CacheDurationMinutes", "SQUIRREL_CACHE_DURATION_MINUTES", "60")
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
            // Validate the environment variable value
            if (!ValidateValue(settingKey, envValue, mapping.EnvironmentVariableName))
            {
                _logger.LogWarning(
                    "Environment variable '{EnvVar}' has invalid value for setting '{SettingKey}'. Using default value instead.",
                    mapping.EnvironmentVariableName, settingKey);
                return mapping.DefaultValue;
            }
            
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
    /// Checks if a setting has an environment variable defined with a valid value
    /// </summary>
    /// <param name="settingKey">The setting key</param>
    /// <returns>True if environment variable is set and has a valid value</returns>
    public bool IsFromEnvironment(string settingKey)
    {
        var mapping = Mappings.FirstOrDefault(m => m.SettingKey == settingKey);
        if (mapping == null)
        {
            return false;
        }

        var envValue = Environment.GetEnvironmentVariable(mapping.EnvironmentVariableName);
        if (envValue == null)
        {
            return false;
        }

        // Only return true if the value is valid
        return ValidateValue(settingKey, envValue, mapping.EnvironmentVariableName);
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
    /// Validates a setting value against defined constraints
    /// </summary>
    /// <param name="settingKey">The setting key</param>
    /// <param name="value">The value to validate</param>
    /// <param name="envVarName">The environment variable name (for logging)</param>
    /// <returns>True if valid, false otherwise</returns>
    private bool ValidateValue(string settingKey, string value, string envVarName)
    {
        // If no constraints defined, value is valid
        if (!Constraints.TryGetValue(settingKey, out var constraints))
        {
            return true;
        }

        // Validate numeric ranges
        if (constraints.MinValue.HasValue || constraints.MaxValue.HasValue)
        {
            if (!int.TryParse(value, out var numValue))
            {
                _logger.LogWarning(
                    "Environment variable '{EnvVar}' for setting '{SettingKey}' must be a number. Got: {Value}",
                    envVarName, settingKey, value);
                return false;
            }

            if (constraints.MinValue.HasValue && numValue < constraints.MinValue.Value)
            {
                _logger.LogWarning(
                    "Environment variable '{EnvVar}' for setting '{SettingKey}' must be at least {Min}. Got: {Value}",
                    envVarName, settingKey, constraints.MinValue.Value, numValue);
                return false;
            }

            if (constraints.MaxValue.HasValue && numValue > constraints.MaxValue.Value)
            {
                _logger.LogWarning(
                    "Environment variable '{EnvVar}' for setting '{SettingKey}' must be at most {Max}. Got: {Value}",
                    envVarName, settingKey, constraints.MaxValue.Value, numValue);
                return false;
            }
        }

        // Validate allowed values (dropdown/enum)
        if (constraints.AllowedValues != null && constraints.AllowedValues.Length > 0)
        {
            if (!constraints.AllowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Environment variable '{EnvVar}' for setting '{SettingKey}' must be one of: {AllowedValues}. Got: {Value}",
                    envVarName, settingKey, string.Join(", ", constraints.AllowedValues), value);
                return false;
            }
        }

        // Validate URL format
        if (constraints.MustBeUrl && !string.IsNullOrEmpty(value))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                _logger.LogWarning(
                    "Environment variable '{EnvVar}' for setting '{SettingKey}' must be a valid HTTP/HTTPS URL. Got: {Value}",
                    envVarName, settingKey, value);
                return false;
            }
        }

        return true;
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

/// <summary>
/// Represents validation constraints for a setting
/// </summary>
public class SettingConstraints
{
    public int? MinValue { get; set; }
    public int? MaxValue { get; set; }
    public string[]? AllowedValues { get; set; }
    public bool MustBeUrl { get; set; }
}
