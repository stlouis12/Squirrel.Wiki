using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Cached decorator for ICategoryTreeBuilder that adds distributed caching
/// </summary>
public class CachedCategoryTreeBuilder : ICategoryTreeBuilder
{
    private readonly ICategoryTreeBuilder _innerBuilder;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CachedCategoryTreeBuilder> _logger;

    public CachedCategoryTreeBuilder(
        ICategoryTreeBuilder innerBuilder,
        ICacheService cacheService,
        ILogger<CachedCategoryTreeBuilder> logger)
    {
        _innerBuilder = innerBuilder;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<List<CategoryTreeNode>> BuildTreeAsync(
        IEnumerable<CategoryDto> categories,
        CategoryTreeOptions? options = null)
    {
        // Generate cache key based on categories and options
        var cacheKey = CacheKeys.Build(CacheKeys.Categories, "tree", GenerateCacheKeySuffix(categories, options));
        
        // Try to get from cache
        var cached = await _cacheService.GetAsync<List<CategoryTreeNode>>(cacheKey);
        if (cached != null)
        {
            return cached;
        }

        // Build tree using inner service
        var result = await _innerBuilder.BuildTreeAsync(categories, options);

        // Cache the result
        await _cacheService.SetAsync(cacheKey, result);

        return result;
    }

    public async Task<List<CategoryTreeNode>> BuildTreeWithAuthorizationAsync(
        IEnumerable<CategoryDto> categories,
        Func<PageDto, Task<bool>> authorizationFilter,
        CategoryTreeOptions? options = null)
    {
        // Don't cache authorized trees as they're user-specific
        return await _innerBuilder.BuildTreeWithAuthorizationAsync(
            categories, 
            authorizationFilter, 
            options);
    }

    public async Task<List<TNode>> ConvertTreeAsync<TNode>(
        IEnumerable<CategoryTreeNode> tree,
        Func<CategoryTreeNode, Task<TNode>> converter) where TNode : class
    {
        // Don't cache conversions as they may have side effects
        // and the converter function can't be serialized
        return await _innerBuilder.ConvertTreeAsync(tree, converter);
    }

    public List<CategorySelectItem> FlattenTreeForSelection(
        IEnumerable<CategoryTreeNode> tree,
        HashSet<int>? excludedIds = null)
    {
        // Generate cache key for flattened tree
        var cacheKey = CacheKeys.Build(CacheKeys.Categories, "flatten", GenerateFlattenCacheKeySuffix(tree, excludedIds));
        
        // Try to get from cache (synchronous wrapper for async cache)
        var cached = _cacheService.GetAsync<List<CategorySelectItem>>(cacheKey).GetAwaiter().GetResult();
        if (cached != null)
        {
            return cached;
        }

        // Flatten using inner service
        var result = _innerBuilder.FlattenTreeForSelection(tree, excludedIds);

        // Cache the result (fire and forget)
        _ = _cacheService.SetAsync(cacheKey, result);

        return result;
    }


    private string GenerateCacheKeySuffix(
        IEnumerable<CategoryDto> categories,
        CategoryTreeOptions? options)
    {
        // Create a deterministic cache key suffix based on:
        // 1. Category IDs (sorted for consistency)
        // 2. Options (if provided)
        
        var categoryIds = string.Join(",", categories.Select(c => c.Id).OrderBy(id => id));
        var optionsHash = options != null ? GetOptionsHash(options) : "default";
        
        return $"{categoryIds}:{optionsHash}";
    }

    private string GenerateFlattenCacheKeySuffix(
        IEnumerable<CategoryTreeNode> tree,
        HashSet<int>? excludedIds)
    {
        // Create cache key suffix for flattened tree
        var treeIds = string.Join(",", GetAllNodeIds(tree).OrderBy(id => id));
        var excludeKey = excludedIds != null && excludedIds.Any() 
            ? string.Join(",", excludedIds.OrderBy(id => id))
            : "none";
        
        return $"{treeIds}:exclude:{excludeKey}";
    }

    private IEnumerable<int> GetAllNodeIds(IEnumerable<CategoryTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node.Id;
            
            if (node.Children.Any())
            {
                foreach (var childId in GetAllNodeIds(node.Children))
                {
                    yield return childId;
                }
            }
        }
    }

    private string GetOptionsHash(CategoryTreeOptions options)
    {
        // Create a simple hash of options for cache key
        return $"sort:{options.SortByName}_depth:{options.MaxDepth?.ToString() ?? "none"}";
    }
}
