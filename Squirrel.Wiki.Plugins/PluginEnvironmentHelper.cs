using Squirrel.Wiki.Contracts.Configuration;

namespace Squirrel.Wiki.Plugins;

/// <summary>
/// Helper class for managing plugin environment variables
/// Provides centralized logic for environment variable naming and access
/// </summary>
public static class PluginEnvironmentHelper
{
    /// <summary>
    /// Gets the environment variable prefix for a plugin
    /// Converts plugin ID to uppercase and replaces special characters with underscores
    /// </summary>
    /// <param name="pluginId">The plugin ID (e.g., "Squirrel.Wiki.Plugins.Lucene")</param>
    /// <returns>The environment variable prefix (e.g., "PLUGIN_SQUIRREL_WIKI_PLUGINS_LUCENE_")</returns>
    public static string GetEnvironmentPrefix(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            throw new ArgumentException("Plugin ID cannot be null or empty", nameof(pluginId));
        }

        return $"PLUGIN_{pluginId.ToUpperInvariant().Replace("-", "_").Replace(".", "_")}_";
    }

    /// <summary>
    /// Gets the full environment variable name for a plugin setting
    /// </summary>
    /// <param name="pluginId">The plugin ID</param>
    /// <param name="settingKey">The setting key (e.g., "IndexPath")</param>
    /// <returns>The full environment variable name (e.g., "PLUGIN_SQUIRREL_WIKI_PLUGINS_LUCENE_INDEXPATH")</returns>
    public static string GetEnvironmentVariableName(string pluginId, string settingKey)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            throw new ArgumentException("Plugin ID cannot be null or empty", nameof(pluginId));
        }

        if (string.IsNullOrWhiteSpace(settingKey))
        {
            throw new ArgumentException("Setting key cannot be null or empty", nameof(settingKey));
        }

        return $"{GetEnvironmentPrefix(pluginId)}{settingKey.ToUpperInvariant()}";
    }

    /// <summary>
    /// Checks if a plugin's enabled state is controlled by an environment variable
    /// </summary>
    /// <param name="pluginId">The plugin ID</param>
    /// <param name="configurationService">The configuration service to check environment variables</param>
    /// <returns>True if the ENABLED environment variable is set, false otherwise</returns>
    public static bool IsEnabledLockedByEnvironment(string pluginId, IConfigurationService configurationService)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            throw new ArgumentException("Plugin ID cannot be null or empty", nameof(pluginId));
        }

        if (configurationService == null)
        {
            throw new ArgumentNullException(nameof(configurationService));
        }

        var envVar = $"{GetEnvironmentPrefix(pluginId)}ENABLED";
        
        try
        {
            // Use IConfigurationService to check environment variable
            // The EnvironmentVariableConfigurationProvider now supports dynamic keys
            var value = configurationService.GetValueAsync<string>(envVar).GetAwaiter().GetResult();
            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            // If we can't read the configuration, assume it's not locked
            return false;
        }
    }

    /// <summary>
    /// Gets all environment variables for a plugin based on its configuration schema
    /// </summary>
    /// <param name="pluginId">The plugin ID</param>
    /// <param name="settingKeys">The setting keys to check for environment variables</param>
    /// <param name="configurationService">The configuration service to check environment variables</param>
    /// <returns>Dictionary of setting keys to environment variable values</returns>
    public static Dictionary<string, string> GetPluginEnvironmentVariables(
        string pluginId,
        IEnumerable<string> settingKeys,
        IConfigurationService configurationService)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            throw new ArgumentException("Plugin ID cannot be null or empty", nameof(pluginId));
        }

        if (settingKeys == null)
        {
            throw new ArgumentNullException(nameof(settingKeys));
        }

        if (configurationService == null)
        {
            throw new ArgumentNullException(nameof(configurationService));
        }

        var result = new Dictionary<string, string>();
        var prefix = GetEnvironmentPrefix(pluginId);

        foreach (var key in settingKeys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var envVar = $"{prefix}{key.ToUpperInvariant()}";
            
            try
            {
                var value = configurationService.GetValueAsync<string>(envVar).GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(value))
                {
                    result[key] = value;
                }
            }
            catch
            {
                // Skip this key if we can't read it
                continue;
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the enabled state from environment variable if set
    /// </summary>
    /// <param name="pluginId">The plugin ID</param>
    /// <param name="configurationService">The configuration service to check environment variables</param>
    /// <returns>The enabled state if set, null otherwise</returns>
    public static bool? GetEnabledStateFromEnvironment(string pluginId, IConfigurationService configurationService)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            throw new ArgumentException("Plugin ID cannot be null or empty", nameof(pluginId));
        }

        if (configurationService == null)
        {
            throw new ArgumentNullException(nameof(configurationService));
        }

        var envVar = $"{GetEnvironmentPrefix(pluginId)}ENABLED";
        
        try
        {
            var value = configurationService.GetValueAsync<string>(envVar).GetAwaiter().GetResult();
            
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            // Parse boolean value
            var lowerValue = value.ToLowerInvariant();
            if (lowerValue == "true" || lowerValue == "1" || lowerValue == "yes")
            {
                return true;
            }
            else if (lowerValue == "false" || lowerValue == "0" || lowerValue == "no")
            {
                return false;
            }

            // If we can't parse it, treat it as "set" (locked) but return null for the value
            return null;
        }
        catch
        {
            return null;
        }
    }
}
