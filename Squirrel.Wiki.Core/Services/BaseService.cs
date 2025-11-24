using Microsoft.Extensions.Logging;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Base service class providing common functionality for all services
/// Includes logging, caching, and cache invalidation capabilities
/// </summary>
public abstract class BaseService
{
    /// <summary>
    /// Logger instance for the derived service
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Cache service for reading and writing cached data
    /// </summary>
    protected readonly ICacheService Cache;

    /// <summary>
    /// Cache invalidation service for managing cache dependencies
    /// </summary>
    protected readonly ICacheInvalidationService CacheInvalidation;

    /// <summary>
    /// Initializes a new instance of the BaseService class
    /// </summary>
    /// <param name="logger">Logger instance for the derived service</param>
    /// <param name="cache">Cache service for data caching</param>
    /// <param name="cacheInvalidation">Cache invalidation service</param>
    protected BaseService(
        ILogger logger,
        ICacheService cache,
        ICacheInvalidationService cacheInvalidation)
    {
        Logger = logger;
        Cache = cache;
        CacheInvalidation = cacheInvalidation;
    }

    /// <summary>
    /// Logs an informational message
    /// </summary>
    protected void LogInfo(string message, params object[] args)
    {
        Logger.LogInformation(message, args);
    }

    /// <summary>
    /// Logs a debug message
    /// </summary>
    protected void LogDebug(string message, params object[] args)
    {
        Logger.LogDebug(message, args);
    }

    /// <summary>
    /// Logs a warning message
    /// </summary>
    protected void LogWarning(string message, params object[] args)
    {
        Logger.LogWarning(message, args);
    }

    /// <summary>
    /// Logs an error message
    /// </summary>
    protected void LogError(Exception ex, string message, params object[] args)
    {
        Logger.LogError(ex, message, args);
    }
}
