using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Plugins;

using Squirrel.Wiki.Contracts.Plugins;

namespace Squirrel.Wiki.Core.Services.Plugins;

/// <summary>
/// Service for managing authentication plugins
/// </summary>
public interface IPluginService
{
    /// <summary>
    /// Initialize the plugin service and load all plugins
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all registered plugins
    /// </summary>
    Task<IEnumerable<Plugin>> GetAllPluginsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a plugin by its database ID
    /// </summary>
    Task<Plugin?> GetPluginAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a plugin by its plugin ID string
    /// </summary>
    Task<Plugin?> GetPluginByPluginIdAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all enabled plugins
    /// </summary>
    Task<IEnumerable<Plugin>> GetEnabledPluginsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new plugin in the database
    /// </summary>
    Task<Plugin> RegisterPluginAsync(
        string pluginId,
        string name,
        string version,
        string pluginType,
        bool isCorePlugin = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enable a plugin
    /// </summary>
    Task EnablePluginAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disable a plugin
    /// </summary>
    Task DisablePluginAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update plugin configuration
    /// </summary>
    Task UpdatePluginConfigurationAsync(
        Guid id,
        Dictionary<string, string> configuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get plugin configuration
    /// </summary>
    Task<Dictionary<string, string>> GetPluginConfigurationAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the loaded plugin instance
    /// </summary>
    IAuthenticationPlugin? GetLoadedPlugin(string pluginId);

    /// <summary>
    /// Get all loaded plugin instances
    /// </summary>
    IEnumerable<IAuthenticationPlugin> GetLoadedPlugins();

    /// <summary>
    /// Reload a plugin (hot-reload)
    /// </summary>
    Task ReloadPluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate plugin configuration
    /// </summary>
    Task<bool> ValidatePluginConfigurationAsync(
        string pluginId,
        Dictionary<string, string> configuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a plugin (only if not a core plugin)
    /// </summary>
    Task DeletePluginAsync(Guid id, CancellationToken cancellationToken = default);
}
