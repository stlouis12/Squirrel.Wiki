namespace Squirrel.Wiki.Contracts.Plugins;

/// <summary>
/// Describes a configuration item for a plugin
/// </summary>
public class PluginConfigurationItem
{
    /// <summary>
    /// Configuration key (e.g., "ClientId", "Authority")
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Display name for the configuration item
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Description of what this configuration item does
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Type of configuration value
    /// </summary>
    public PluginConfigType Type { get; set; } = PluginConfigType.Text;

    /// <summary>
    /// Whether this configuration item is required
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Whether this is sensitive data (passwords, secrets, etc.)
    /// </summary>
    public bool IsSecret { get; set; }

    /// <summary>
    /// Default value for this configuration item
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Validation pattern (regex) for this configuration item
    /// </summary>
    public string? ValidationPattern { get; set; }

    /// <summary>
    /// Validation error message to display if validation fails
    /// </summary>
    public string? ValidationErrorMessage { get; set; }

    /// <summary>
    /// For dropdown type, the available options
    /// </summary>
    public string[]? DropdownOptions { get; set; }

    /// <summary>
    /// Display order (lower numbers appear first)
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Suggested environment variable name for this configuration item.
    /// If specified, the admin UI can show this as an alternative to database configuration.
    /// Naming convention: PLUGIN_{PLUGINNAME}_{SETTING}
    /// Example: "PLUGIN_OIDC_AUTHORITY", "PLUGIN_OIDC_CLIENT_ID"
    /// </summary>
    public string? EnvironmentVariableName { get; set; }
}

/// <summary>
/// Types of configuration values
/// </summary>
public enum PluginConfigType
{
    /// <summary>
    /// Plain text input
    /// </summary>
    Text = 0,

    /// <summary>
    /// URL input with validation
    /// </summary>
    Url = 1,

    /// <summary>
    /// Secret/password input (masked)
    /// </summary>
    Secret = 2,

    /// <summary>
    /// Boolean checkbox
    /// </summary>
    Boolean = 3,

    /// <summary>
    /// Numeric input
    /// </summary>
    Number = 4,

    /// <summary>
    /// Dropdown selection
    /// </summary>
    Dropdown = 5,

    /// <summary>
    /// Multi-line text area
    /// </summary>
    TextArea = 6
}
