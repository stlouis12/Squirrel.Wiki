namespace Squirrel.Wiki.Contracts.Configuration;

/// <summary>
/// Metadata about a configuration property
/// </summary>
public class ConfigurationProperty
{
    /// <summary>
    /// The configuration key (e.g., "SQUIRREL_SITE_NAME")
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Display name for UI
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this setting does
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Category for grouping settings (e.g., "General", "Security", "Performance")
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// The .NET type of the value
    /// </summary>
    public Type ValueType { get; set; } = typeof(string);

    /// <summary>
    /// The default value if not configured
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// The environment variable name that can override this setting
    /// </summary>
    public string? EnvironmentVariable { get; set; }

    /// <summary>
    /// Whether this is a secret value (should be masked in UI)
    /// </summary>
    public bool IsSecret { get; set; }

    /// <summary>
    /// Whether this setting can be modified at runtime via the UI
    /// </summary>
    public bool AllowRuntimeModification { get; set; }

    /// <summary>
    /// Whether this setting should be visible in the Settings UI.
    /// Set to false for startup-only settings that shouldn't be displayed to users.
    /// </summary>
    public bool IsVisibleInUI { get; set; } = true;

    /// <summary>
    /// Validation rules for this setting
    /// </summary>
    public ValidationRules? Validation { get; set; }
}

/// <summary>
/// Validation rules for a configuration property
/// </summary>
public class ValidationRules
{
    /// <summary>
    /// Minimum numeric value (for int/long/double types)
    /// </summary>
    public int? MinValue { get; set; }

    /// <summary>
    /// Maximum numeric value (for int/long/double types)
    /// </summary>
    public int? MaxValue { get; set; }

    /// <summary>
    /// Allowed string values (for enum-like settings)
    /// </summary>
    public string[]? AllowedValues { get; set; }

    /// <summary>
    /// Whether the value must be a valid URL
    /// </summary>
    public bool MustBeUrl { get; set; }

    /// <summary>
    /// Regular expression pattern the value must match
    /// </summary>
    public string? RegexPattern { get; set; }

    /// <summary>
    /// Custom validator method name (for complex validation)
    /// </summary>
    public string? CustomValidator { get; set; }
}
