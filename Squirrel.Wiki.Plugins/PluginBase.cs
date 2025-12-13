using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Contracts.Plugins;

namespace Squirrel.Wiki.Plugins;

/// <summary>
/// Base class for plugins providing common functionality
/// </summary>
public abstract class PluginBase : IPlugin
{
    /// <summary>
    /// Configuration service for accessing application and plugin settings
    /// </summary>
    protected IConfigurationService? Configuration { get; private set; }

    /// <summary>
    /// Plugin-specific configuration values loaded from database or environment variables
    /// </summary>
    protected Dictionary<string, string>? PluginConfiguration { get; private set; }

    /// <inheritdoc/>
    public abstract PluginMetadata Metadata { get; }

    /// <inheritdoc/>
    public abstract IEnumerable<PluginConfigurationItem> GetConfigurationSchema();

    /// <inheritdoc/>
    public virtual IEnumerable<IPluginAction> GetActions()
    {
        // Default implementation returns no actions
        return Enumerable.Empty<IPluginAction>();
    }

    /// <inheritdoc/>
    public virtual async Task<bool> ValidateConfigurationAsync(
        Dictionary<string, string> config,
        CancellationToken cancellationToken = default)
    {
        // Use the centralized validator for schema-based validation
        var validator = new PluginConfigurationValidator();
        var schema = GetConfigurationSchema().ToList();
        var result = PluginConfigurationValidator.Validate(config, schema);
        
        if (!result.IsValid)
        {
            return false;
        }
        
        // Allow plugins to add custom validation
        return await ValidateConfigurationCustomAsync(config, cancellationToken);
    }

    /// <summary>
    /// Override this method to add custom validation logic beyond schema validation
    /// </summary>
    /// <param name="config">The configuration to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if configuration is valid, false otherwise</returns>
    protected virtual Task<bool> ValidateConfigurationCustomAsync(
        Dictionary<string, string> config,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public virtual Task InitializeAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        // Get configuration service from DI
        Configuration = serviceProvider.GetService<IConfigurationService>();
        
        if (Configuration == null)
        {
            throw new InvalidOperationException(
                "IConfigurationService is not registered. " +
                "Plugins require access to configuration.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Set the plugin configuration. Called by the plugin service before InitializeAsync.
    /// </summary>
    public virtual void SetPluginConfiguration(Dictionary<string, string> configuration)
    {
        PluginConfiguration = configuration;
    }

    /// <inheritdoc/>
    public virtual Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        // Default implementation does nothing
        return Task.CompletedTask;
    }

    /// <summary>
    /// Helper method to get a configuration value with a default
    /// </summary>
    protected static string GetConfigValue(Dictionary<string, string> config, string key, string defaultValue = "")
    {
        return config.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Helper method to get a boolean configuration value
    /// </summary>
    protected static bool GetConfigBool(Dictionary<string, string> config, string key, bool defaultValue = false)
    {
        if (config.TryGetValue(key, out var value) && bool.TryParse(value, out var result))
        {
            return result;
        }
        return defaultValue;
    }

    /// <summary>
    /// Helper method to get an integer configuration value
    /// </summary>
    protected static int GetConfigInt(Dictionary<string, string> config, string key, int defaultValue = 0)
    {
        if (config.TryGetValue(key, out var value) && int.TryParse(value, out var result))
        {
            return result;
        }
        return defaultValue;
    }
}
