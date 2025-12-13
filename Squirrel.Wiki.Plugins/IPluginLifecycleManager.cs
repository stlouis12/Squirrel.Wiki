using Squirrel.Wiki.Contracts.Plugins;

namespace Squirrel.Wiki.Plugins;

/// <summary>
/// Manages the lifecycle of plugins including initialization, shutdown, and health monitoring
/// Separates lifecycle concerns from persistence and configuration management
/// </summary>
public interface IPluginLifecycleManager
{
    /// <summary>
    /// Initializes a plugin with configuration and services
    /// </summary>
    /// <param name="plugin">The plugin to initialize</param>
    /// <param name="configuration">The plugin configuration</param>
    /// <param name="serviceProvider">The service provider for dependency injection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure with details</returns>
    Task<PluginLifecycleResult> InitializePluginAsync(
        IPlugin plugin,
        Dictionary<string, string> configuration,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Shuts down a plugin gracefully, allowing it to clean up resources
    /// </summary>
    /// <param name="plugin">The plugin to shut down</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure with details</returns>
    Task<PluginLifecycleResult> ShutdownPluginAsync(
        IPlugin plugin,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the health status of a plugin
    /// </summary>
    /// <param name="plugin">The plugin to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health status information</returns>
    Task<PluginHealthStatus> CheckPluginHealthAsync(
        IPlugin plugin,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restarts a plugin (shutdown followed by initialization)
    /// </summary>
    /// <param name="plugin">The plugin to restart</param>
    /// <param name="configuration">The plugin configuration</param>
    /// <param name="serviceProvider">The service provider for dependency injection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure with details</returns>
    Task<PluginLifecycleResult> RestartPluginAsync(
        IPlugin plugin,
        Dictionary<string, string> configuration,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a plugin lifecycle operation
/// </summary>
public class PluginLifecycleResult
{
    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The plugin ID
    /// </summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// The plugin name
    /// </summary>
    public string PluginName { get; set; } = string.Empty;

    /// <summary>
    /// The operation that was performed
    /// </summary>
    public PluginLifecycleOperation Operation { get; set; }

    /// <summary>
    /// Error message if operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Exception details if operation failed
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Duration of the operation
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Timestamp when the operation completed
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static PluginLifecycleResult CreateSuccess(
        string pluginId,
        string pluginName,
        PluginLifecycleOperation operation,
        TimeSpan duration)
    {
        return new PluginLifecycleResult
        {
            Success = true,
            PluginId = pluginId,
            PluginName = pluginName,
            Operation = operation,
            Duration = duration
        };
    }

    /// <summary>
    /// Creates a failure result
    /// </summary>
    public static PluginLifecycleResult CreateFailure(
        string pluginId,
        string pluginName,
        PluginLifecycleOperation operation,
        string errorMessage,
        Exception? exception = null,
        TimeSpan duration = default)
    {
        return new PluginLifecycleResult
        {
            Success = false,
            PluginId = pluginId,
            PluginName = pluginName,
            Operation = operation,
            ErrorMessage = errorMessage,
            Exception = exception,
            Duration = duration
        };
    }
}

/// <summary>
/// Types of plugin lifecycle operations
/// </summary>
public enum PluginLifecycleOperation
{
    /// <summary>
    /// Plugin initialization
    /// </summary>
    Initialize,

    /// <summary>
    /// Plugin shutdown
    /// </summary>
    Shutdown,

    /// <summary>
    /// Plugin restart
    /// </summary>
    Restart,

    /// <summary>
    /// Plugin health check
    /// </summary>
    HealthCheck
}

/// <summary>
/// Health status of a plugin
/// </summary>
public class PluginHealthStatus
{
    /// <summary>
    /// The plugin ID
    /// </summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// The plugin name
    /// </summary>
    public string PluginName { get; set; } = string.Empty;

    /// <summary>
    /// Overall health status
    /// </summary>
    public PluginHealth Health { get; set; }

    /// <summary>
    /// Detailed status message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// When the plugin was last initialized
    /// </summary>
    public DateTime? LastInitialized { get; set; }

    /// <summary>
    /// How long the plugin has been running
    /// </summary>
    public TimeSpan? Uptime { get; set; }

    /// <summary>
    /// Additional health check details
    /// </summary>
    public Dictionary<string, object> Details { get; set; } = new();

    /// <summary>
    /// Timestamp of the health check
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Plugin health states
/// </summary>
public enum PluginHealth
{
    /// <summary>
    /// Plugin is healthy and functioning normally
    /// </summary>
    Healthy,

    /// <summary>
    /// Plugin is functioning but with warnings
    /// </summary>
    Degraded,

    /// <summary>
    /// Plugin is not functioning properly
    /// </summary>
    Unhealthy,

    /// <summary>
    /// Plugin health status is unknown
    /// </summary>
    Unknown
}
