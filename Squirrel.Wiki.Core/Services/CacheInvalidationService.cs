using Microsoft.Extensions.Logging;

namespace Squirrel.Wiki.Core.Services;

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

    public async Task InvalidatePageAsync(int pageId, CancellationToken ct = default)
    {
        _logger.LogDebug("Invalidating page cache for page: {PageId}", pageId);

        // Invalidate specific page cache
        await _cacheService.RemoveAsync($"page:{pageId}", ct);

        // Invalidate all pages cache
        await _cacheService.RemoveAsync("pages:all", ct);

        // Invalidate all collection caches (category, tag, author filters)
        await _cacheService.RemoveByPatternAsync("pages:category:*", ct);
        await _cacheService.RemoveByPatternAsync("pages:tag:*", ct);
        await _cacheService.RemoveByPatternAsync("pages:author:*", ct);

        // Invalidate dependent caches
        await InvalidateTagsAsync(ct);
        await InvalidateMenusAsync(ct);
        await InvalidateCategoryTreeAsync(ct);

        _logger.LogDebug("Page cache invalidation complete for page: {PageId}", pageId);
    }

    public async Task InvalidateCategoryAsync(int categoryId, CancellationToken ct = default)
    {
        _logger.LogDebug("Invalidating category cache for category: {CategoryId}", categoryId);

        // Invalidate specific category cache
        await _cacheService.RemoveAsync($"category:{categoryId}", ct);

        // Invalidate category tree cache
        await InvalidateCategoryTreeAsync(ct);

        // Invalidate pages by category cache
        await _cacheService.RemoveByPatternAsync("pages:category:*", ct);

        // Invalidate menus (they may use %ALLCATEGORIES% token)
        await InvalidateMenusAsync(ct);

        _logger.LogDebug("Category cache invalidation complete for category: {CategoryId}", categoryId);
    }

    public async Task InvalidateTagsAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Invalidating tag caches");

        // Invalidate all tag-related caches
        await _cacheService.RemoveByPatternAsync("tags:*", ct);

        // Invalidate pages by tag cache
        await _cacheService.RemoveByPatternAsync("pages:tag:*", ct);

        _logger.LogDebug("Tag cache invalidation complete");
    }

    public async Task InvalidateMenusAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Invalidating menu caches");

        // Invalidate all menu-related caches
        await _cacheService.RemoveByPatternAsync("menu:*", ct);

        _logger.LogDebug("Menu cache invalidation complete");
    }

    public async Task InvalidateSettingsAsync(string? settingKey = null, CancellationToken ct = default)
    {
        if (settingKey != null)
        {
            _logger.LogDebug("Invalidating settings cache for key: {SettingKey}", settingKey);
            await _cacheService.RemoveAsync($"settings:{settingKey}", ct);
        }
        else
        {
            _logger.LogDebug("Invalidating all settings caches");
            await _cacheService.RemoveByPatternAsync("settings:*", ct);
        }

        _logger.LogDebug("Settings cache invalidation complete");
    }

    public async Task InvalidateAllAsync(CancellationToken ct = default)
    {
        _logger.LogWarning("Invalidating ALL application caches");

        // Invalidate all cache patterns
        await _cacheService.RemoveByPatternAsync("page:*", ct);
        await _cacheService.RemoveByPatternAsync("pages:*", ct);
        await _cacheService.RemoveByPatternAsync("category:*", ct);
        await _cacheService.RemoveByPatternAsync("tags:*", ct);
        await _cacheService.RemoveByPatternAsync("menu:*", ct);
        await _cacheService.RemoveByPatternAsync("settings:*", ct);

        _logger.LogWarning("All application caches invalidated");
    }

    /// <summary>
    /// Private helper to invalidate category tree cache
    /// </summary>
    private async Task InvalidateCategoryTreeAsync(CancellationToken ct)
    {
        await _cacheService.RemoveAsync("category:tree", ct);
        _logger.LogDebug("Invalidated category tree cache");
    }
}
