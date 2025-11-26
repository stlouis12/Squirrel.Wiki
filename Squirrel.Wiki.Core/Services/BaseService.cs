using AutoMapper;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Events;
using Squirrel.Wiki.Core.Services.Caching;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Full-featured base service class for business services
/// Extends MinimalBaseService and adds caching, event publishing, configuration access, and object mapping capabilities
/// Use this for domain/business services that require these features
/// </summary>
public abstract class BaseService : MinimalBaseService
{
    /// <summary>
    /// Cache service for reading and writing cached data
    /// </summary>
    protected readonly ICacheService Cache;

    /// <summary>
    /// Event publisher for publishing domain events
    /// </summary>
    protected readonly IEventPublisher EventPublisher;

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
    /// <param name="eventPublisher">Event publisher for domain events</param>
    /// <param name="mapper">AutoMapper instance for object mapping</param>
    /// <param name="configuration">Configuration service (optional to avoid circular dependencies)</param>
    protected BaseService(
        ILogger logger,
        ICacheService cache,
        IEventPublisher eventPublisher,
        IMapper mapper,
        IConfigurationService? configuration = null)
        : base(logger)
    {
        Cache = cache;
        EventPublisher = eventPublisher;
        Mapper = mapper;
        Configuration = configuration;
    }
}
