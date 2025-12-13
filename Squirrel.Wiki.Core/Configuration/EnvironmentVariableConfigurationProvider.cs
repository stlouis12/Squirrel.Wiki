using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;

namespace Squirrel.Wiki.Core.Configuration;

/// <summary>
/// Configuration provider that reads values from environment variables
/// This is the highest priority provider (overrides all other sources)
/// </summary>
public class EnvironmentVariableConfigurationProvider : IConfigurationProvider
{
    private readonly ILogger<EnvironmentVariableConfigurationProvider> _logger;

    public string Name => "Environment";
    public int Priority => 100; // Highest priority

    public EnvironmentVariableConfigurationProvider(ILogger<EnvironmentVariableConfigurationProvider> logger)
    {
        _logger = logger;
    }

    public Task<ConfigurationValue?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if this key has metadata (is a known configuration key)
            bool hasMetadata = ConfigurationMetadataRegistry.HasMetadata(key);
            ConfigurationProperty? metadata = hasMetadata ? ConfigurationMetadataRegistry.GetMetadata(key) : null;
            
            // Get the environment variable name
            // For registered keys, use the metadata's environment variable name
            // For unregistered keys (like plugin config), use the key directly
            var envVarName = metadata?.EnvironmentVariable ?? key;
            
            // Try to read from environment
            var envValue = Environment.GetEnvironmentVariable(envVarName);
            
            if (string.IsNullOrEmpty(envValue))
            {
                return Task.FromResult<ConfigurationValue?>(null);
            }

            // Convert the string value to the appropriate type
            object typedValue;
            try
            {
                // If we have metadata, use its type information
                // Otherwise, return as string (for dynamic/plugin keys)
                var targetType = metadata?.ValueType ?? typeof(string);
                typedValue = ConvertValue(envValue, targetType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert environment variable {EnvVar} value '{Value}' to type {Type}", 
                    envVarName, envValue, metadata?.ValueType.Name ?? "string");
                return Task.FromResult<ConfigurationValue?>(null);
            }

            var configValue = new ConfigurationValue
            {
                Key = key,
                Value = typedValue,
                Source = ConfigurationSource.EnvironmentVariable,
                LastModified = null,
                ModifiedBy = null
            };

            // Log the value (mask if secret or if it looks like a plugin key)
            var isPluginKey = key.StartsWith("PLUGIN_", StringComparison.OrdinalIgnoreCase);
            var displayValue = (metadata?.IsSecret == true || isPluginKey) ? "***" : typedValue.ToString();
            _logger.LogDebug("Loaded configuration from environment variable {EnvVar}: {Value}", 
                envVarName, displayValue);

            return Task.FromResult<ConfigurationValue?>(configValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading environment variable for {Key}", key);
            return Task.FromResult<ConfigurationValue?>(null);
        }
    }

    public Task<bool> CanSetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        // Environment variables are read-only from the application's perspective
        return Task.FromResult(false);
    }

    public Task SetValueAsync(string key, object value, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Cannot modify environment variables from the application. " +
            "Environment variables must be set at the system level and require an application restart.");
    }

    public Task<IEnumerable<string>> GetAllKeysAsync(CancellationToken cancellationToken = default)
    {
        // Return all keys that have environment variables set
        var keys = new List<string>();
        
        foreach (var key in ConfigurationMetadataRegistry.GetAllKeys())
        {
            var metadata = ConfigurationMetadataRegistry.GetMetadata(key);
            var envVarName = metadata.EnvironmentVariable ?? key;
            var envValue = Environment.GetEnvironmentVariable(envVarName);
            
            if (!string.IsNullOrEmpty(envValue))
            {
                keys.Add(key);
            }
        }
        
        return Task.FromResult<IEnumerable<string>>(keys);
    }

    /// <summary>
    /// Converts a string value to the specified type
    /// </summary>
    private static object ConvertValue(string value, Type targetType)
    {
        if (targetType == typeof(string))
        {
            return value;
        }
        
        if (targetType == typeof(int))
        {
            return int.Parse(value);
        }
        
        if (targetType == typeof(bool))
        {
            // Support various boolean representations
            return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }
        
        if (targetType == typeof(long))
        {
            return long.Parse(value);
        }
        
        if (targetType == typeof(double))
        {
            return double.Parse(value);
        }
        
        if (targetType == typeof(decimal))
        {
            return decimal.Parse(value);
        }
        
        // For other types, try Convert.ChangeType
        return Convert.ChangeType(value, targetType);
    }
}
