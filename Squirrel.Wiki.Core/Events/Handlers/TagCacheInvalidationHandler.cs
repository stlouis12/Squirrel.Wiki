using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Events.Tags;
using Squirrel.Wiki.Core.Services.Caching;

namespace Squirrel.Wiki.Core.Events.Handlers;

/// <summary>
/// Handles cache invalidation for tag-related events
/// </summary>
public class TagCacheInvalidationHandler : IEventHandler<TagChangedEvent>
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<TagCacheInvalidationHandler> _logger;

    public TagCacheInvalidationHandler(
        ICacheService cacheService,
        ILogger<TagCacheInvalidationHandler> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task HandleAsync(TagChangedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Invalidating cache for tag changed: {TagName}", domainEvent.TagName);

        // Invalidate all tag-related caches
        await _cacheService.RemoveByPatternAsync(CacheKeys.TagsPattern, cancellationToken);

        // Invalidate pages by tag cache
        await _cacheService.RemoveByPatternAsync(CacheKeys.PagesByTagPattern, cancellationToken);

        _logger.LogDebug("Tag cache invalidation complete for tag: {TagName}", domainEvent.TagName);
    }
}
