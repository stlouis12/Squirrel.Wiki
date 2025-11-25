using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;

namespace Squirrel.Wiki.Core.Configuration;

/// <summary>
/// Configuration provider that supplies hardcoded default values
/// This is the lowest priority provider (fallback when no other source has a value)
/// </summary>
public class DefaultConfigurationProvider : IConfigurationProvider
{
    private readonly ILogger<DefaultConfigurationProvider> _logger;

    public string Name => "Default";
    public int Priority => 0; // Lowest priority

    public DefaultConfigurationProvider(ILogger<DefaultConfigurationProvider> logger)
    {
        _logger = logger;
    }

    public Task<ConfigurationValue?> GetValueAsync(string key, CancellationToken ct = default)
    {
        try
        {
            if (!ConfigurationMetadataRegistry.HasMetadata(key))
            {
                return Task.FromResult<ConfigurationValue?>(null);
            }

            var metadata = ConfigurationMetadataRegistry.GetMetadata(key);
            
            if (metadata.DefaultValue == null)
            {
                return Task.FromResult<ConfigurationValue?>(null);
            }

            var configValue = new ConfigurationValue
            {
                Key = key,
                Value = metadata.DefaultValue,
                Source = ConfigurationSource.Default,
                LastModified = null,
                ModifiedBy = null
            };

            _logger.LogDebug("Loaded default value for {Key}: {Value}", key, metadata.DefaultValue);

            return Task.FromResult<ConfigurationValue?>(configValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading default value for {Key}", key);
            return Task.FromResult<ConfigurationValue?>(null);
        }
    }

    public Task<bool> CanSetValueAsync(string key, CancellationToken ct = default)
    {
        // Default provider is read-only
        return Task.FromResult(false);
    }

    public Task SetValueAsync(string key, object value, CancellationToken ct = default)
    {
        throw new NotSupportedException("Cannot modify default configuration values");
    }

    public Task<IEnumerable<string>> GetAllKeysAsync(CancellationToken ct = default)
    {
        var keys = ConfigurationMetadataRegistry.GetAllKeys();
        return Task.FromResult(keys);
    }
}
