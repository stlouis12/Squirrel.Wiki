using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Events.Pages;
using Squirrel.Wiki.Core.Services.Caching;

namespace Squirrel.Wiki.Core.Events.Handlers;

/// <summary>
/// Handles cache invalidation for page-related events
/// </summary>
public class PageCacheInvalidationHandler : 
    IEventHandler<PageCreatedEvent>,
    IEventHandler<PageUpdatedEvent>,
    IEventHandler<PageDeletedEvent>
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<PageCacheInvalidationHandler> _logger;

    public PageCacheInvalidationHandler(
        ICacheService cacheService,
        ILogger<PageCacheInvalidationHandler> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task HandleAsync(PageCreatedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Invalidating cache for page created: {PageId} - {Title}", 
            domainEvent.PageId, domainEvent.Title);
        await InvalidatePageCacheAsync(domainEvent.PageId, cancellationToken);
    }

    public async Task HandleAsync(PageUpdatedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Invalidating cache for page updated: {PageId} - {Title}", 
            domainEvent.PageId, domainEvent.Title);
        await InvalidatePageCacheAsync(domainEvent.PageId, cancellationToken);
    }

    public async Task HandleAsync(PageDeletedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Invalidating cache for page deleted: {PageId} - {Title}", 
            domainEvent.PageId, domainEvent.Title);
        await InvalidatePageCacheAsync(domainEvent.PageId, cancellationToken);
    }

    private async Task InvalidatePageCacheAsync(int pageId, CancellationToken cancellationToken)
    {
        // Invalidate specific page cache
        await _cacheService.RemoveAsync(CacheKeys.Page(pageId), cancellationToken);

        // Invalidate all pages cache
        await _cacheService.RemoveAsync(CacheKeys.AllPages, cancellationToken);

        // Invalidate collection caches (category, tag, author filters)
        await _cacheService.RemoveByPatternAsync(CacheKeys.PagesByCategoryPattern, cancellationToken);
        await _cacheService.RemoveByPatternAsync(CacheKeys.PagesByTagPattern, cancellationToken);
        await _cacheService.RemoveByPatternAsync(CacheKeys.PagesByAuthorPattern, cancellationToken);

        // Invalidate dependent caches
        await _cacheService.RemoveByPatternAsync(CacheKeys.TagsPattern, cancellationToken);
        await _cacheService.RemoveByPatternAsync(CacheKeys.MenusPattern, cancellationToken);
        await _cacheService.RemoveAsync(CacheKeys.CategoryTree, cancellationToken);

        _logger.LogDebug("Page cache invalidation complete for page: {PageId}", pageId);
    }
}
