namespace Squirrel.Wiki.Contracts.Plugins;

/// <summary>
/// Base interface for all plugin types
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Plugin metadata
    /// </summary>
    PluginMetadata Metadata { get; }

    /// <summary>
    /// Get the configuration schema for this plugin
    /// </summary>
    IEnumerable<PluginConfigurationItem> GetConfigurationSchema();

    /// <summary>
    /// Validate plugin configuration
    /// </summary>
    /// <param name="config">Configuration dictionary</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if configuration is valid</returns>
    Task<bool> ValidateConfigurationAsync(
        Dictionary<string, string> config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Initialize the plugin with the given service provider
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InitializeAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Shutdown the plugin gracefully
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ShutdownAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get custom actions exposed by this plugin
    /// </summary>
    /// <returns>Collection of plugin actions</returns>
    IEnumerable<IPluginAction> GetActions();
}
