namespace Squirrel.Wiki.Core.Database.Entities;

/// <summary>
/// Represents an authentication plugin in the database
/// </summary>
public class AuthenticationPlugin
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Plugin identifier (e.g., "oidc-auth", "github-auth")
    /// </summary>
    public required string PluginId { get; set; }

    /// <summary>
    /// Display name of the plugin
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Plugin version
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Whether the plugin is enabled
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Whether the plugin is configured
    /// </summary>
    public bool IsConfigured { get; set; }

    /// <summary>
    /// Load order (lower numbers load first)
    /// </summary>
    public int LoadOrder { get; set; }

    /// <summary>
    /// Whether this is a core plugin (cannot be uninstalled)
    /// </summary>
    public bool IsCorePlugin { get; set; }

    /// <summary>
    /// When the plugin was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the plugin was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Plugin settings
    /// </summary>
    public ICollection<AuthenticationPluginSetting> Settings { get; set; } = new List<AuthenticationPluginSetting>();
}
