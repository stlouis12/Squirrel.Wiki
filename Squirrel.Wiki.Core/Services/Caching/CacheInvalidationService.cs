using Microsoft.Extensions.Logging;

namespace Squirrel.Wiki.Core.Services.Caching;

/// <summary>
/// Centralized service for managing cache invalidation across the application
/// Based on actual cache dependency patterns found in PageService.InvalidatePageCacheAsync
/// </summary>
public class CacheInvalidationService : ICacheInvalidationService
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<CacheInvalidationService> _logger;

    public CacheInvalidationService(
        ICacheService cacheService,
        ILogger<CacheInvalidationService> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task InvalidatePageAsync(int pageId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Invalidating page cache for page: {PageId}", pageId);

        // Invalidate specific page cache
        await _cacheService.RemoveAsync($"page:{pageId}", cancellationToken);

        // Invalidate all pages cache
        await _cacheService.RemoveAsync("pages:all", cancellationToken);

        // Invalidate all collection caches (category, tag, author filters)
        await _cacheService.RemoveByPatternAsync("pages:category:*", cancellationToken);
        await _cacheService.RemoveByPatternAsync("pages:tag:*", cancellationToken);
        await _cacheService.RemoveByPatternAsync("pages:author:*", cancellationToken);

        // Invalidate dependent caches
        await InvalidateTagsAsync(cancellationToken);
        await InvalidateMenusAsync(cancellationToken);
        await InvalidateCategoryTreeAsync(cancellationToken);

        _logger.LogDebug("Page cache invalidation complete for page: {PageId}", pageId);
    }

    public async Task InvalidateCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Invalidating category cache for category: {CategoryId}", categoryId);

        // Invalidate specific category cache
        await _cacheService.RemoveAsync($"category:{categoryId}", cancellationToken);

        // Invalidate category tree cache
        await InvalidateCategoryTreeAsync(cancellationToken);

        // Invalidate pages by category cache
        await _cacheService.RemoveByPatternAsync("pages:category:*", cancellationToken);

        // Invalidate menus (they may use %ALLCATEGORIES% token)
        await InvalidateMenusAsync(cancellationToken);

        _logger.LogDebug("Category cache invalidation complete for category: {CategoryId}", categoryId);
    }

    public async Task InvalidateTagsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Invalidating tag caches");

        // Invalidate all tag-related caches
        await _cacheService.RemoveByPatternAsync("tags:*", cancellationToken);

        // Invalidate pages by tag cache
        await _cacheService.RemoveByPatternAsync("pages:tag:*", cancellationToken);

        _logger.LogDebug("Tag cache invalidation complete");
    }

    public async Task InvalidateMenusAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Invalidating menu caches");

        // Invalidate all menu-related caches
        await _cacheService.RemoveByPatternAsync("menu:*", cancellationToken);

        _logger.LogDebug("Menu cache invalidation complete");
    }

    public async Task InvalidateSettingsAsync(string? settingKey = null, CancellationToken cancellationToken = default)
    {
        if (settingKey != null)
        {
            _logger.LogDebug("Invalidating settings cache for key: {SettingKey}", settingKey);
            await _cacheService.RemoveAsync($"settings:{settingKey}", cancellationToken);
        }
        else
        {
            _logger.LogDebug("Invalidating all settings caches");
            await _cacheService.RemoveByPatternAsync("settings:*", cancellationToken);
        }

        _logger.LogDebug("Settings cache invalidation complete");
    }

    public async Task InvalidateAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Invalidating ALL application caches");

        // Invalidate all cache patterns
        await _cacheService.RemoveByPatternAsync("page:*", cancellationToken);
        await _cacheService.RemoveByPatternAsync("pages:*", cancellationToken);
        await _cacheService.RemoveByPatternAsync("category:*", cancellationToken);
        await _cacheService.RemoveByPatternAsync("tags:*", cancellationToken);
        await _cacheService.RemoveByPatternAsync("menu:*", cancellationToken);
        await _cacheService.RemoveByPatternAsync("settings:*", cancellationToken);

        _logger.LogWarning("All application caches invalidated");
    }

    /// <summary>
    /// Private helper to invalidate category tree cache
    /// </summary>
    private async Task InvalidateCategoryTreeAsync(CancellationToken cancellationToken)
    {
        await _cacheService.RemoveAsync("category:tree", cancellationToken);
        _logger.LogDebug("Invalidated category tree cache");
    }
}
