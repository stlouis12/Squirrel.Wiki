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
        var schema = GetConfigurationSchema();

        foreach (var item in schema)
        {
            // Check required fields
            if (item.IsRequired && (!config.ContainsKey(item.Key) || string.IsNullOrWhiteSpace(config[item.Key])))
            {
                return false;
            }

            // Validate against pattern if provided
            if (config.ContainsKey(item.Key) && !string.IsNullOrWhiteSpace(item.ValidationPattern))
            {
                var value = config[item.Key];
                if (!string.IsNullOrWhiteSpace(value) && !Regex.IsMatch(value, item.ValidationPattern))
                {
                    return false;
                }
            }

            // Type-specific validation
            if (config.ContainsKey(item.Key))
            {
                var value = config[item.Key];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    switch (item.Type)
                    {
                        case PluginConfigType.Url:
                            if (!Uri.TryCreate(value, UriKind.Absolute, out _))
                            {
                                return false;
                            }
                            break;

                        case PluginConfigType.Number:
                            if (!int.TryParse(value, out _))
                            {
                                return false;
                            }
                            break;

                        case PluginConfigType.Boolean:
                            if (!bool.TryParse(value, out _))
                            {
                                return false;
                            }
                            break;
                    }
                }
            }
        }

        return await Task.FromResult(true);
    }

    /// <inheritdoc/>
    public virtual Task InitializeAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        // Get configuration service from DI
        Configuration = services.GetService<IConfigurationService>();
        
        if (Configuration == null)
        {
            throw new InvalidOperationException(
                "IConfigurationService is not registered. " +
                "Plugins require access to configuration.");
        }

        return Task.CompletedTask;
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
    protected string GetConfigValue(Dictionary<string, string> config, string key, string defaultValue = "")
    {
        return config.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Helper method to get a boolean configuration value
    /// </summary>
    protected bool GetConfigBool(Dictionary<string, string> config, string key, bool defaultValue = false)
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
    protected int GetConfigInt(Dictionary<string, string> config, string key, int defaultValue = 0)
    {
        if (config.TryGetValue(key, out var value) && int.TryParse(value, out var result))
        {
            return result;
        }
        return defaultValue;
    }
}
