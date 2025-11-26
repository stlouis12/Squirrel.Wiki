using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Events.Categories;
using Squirrel.Wiki.Core.Services.Caching;

namespace Squirrel.Wiki.Core.Events.Handlers;

/// <summary>
/// Handles cache invalidation for category-related events
/// </summary>
public class CategoryCacheInvalidationHandler : IEventHandler<CategoryChangedEvent>
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<CategoryCacheInvalidationHandler> _logger;

    public CategoryCacheInvalidationHandler(
        ICacheService cacheService,
        ILogger<CategoryCacheInvalidationHandler> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task HandleAsync(CategoryChangedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Invalidating cache for category changed: {CategoryId} - {CategoryName}", 
            domainEvent.CategoryId, domainEvent.CategoryName);

        // Invalidate specific category cache
        await _cacheService.RemoveAsync(CacheKeys.Category(domainEvent.CategoryId), cancellationToken);

        // Invalidate category tree cache
        await _cacheService.RemoveAsync(CacheKeys.CategoryTree, cancellationToken);

        // Invalidate all categories cache
        await _cacheService.RemoveAsync(CacheKeys.AllCategories, cancellationToken);

        // Invalidate pages by category cache
        await _cacheService.RemoveByPatternAsync(CacheKeys.PagesByCategoryPattern, cancellationToken);

        // Invalidate menus (they may use %ALLCATEGORIES% token)
        await _cacheService.RemoveByPatternAsync(CacheKeys.MenusPattern, cancellationToken);

        _logger.LogDebug("Category cache invalidation complete for category: {CategoryId}", domainEvent.CategoryId);
    }
}
