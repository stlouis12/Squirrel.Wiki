namespace Squirrel.Wiki.Contracts.Plugins;

/// <summary>
/// Metadata describing a plugin
/// </summary>
public class PluginMetadata
{
    /// <summary>
    /// Unique identifier for the plugin (e.g., "oidc-auth", "github-auth")
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Display name of the plugin (e.g., "OpenID Connect Authentication")
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Description of what the plugin does
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Plugin version (semantic versioning recommended)
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Plugin author/organization
    /// </summary>
    public required string Author { get; set; }

    /// <summary>
    /// IDs of other plugins this plugin depends on
    /// </summary>
    public string[] Dependencies { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether this plugin requires configuration before it can be used
    /// </summary>
    public bool RequiresConfiguration { get; set; }

    /// <summary>
    /// Whether this is a core plugin (cannot be uninstalled, only disabled)
    /// </summary>
    public bool IsCorePlugin { get; set; }

    /// <summary>
    /// Plugin type (for future extensibility)
    /// </summary>
    public PluginType Type { get; set; } = PluginType.Authentication;

    /// <summary>
    /// Configuration items required by this plugin
    /// </summary>
    public PluginConfigurationItem[] Configuration { get; set; } = Array.Empty<PluginConfigurationItem>();
}

/// <summary>
/// Types of plugins supported by the system
/// </summary>
public enum PluginType
{
    /// <summary>
    /// Authentication provider plugin
    /// </summary>
    Authentication = 0,

    /// <summary>
    /// Markdown extension plugin (future)
    /// </summary>
    MarkdownExtension = 1,

    /// <summary>
    /// Storage provider plugin (future)
    /// </summary>
    StorageProvider = 2,

    /// <summary>
    /// Search provider plugin (future)
    /// </summary>
    SearchProvider = 3,

    /// <summary>
    /// Notification provider plugin (future)
    /// </summary>
    NotificationProvider = 4,

    /// <summary>
    /// Theme plugin (future)
    /// </summary>
    Theme = 5,

    /// <summary>
    /// Widget plugin (future)
    /// </summary>
    Widget = 6
}
