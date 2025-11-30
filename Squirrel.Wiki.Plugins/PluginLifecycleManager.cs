using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Plugins;

namespace Squirrel.Wiki.Plugins;

/// <summary>
/// Default implementation of IPluginLifecycleManager
/// Manages plugin lifecycle operations with proper error handling and logging
/// </summary>
public class PluginLifecycleManager : IPluginLifecycleManager
{
    private readonly ILogger<PluginLifecycleManager> _logger;
    private readonly Dictionary<string, PluginLifecycleState> _pluginStates = new();
    private readonly object _stateLock = new();

    public PluginLifecycleManager(ILogger<PluginLifecycleManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<PluginLifecycleResult> InitializePluginAsync(
        IPlugin plugin,
        Dictionary<string, string> configuration,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        if (plugin == null)
        {
            throw new ArgumentNullException(nameof(plugin));
        }

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        var stopwatch = Stopwatch.StartNew();
        var pluginId = plugin.Metadata.Id;
        var pluginName = plugin.Metadata.Name;

        try
        {
            _logger.LogInformation(
                "Initializing plugin {PluginId} ({PluginName})",
                pluginId,
                pluginName);

            // Set plugin configuration if it's a PluginBase
            if (plugin is PluginBase pluginBase)
            {
                pluginBase.SetPluginConfiguration(configuration);
            }

            // Initialize the plugin
            await plugin.InitializeAsync(services, cancellationToken);

            stopwatch.Stop();

            // Track plugin state
            lock (_stateLock)
            {
                _pluginStates[pluginId] = new PluginLifecycleState
                {
                    PluginId = pluginId,
                    PluginName = pluginName,
                    IsInitialized = true,
                    LastInitialized = DateTime.UtcNow,
                    InitializationDuration = stopwatch.Elapsed
                };
            }

            _logger.LogInformation(
                "Plugin {PluginId} initialized successfully in {Duration}ms",
                pluginId,
                stopwatch.ElapsedMilliseconds);

            return PluginLifecycleResult.CreateSuccess(
                pluginId,
                pluginName,
                PluginLifecycleOperation.Initialize,
                stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Failed to initialize plugin {PluginId} ({PluginName})",
                pluginId,
                pluginName);

            // Track failed state
            lock (_stateLock)
            {
                _pluginStates[pluginId] = new PluginLifecycleState
                {
                    PluginId = pluginId,
                    PluginName = pluginName,
                    IsInitialized = false,
                    LastError = ex.Message,
                    LastErrorTime = DateTime.UtcNow
                };
            }

            return PluginLifecycleResult.CreateFailure(
                pluginId,
                pluginName,
                PluginLifecycleOperation.Initialize,
                $"Failed to initialize plugin: {ex.Message}",
                ex,
                stopwatch.Elapsed);
        }
    }

    /// <inheritdoc/>
    public async Task<PluginLifecycleResult> ShutdownPluginAsync(
        IPlugin plugin,
        CancellationToken cancellationToken = default)
    {
        if (plugin == null)
        {
            throw new ArgumentNullException(nameof(plugin));
        }

        var stopwatch = Stopwatch.StartNew();
        var pluginId = plugin.Metadata.Id;
        var pluginName = plugin.Metadata.Name;

        try
        {
            _logger.LogInformation(
                "Shutting down plugin {PluginId} ({PluginName})",
                pluginId,
                pluginName);

            // Shutdown the plugin
            await plugin.ShutdownAsync(cancellationToken);

            stopwatch.Stop();

            // Update plugin state
            lock (_stateLock)
            {
                if (_pluginStates.TryGetValue(pluginId, out var state))
                {
                    state.IsInitialized = false;
                    state.LastShutdown = DateTime.UtcNow;
                }
            }

            _logger.LogInformation(
                "Plugin {PluginId} shut down successfully in {Duration}ms",
                pluginId,
                stopwatch.ElapsedMilliseconds);

            return PluginLifecycleResult.CreateSuccess(
                pluginId,
                pluginName,
                PluginLifecycleOperation.Shutdown,
                stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Failed to shut down plugin {PluginId} ({PluginName})",
                pluginId,
                pluginName);

            // Track error but mark as shut down anyway
            lock (_stateLock)
            {
                if (_pluginStates.TryGetValue(pluginId, out var state))
                {
                    state.IsInitialized = false;
                    state.LastError = ex.Message;
                    state.LastErrorTime = DateTime.UtcNow;
                }
            }

            return PluginLifecycleResult.CreateFailure(
                pluginId,
                pluginName,
                PluginLifecycleOperation.Shutdown,
                $"Failed to shut down plugin cleanly: {ex.Message}",
                ex,
                stopwatch.Elapsed);
        }
    }

    /// <inheritdoc/>
    public async Task<PluginHealthStatus> CheckPluginHealthAsync(
        IPlugin plugin,
        CancellationToken cancellationToken = default)
    {
        if (plugin == null)
        {
            throw new ArgumentNullException(nameof(plugin));
        }

        var pluginId = plugin.Metadata.Id;
        var pluginName = plugin.Metadata.Name;

        PluginLifecycleState? state;
        lock (_stateLock)
        {
            _pluginStates.TryGetValue(pluginId, out state);
        }

        var healthStatus = new PluginHealthStatus
        {
            PluginId = pluginId,
            PluginName = pluginName,
            LastInitialized = state?.LastInitialized,
            Uptime = state?.LastInitialized.HasValue == true
                ? DateTime.UtcNow - state.LastInitialized.Value
                : null
        };

        // Check if plugin is initialized
        if (state == null || !state.IsInitialized)
        {
            healthStatus.Health = PluginHealth.Unhealthy;
            healthStatus.Message = "Plugin is not initialized";
            return await Task.FromResult(healthStatus);
        }

        // Check for recent errors
        if (state.LastErrorTime.HasValue &&
            (DateTime.UtcNow - state.LastErrorTime.Value).TotalMinutes < 5)
        {
            healthStatus.Health = PluginHealth.Degraded;
            healthStatus.Message = $"Recent error: {state.LastError}";
            healthStatus.Details["LastError"] = state.LastError ?? "Unknown";
            healthStatus.Details["LastErrorTime"] = state.LastErrorTime.Value;
            return await Task.FromResult(healthStatus);
        }

        // Plugin appears healthy
        healthStatus.Health = PluginHealth.Healthy;
        healthStatus.Message = "Plugin is functioning normally";
        healthStatus.Details["InitializationDuration"] = state.InitializationDuration;

        return await Task.FromResult(healthStatus);
    }

    /// <inheritdoc/>
    public async Task<PluginLifecycleResult> RestartPluginAsync(
        IPlugin plugin,
        Dictionary<string, string> configuration,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        if (plugin == null)
        {
            throw new ArgumentNullException(nameof(plugin));
        }

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        var stopwatch = Stopwatch.StartNew();
        var pluginId = plugin.Metadata.Id;
        var pluginName = plugin.Metadata.Name;

        try
        {
            _logger.LogInformation(
                "Restarting plugin {PluginId} ({PluginName})",
                pluginId,
                pluginName);

            // Shutdown first
            var shutdownResult = await ShutdownPluginAsync(plugin, cancellationToken);
            if (!shutdownResult.Success)
            {
                _logger.LogWarning(
                    "Plugin {PluginId} shutdown had issues during restart, continuing anyway",
                    pluginId);
            }

            // Small delay to allow cleanup
            await Task.Delay(100, cancellationToken);

            // Initialize again
            var initResult = await InitializePluginAsync(plugin, configuration, services, cancellationToken);

            stopwatch.Stop();

            if (initResult.Success)
            {
                _logger.LogInformation(
                    "Plugin {PluginId} restarted successfully in {Duration}ms",
                    pluginId,
                    stopwatch.ElapsedMilliseconds);

                return PluginLifecycleResult.CreateSuccess(
                    pluginId,
                    pluginName,
                    PluginLifecycleOperation.Restart,
                    stopwatch.Elapsed);
            }
            else
            {
                return PluginLifecycleResult.CreateFailure(
                    pluginId,
                    pluginName,
                    PluginLifecycleOperation.Restart,
                    $"Failed to restart plugin: {initResult.ErrorMessage}",
                    initResult.Exception,
                    stopwatch.Elapsed);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Failed to restart plugin {PluginId} ({PluginName})",
                pluginId,
                pluginName);

            return PluginLifecycleResult.CreateFailure(
                pluginId,
                pluginName,
                PluginLifecycleOperation.Restart,
                $"Failed to restart plugin: {ex.Message}",
                ex,
                stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Gets the current state of a plugin
    /// </summary>
    public PluginLifecycleState? GetPluginState(string pluginId)
    {
        lock (_stateLock)
        {
            return _pluginStates.TryGetValue(pluginId, out var state) ? state : null;
        }
    }

    /// <summary>
    /// Gets all plugin states
    /// </summary>
    public IReadOnlyDictionary<string, PluginLifecycleState> GetAllPluginStates()
    {
        lock (_stateLock)
        {
            return new Dictionary<string, PluginLifecycleState>(_pluginStates);
        }
    }
}

/// <summary>
/// Tracks the lifecycle state of a plugin
/// </summary>
public class PluginLifecycleState
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
    /// Whether the plugin is currently initialized
    /// </summary>
    public bool IsInitialized { get; set; }

    /// <summary>
    /// When the plugin was last initialized
    /// </summary>
    public DateTime? LastInitialized { get; set; }

    /// <summary>
    /// When the plugin was last shut down
    /// </summary>
    public DateTime? LastShutdown { get; set; }

    /// <summary>
    /// How long initialization took
    /// </summary>
    public TimeSpan InitializationDuration { get; set; }

    /// <summary>
    /// Last error message
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// When the last error occurred
    /// </summary>
    public DateTime? LastErrorTime { get; set; }
}
