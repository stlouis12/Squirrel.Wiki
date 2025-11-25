using Squirrel.Wiki.Contracts.Configuration;

namespace Squirrel.Wiki.Core.Configuration;

/// <summary>
/// Interface for configuration providers that supply configuration values from different sources
/// </summary>
public interface IConfigurationProvider
{
    /// <summary>
    /// Name of this provider (e.g., "Environment", "Database", "Default")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Priority of this provider (higher values take precedence)
    /// Environment = 100, Database = 50, Default = 0
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Gets a configuration value by key
    /// </summary>
    /// <param name="key">The configuration key</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The configuration value, or null if not found</returns>
    Task<ConfigurationValue?> GetValueAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if this provider can set values
    /// </summary>
    /// <param name="key">The configuration key</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if this provider can set the value</returns>
    Task<bool> CanSetValueAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a configuration value
    /// </summary>
    /// <param name="key">The configuration key</param>
    /// <param name="value">The value to set</param>
    /// <param name="ct">Cancellation token</param>
    /// <exception cref="NotSupportedException">Thrown if this provider is read-only</exception>
    Task SetValueAsync(string key, object value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all configuration keys available from this provider
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>All configuration keys</returns>
    Task<IEnumerable<string>> GetAllKeysAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a configuration value with metadata about its source
/// </summary>
public class ConfigurationValue
{
    /// <summary>
    /// The configuration key
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The configuration value
    /// </summary>
    public object Value { get; set; } = string.Empty;

    /// <summary>
    /// Where this value came from
    /// </summary>
    public ConfigurationSource Source { get; set; }

    /// <summary>
    /// When this value was last modified (null for environment variables and defaults)
    /// </summary>
    public DateTime? LastModified { get; set; }

    /// <summary>
    /// Who last modified this value (null for environment variables and defaults)
    /// </summary>
    public string? ModifiedBy { get; set; }
}
