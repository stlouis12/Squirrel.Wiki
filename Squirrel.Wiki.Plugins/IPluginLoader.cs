using Squirrel.Wiki.Contracts.Plugins;

namespace Squirrel.Wiki.Plugins;

/// <summary>
/// Interface for loading plugins from disk
/// </summary>
public interface IPluginLoader
{
    /// <summary>
    /// Load all plugins from the plugins directory
    /// </summary>
    /// <param name="pluginsPath">Path to the plugins directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of loaded plugins</returns>
    Task<IEnumerable<IPlugin>> LoadPluginsAsync(
        string pluginsPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Load a specific plugin by ID
    /// </summary>
    /// <param name="pluginsPath">Path to the plugins directory</param>
    /// <param name="pluginId">Plugin ID to load</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Loaded plugin or null if not found</returns>
    Task<IPlugin?> LoadPluginAsync(
        string pluginsPath,
        string pluginId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unload a plugin by ID
    /// </summary>
    /// <param name="pluginId">Plugin ID to unload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UnloadPluginAsync(
        string pluginId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reload a plugin by ID (for hot-reload support)
    /// </summary>
    /// <param name="pluginsPath">Path to the plugins directory</param>
    /// <param name="pluginId">Plugin ID to reload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Reloaded plugin or null if not found</returns>
    Task<IPlugin?> ReloadPluginAsync(
        string pluginsPath,
        string pluginId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all currently loaded plugins
    /// </summary>
    IEnumerable<IPlugin> GetLoadedPlugins();

    /// <summary>
    /// Get a loaded plugin by ID
    /// </summary>
    /// <param name="pluginId">Plugin ID</param>
    /// <returns>Plugin or null if not loaded</returns>
    IPlugin? GetLoadedPlugin(string pluginId);
}
