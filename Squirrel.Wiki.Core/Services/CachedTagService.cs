using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Cached decorator for ITagService that adds caching layer to tag operations
/// </summary>
public class CachedTagService : ITagService
{
    private readonly ITagService _inner;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CachedTagService> _logger;

    // Cache key constants
    private const string AllTagsKey = "tags:all";
    private const string AllWithCountsKey = "tags:all-with-counts";
    private const string PopularKeyPrefix = "tags:popular:";
    private const string CloudKeyPrefix = "tags:cloud:";
    private const string PageKeyPrefix = "tags:page:";
    private const string ByNameKeyPrefix = "tags:by-name:";

    // Cache expiration times
    private static readonly TimeSpan LongExpiration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ShortExpiration = TimeSpan.FromMinutes(15);

    public CachedTagService(
        ITagService inner,
        ICacheService cacheService,
        ILogger<CachedTagService> logger)
    {
        _inner = inner;
        _cacheService = cacheService;
        _logger = logger;
    }

    #region Cached Methods

    public async Task<IEnumerable<TagDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _cacheService.GetAsync<List<TagDto>>(AllTagsKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogDebug("Tag cache hit for key: {CacheKey}", AllTagsKey);
            return cached;
        }

        _logger.LogDebug("Tag cache miss for key: {CacheKey}", AllTagsKey);
        var result = await _inner.GetAllAsync(cancellationToken);
        var resultList = result.ToList();
        
        await _cacheService.SetAsync(AllTagsKey, resultList, LongExpiration, cancellationToken);
        
        return resultList;
    }

    public async Task<IEnumerable<TagDto>> GetAllTagsAsync(CancellationToken cancellationToken = default)
    {
        return await GetAllAsync(cancellationToken);
    }

    public async Task<IEnumerable<TagWithCountDto>> GetAllWithCountsAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _cacheService.GetAsync<List<TagWithCountDto>>(AllWithCountsKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogDebug("Tag cache hit for key: {CacheKey}", AllWithCountsKey);
            return cached;
        }

        _logger.LogDebug("Tag cache miss for key: {CacheKey}", AllWithCountsKey);
        var result = await _inner.GetAllWithCountsAsync(cancellationToken);
        var resultList = result.ToList();
        
        await _cacheService.SetAsync(AllWithCountsKey, resultList, ShortExpiration, cancellationToken);
        
        return resultList;
    }

    public async Task<IEnumerable<TagWithCountDto>> GetPopularTagsAsync(int count = 20, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{PopularKeyPrefix}{count}";
        var cached = await _cacheService.GetAsync<List<TagWithCountDto>>(cacheKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogDebug("Tag cache hit for key: {CacheKey}", cacheKey);
            return cached;
        }

        _logger.LogDebug("Tag cache miss for key: {CacheKey}", cacheKey);
        var result = await _inner.GetPopularTagsAsync(count, cancellationToken);
        var resultList = result.ToList();
        
        await _cacheService.SetAsync(cacheKey, resultList, ShortExpiration, cancellationToken);
        
        return resultList;
    }

    public async Task<IEnumerable<TagCloudItemDto>> GetTagCloudAsync(int minCount = 1, int maxTags = 50, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CloudKeyPrefix}{minCount}:{maxTags}";
        var cached = await _cacheService.GetAsync<List<TagCloudItemDto>>(cacheKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogDebug("Tag cache hit for key: {CacheKey}", cacheKey);
            return cached;
        }

        _logger.LogDebug("Tag cache miss for key: {CacheKey}", cacheKey);
        var result = await _inner.GetTagCloudAsync(minCount, maxTags, cancellationToken);
        var resultList = result.ToList();
        
        await _cacheService.SetAsync(cacheKey, resultList, ShortExpiration, cancellationToken);
        
        return resultList;
    }

    public async Task<IEnumerable<TagDto>> GetTagsForPageAsync(int pageId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{PageKeyPrefix}{pageId}";
        var cached = await _cacheService.GetAsync<List<TagDto>>(cacheKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogDebug("Tag cache hit for key: {CacheKey}", cacheKey);
            return cached;
        }

        _logger.LogDebug("Tag cache miss for key: {CacheKey}", cacheKey);
        var result = await _inner.GetTagsForPageAsync(pageId, cancellationToken);
        var resultList = result.ToList();
        
        await _cacheService.SetAsync(cacheKey, resultList, LongExpiration, cancellationToken);
        
        return resultList;
    }

    public async Task<TagDto?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{ByNameKeyPrefix}{name.ToLowerInvariant()}";
        var cached = await _cacheService.GetAsync<TagDto>(cacheKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogDebug("Tag cache hit for key: {CacheKey}", cacheKey);
            return cached;
        }

        _logger.LogDebug("Tag cache miss for key: {CacheKey}", cacheKey);
        var result = await _inner.GetByNameAsync(name, cancellationToken);
        
        if (result != null)
        {
            await _cacheService.SetAsync(cacheKey, result, LongExpiration, cancellationToken);
        }
        
        return result;
    }

    #endregion

    #region Pass-Through Methods (Not Cached)

    public Task<TagDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _inner.GetByIdAsync(id, cancellationToken);
    }

    public Task<IEnumerable<PageDto>> GetPagesWithTagAsync(string tagName, CancellationToken cancellationToken = default)
    {
        return _inner.GetPagesWithTagAsync(tagName, cancellationToken);
    }

    public Task<IEnumerable<TagDto>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        return _inner.SearchAsync(searchTerm, cancellationToken);
    }

    public Task<IEnumerable<TagDto>> GetRelatedTagsAsync(int tagId, int count = 10, CancellationToken cancellationToken = default)
    {
        return _inner.GetRelatedTagsAsync(tagId, count, cancellationToken);
    }

    public Task<TagStatsDto> GetTagStatsAsync(CancellationToken cancellationToken = default)
    {
        return _inner.GetTagStatsAsync(cancellationToken);
    }

    #endregion

    #region Mutating Methods (With Cache Invalidation)

    public async Task<TagDto> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var result = await _inner.CreateAsync(name, cancellationToken);
        await InvalidateAllTagCachesAsync(cancellationToken);
        return result;
    }

    public async Task<TagDto> GetOrCreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var result = await _inner.GetOrCreateAsync(name, cancellationToken);
        // Invalidate in case a new tag was created
        await InvalidateAllTagCachesAsync(cancellationToken);
        return result;
    }

    public async Task<TagDto> UpdateAsync(int id, string newName, CancellationToken cancellationToken = default)
    {
        // Get old name before update for cache invalidation
        var oldTag = await _inner.GetByIdAsync(id, cancellationToken);
        var oldName = oldTag?.Name;

        var result = await _inner.UpdateAsync(id, newName, cancellationToken);
        
        await InvalidateAllTagCachesAsync(cancellationToken);
        
        // Invalidate specific by-name caches
        if (oldName != null)
        {
            await InvalidateTagByNameAsync(oldName, cancellationToken);
        }
        await InvalidateTagByNameAsync(newName, cancellationToken);
        
        return result;
    }

    public async Task<TagDto> RenameAsync(int id, string newName, CancellationToken cancellationToken = default)
    {
        return await UpdateAsync(id, newName, cancellationToken);
    }

    public async Task MergeTagsAsync(int sourceTagId, int targetTagId, CancellationToken cancellationToken = default)
    {
        await _inner.MergeTagsAsync(sourceTagId, targetTagId, cancellationToken);
        await InvalidateAllTagCachesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        // Get tag name before deletion for cache invalidation
        var tag = await _inner.GetByIdAsync(id, cancellationToken);
        var tagName = tag?.Name;

        await _inner.DeleteAsync(id, cancellationToken);
        
        await InvalidateAllTagCachesAsync(cancellationToken);
        
        if (tagName != null)
        {
            await InvalidateTagByNameAsync(tagName, cancellationToken);
        }
    }

    public async Task<int> CleanupUnusedTagsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _inner.CleanupUnusedTagsAsync(cancellationToken);
        
        if (result > 0)
        {
            await InvalidateAllTagCachesAsync(cancellationToken);
        }
        
        return result;
    }

    #endregion

    #region Cache Invalidation Methods

    /// <summary>
    /// Invalidates all tag-related caches
    /// </summary>
    private async Task InvalidateAllTagCachesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Invalidating all tag caches");
        await _cacheService.RemoveByPatternAsync("tags:*", cancellationToken);
    }

    /// <summary>
    /// Invalidates tag count-related caches (when page-tag associations change)
    /// </summary>
    public async Task InvalidateTagCountCachesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Invalidating tag count caches");
        await _cacheService.RemoveAsync(AllWithCountsKey, cancellationToken);
        await _cacheService.RemoveByPatternAsync($"{PopularKeyPrefix}*", cancellationToken);
        await _cacheService.RemoveByPatternAsync($"{CloudKeyPrefix}*", cancellationToken);
    }

    /// <summary>
    /// Invalidates tag cache for a specific page
    /// </summary>
    public async Task InvalidateTagCachesForPageAsync(int pageId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{PageKeyPrefix}{pageId}";
        _logger.LogDebug("Invalidating tag cache for page: {PageId}", pageId);
        await _cacheService.RemoveAsync(cacheKey, cancellationToken);
    }

    /// <summary>
    /// Invalidates cache for a specific tag name
    /// </summary>
    private async Task InvalidateTagByNameAsync(string name, CancellationToken cancellationToken)
    {
        var cacheKey = $"{ByNameKeyPrefix}{name.ToLowerInvariant()}";
        _logger.LogDebug("Invalidating tag cache for name: {TagName}", name);
        await _cacheService.RemoveAsync(cacheKey, cancellationToken);
    }

    #endregion
}
