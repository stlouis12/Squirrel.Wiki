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

    public Task<ConfigurationValue?> GetValueAsync(string key, CancellationToken ct = default)
    {
        try
        {
            // Check if this key has metadata (is a known configuration key)
            if (!ConfigurationMetadataRegistry.HasMetadata(key))
            {
                return Task.FromResult<ConfigurationValue?>(null);
            }

            var metadata = ConfigurationMetadataRegistry.GetMetadata(key);
            
            // Get the environment variable name (should match the key)
            var envVarName = metadata.EnvironmentVariable ?? key;
            
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
                typedValue = ConvertValue(envValue, metadata.ValueType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert environment variable {EnvVar} value '{Value}' to type {Type}", 
                    envVarName, envValue, metadata.ValueType.Name);
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

            // Log the value (mask if secret)
            var displayValue = metadata.IsSecret ? "***" : typedValue.ToString();
            _logger.LogInformation("Loaded configuration from environment variable {EnvVar}: {Value}", 
                envVarName, displayValue);

            return Task.FromResult<ConfigurationValue?>(configValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading environment variable for {Key}", key);
            return Task.FromResult<ConfigurationValue?>(null);
        }
    }

    public Task<bool> CanSetValueAsync(string key, CancellationToken ct = default)
    {
        // Environment variables are read-only from the application's perspective
        return Task.FromResult(false);
    }

    public Task SetValueAsync(string key, object value, CancellationToken ct = default)
    {
        throw new NotSupportedException(
            "Cannot modify environment variables from the application. " +
            "Environment variables must be set at the system level and require an application restart.");
    }

    public Task<IEnumerable<string>> GetAllKeysAsync(CancellationToken ct = default)
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
    private object ConvertValue(string value, Type targetType)
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
