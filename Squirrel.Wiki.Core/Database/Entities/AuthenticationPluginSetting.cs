namespace Squirrel.Wiki.Core.Database.Entities;

/// <summary>
/// Represents a configuration setting for an authentication plugin
/// </summary>
public class AuthenticationPluginSetting
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Plugin this setting belongs to
    /// </summary>
    public Guid PluginId { get; set; }

    /// <summary>
    /// Configuration key
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Configuration value (encrypted if IsSecret is true)
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Whether this value comes from an environment variable
    /// </summary>
    public bool IsFromEnvironment { get; set; }

    /// <summary>
    /// Environment variable name if IsFromEnvironment is true
    /// </summary>
    public string? EnvironmentVariableName { get; set; }

    /// <summary>
    /// Whether this is a secret value (should be encrypted)
    /// </summary>
    public bool IsSecret { get; set; }

    /// <summary>
    /// When the setting was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the setting was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property to the plugin
    /// </summary>
    public AuthenticationPlugin Plugin { get; set; } = null!;
}
