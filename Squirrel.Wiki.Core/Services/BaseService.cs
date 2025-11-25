using AutoMapper;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Services.Caching;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Base service class providing common functionality for all services
/// Includes logging, caching, cache invalidation, configuration access, and object mapping capabilities
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
    /// AutoMapper instance for entity-to-DTO mappings
    /// </summary>
    protected readonly IMapper Mapper;

    /// <summary>
    /// Configuration service for accessing application settings
    /// Optional to avoid circular dependencies with CacheService
    /// </summary>
    protected IConfigurationService? Configuration { get; private set; }

    /// <summary>
    /// Initializes a new instance of the BaseService class
    /// </summary>
    /// <param name="logger">Logger instance for the derived service</param>
    /// <param name="cache">Cache service for data caching</param>
    /// <param name="cacheInvalidation">Cache invalidation service</param>
    /// <param name="mapper">AutoMapper instance for object mapping</param>
    /// <param name="configuration">Configuration service (optional to avoid circular dependencies)</param>
    protected BaseService(
        ILogger logger,
        ICacheService cache,
        ICacheInvalidationService cacheInvalidation,
        IMapper mapper,
        IConfigurationService? configuration = null)
    {
        Logger = logger;
        Cache = cache;
        CacheInvalidation = cacheInvalidation;
        Mapper = mapper;
        Configuration = configuration;
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
