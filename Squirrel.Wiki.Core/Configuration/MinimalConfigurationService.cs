using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;

namespace Squirrel.Wiki.Core.Configuration;

/// <summary>
/// Minimal configuration service that ONLY reads from environment variables.
/// Used during application startup before the database is available.
/// 
/// This service leverages EnvironmentVariableConfigurationProvider and ConfigurationMetadataRegistry
/// to provide consistent configuration handling during startup.
/// 
/// This service is intentionally simple and has no dependencies on:
/// - Database (doesn't exist yet at startup)
/// - IMemoryCache (would create circular dependencies)
/// - Other configuration providers
/// 
/// Use this for:
/// - Database connection configuration
/// - Cache provider configuration
/// - Other startup-critical settings
/// </summary>
public class MinimalConfigurationService
{
    private readonly EnvironmentVariableConfigurationProvider _provider;
    private readonly ILogger<MinimalConfigurationService> _logger;

    public MinimalConfigurationService(ILogger<MinimalConfigurationService> logger)
    {
        _logger = logger;
        // Create a wrapper logger for the provider
        var providerLogger = new LoggerWrapper<EnvironmentVariableConfigurationProvider>(logger);
        _provider = new EnvironmentVariableConfigurationProvider(providerLogger);
    }

    /// <summary>
    /// Gets a configuration value from environment variables only.
    /// Returns the default value if the environment variable is not set.
    /// Uses ConfigurationMetadataRegistry to get the default if available.
    /// </summary>
    /// <param name="key">The configuration key (e.g., "SQUIRREL_CACHE_PROVIDER")</param>
    /// <param name="fallbackDefault">The fallback default value if not in metadata registry</param>
    /// <returns>The configuration value or default</returns>
    public string GetValue(string key, string fallbackDefault)
    {
        var configValue = _provider.GetValueAsync(key).Result;
        
        if (configValue != null)
        {
            _logger.LogDebug("Configuration '{Key}' loaded from environment variable: {Value}", key, configValue.Value);
            return configValue.Value?.ToString() ?? fallbackDefault;
        }

        // Try to get default from metadata registry
        try
        {
            var metadata = ConfigurationMetadataRegistry.GetMetadata(key);
            var defaultValue = metadata.DefaultValue ?? fallbackDefault;
            _logger.LogDebug("Configuration '{Key}' not found in environment variables, using default from metadata: {Default}", 
                key, defaultValue);
            return defaultValue.ToString() ?? fallbackDefault;
        }
        catch (KeyNotFoundException)
        {
            // Key not in metadata registry, use fallback
            _logger.LogDebug("Configuration '{Key}' not found in environment variables or metadata, using fallback default: {Default}", 
                key, fallbackDefault);
            return fallbackDefault;
        }
    }

    /// <summary>
    /// Gets a configuration value from environment variables only.
    /// Returns the default from ConfigurationMetadataRegistry if the environment variable is not set.
    /// Returns null if the key is not in the metadata registry.
    /// </summary>
    /// <param name="key">The configuration key (e.g., "SQUIRREL_DATABASE_PROVIDER")</param>
    /// <returns>The configuration value or default from metadata registry</returns>
    public string? GetValue(string key)
    {
        var configValue = _provider.GetValueAsync(key).Result;
        
        if (configValue != null)
        {
            _logger.LogDebug("Configuration '{Key}' loaded from environment variable: {Value}", key, configValue.Value);
            return configValue.Value?.ToString();
        }

        // Try to get default from metadata registry
        try
        {
            var metadata = ConfigurationMetadataRegistry.GetMetadata(key);
            var defaultValue = metadata.DefaultValue;
            _logger.LogDebug("Configuration '{Key}' not found in environment variables, using default from metadata: {Default}", 
                key, defaultValue);
            return defaultValue?.ToString();
        }
        catch (KeyNotFoundException)
        {
            // Key not in metadata registry
            _logger.LogDebug("Configuration '{Key}' not found in environment variables or metadata registry", key);
            return null;
        }
    }

    /// <summary>
    /// Checks if a configuration value exists in environment variables.
    /// </summary>
    /// <param name="key">The configuration key</param>
    /// <returns>True if the environment variable is set, false otherwise</returns>
    public bool HasValue(string key)
    {
        var configValue = _provider.GetValueAsync(key).Result;
        return configValue != null;
    }

    /// <summary>
    /// Gets the source of a configuration value (always EnvironmentVariable or Default).
    /// </summary>
    /// <param name="key">The configuration key</param>
    /// <returns>The configuration source</returns>
    public string GetSource(string key)
    {
        return HasValue(key) ? "EnvironmentVariable" : "Default";
    }

    /// <summary>
    /// Gets the metadata for a configuration key from the registry.
    /// </summary>
    /// <param name="key">The configuration key</param>
    /// <returns>The configuration metadata</returns>
    public ConfigurationProperty? GetMetadata(string key)
    {
        try
        {
            return ConfigurationMetadataRegistry.GetMetadata(key);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// Logger wrapper that adapts ILogger<T> to ILogger<U>
    /// </summary>
    private class LoggerWrapper<T> : ILogger<T>
    {
        private readonly ILogger _innerLogger;

        public LoggerWrapper(ILogger innerLogger)
        {
            _innerLogger = innerLogger;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => _innerLogger.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel)
            => _innerLogger.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => _innerLogger.Log(logLevel, eventId, state, exception, formatter);
    }
}
