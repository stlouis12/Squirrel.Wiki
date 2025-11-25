namespace Squirrel.Wiki.Contracts.Configuration;

/// <summary>
/// Service for accessing application configuration from multiple sources
/// Priority: Environment Variables > Database > Defaults
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets a strongly-typed configuration section
    /// </summary>
    /// <typeparam name="T">The configuration class type</typeparam>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The populated configuration object</returns>
    Task<T> GetAsync<T>(CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// Gets a configuration value by key
    /// </summary>
    /// <typeparam name="TValue">The value type</typeparam>
    /// <param name="key">The configuration key (e.g., "SQUIRREL_SITE_NAME")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The configuration value</returns>
    Task<TValue> GetValueAsync<TValue>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a configuration value (only for non-environment-variable settings)
    /// </summary>
    /// <typeparam name="TValue">The value type</typeparam>
    /// <param name="key">The configuration key</param>
    /// <param name="value">The value to set</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="InvalidOperationException">Thrown when trying to modify an environment variable setting</exception>
    Task SetValueAsync<TValue>(string key, TValue value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the source of a configuration value
    /// </summary>
    /// <param name="key">The configuration key</param>
    /// <returns>The configuration source</returns>
    ConfigurationSource GetSource(string key);

    /// <summary>
    /// Gets metadata about a configuration property
    /// </summary>
    /// <param name="key">The configuration key</param>
    /// <returns>The configuration property metadata</returns>
    ConfigurationProperty GetMetadata(string key);

    /// <summary>
    /// Gets all configuration metadata
    /// </summary>
    /// <returns>All configuration property metadata</returns>
    IEnumerable<ConfigurationProperty> GetAllMetadata();

    /// <summary>
    /// Validates a configuration value
    /// </summary>
    /// <param name="key">The configuration key</param>
    /// <param name="value">The value to validate</param>
    /// <returns>Validation result</returns>
    ValidationResult Validate(string key, object value);

    /// <summary>
    /// Invalidates the cache for a specific key or all keys
    /// </summary>
    /// <param name="key">The key to invalidate, or null to invalidate all</param>
    void InvalidateCache(string? key = null);
}
