using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Events;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Services.Caching;
using Squirrel.Wiki.Core.Services.Pages;

namespace Squirrel.Wiki.Core.Services.Categories;

/// <summary>
/// Service for building and manipulating hierarchical category trees with caching
/// </summary>
public class CategoryTreeBuilder : BaseService, ICategoryTreeBuilder
{
    private readonly IPageService _pageService;

    public CategoryTreeBuilder(
        IPageService pageService,
        ILogger<CategoryTreeBuilder> logger,
        ICacheService cache,
        IEventPublisher eventPublisher,
        IConfigurationService configuration)
        : base(logger, cache, eventPublisher, null, configuration)
    {
        _pageService = pageService;
    }

    /// <inheritdoc/>
    public async Task<List<CategoryTreeNode>> BuildTreeAsync(
        IEnumerable<CategoryDto> categories, 
        CategoryTreeOptions? options = null)
    {
        options ??= new CategoryTreeOptions();
        
        // Generate cache key based on categories and options
        var cacheKey = CacheKeys.Build(CacheKeys.Categories, "tree", GenerateCacheKeySuffix(categories, options));
        
        // Try to get from cache
        var cached = await Cache.GetAsync<List<CategoryTreeNode>>(cacheKey);
        if (cached != null)
        {
            LogDebug("Cache hit for category tree with key {CacheKey}", cacheKey);
            return cached;
        }

        LogDebug("Cache miss for category tree with key {CacheKey}, building tree", cacheKey);
        
        var categoryList = categories.ToList();
        var tree = new List<CategoryTreeNode>();
        var lookup = new Dictionary<int, CategoryTreeNode>();

        // Create nodes
        foreach (var category in categoryList)
        {
            var node = MapToTreeNode(category);
            lookup[category.Id] = node;
        }

        // Build tree structure
        foreach (var category in categoryList)
        {
            var node = lookup[category.Id];
            
            if (category.ParentId.HasValue && lookup.ContainsKey(category.ParentId.Value))
            {
                lookup[category.ParentId.Value].Children.Add(node);
            }
            else
            {
                tree.Add(node);
            }
        }

        // Calculate counts if requested
        if (options.IncludeSubcategoryCounts)
        {
            foreach (var node in lookup.Values)
            {
                node.SubcategoryCount = node.Children.Count;
            }
        }

        // Sort if requested
        if (options.SortByName)
        {
            SortTree(tree, options.SortRecursively);
        }

        // Apply max depth if specified
        if (options.MaxDepth.HasValue)
        {
            tree = LimitDepth(tree, options.MaxDepth.Value);
        }

        // Cache the result
        await Cache.SetAsync(cacheKey, tree);

        return tree;
    }

    /// <inheritdoc/>
    public async Task<List<CategoryTreeNode>> BuildTreeWithAuthorizationAsync(
        IEnumerable<CategoryDto> categories,
        Func<PageDto, Task<bool>> authorizationFilter,
        CategoryTreeOptions? options = null)
    {
        var tree = await BuildTreeAsync(categories, options);
        
        // Apply authorization filtering and update counts
        await ApplyAuthorizationFilterAsync(tree, authorizationFilter);
        
        return tree;
    }

    /// <inheritdoc/>
    public async Task<List<TNode>> ConvertTreeAsync<TNode>(
        IEnumerable<CategoryTreeNode> tree,
        Func<CategoryTreeNode, Task<TNode>> converter) 
        where TNode : class
    {
        var result = new List<TNode>();
        
        foreach (var node in tree)
        {
            var converted = await converter(node);
            result.Add(converted);
        }
        
        return result;
    }

    /// <inheritdoc/>
    public List<CategorySelectItem> FlattenTreeForSelection(
        IEnumerable<CategoryTreeNode> tree,
        HashSet<int>? excludedIds = null)
    {
        // Generate cache key for flattened tree
        var cacheKey = CacheKeys.Build(CacheKeys.Categories, "flatten", GenerateFlattenCacheKeySuffix(tree, excludedIds));
        
        // Try to get from cache (synchronous wrapper for async cache)
        var cached = Cache.GetAsync<List<CategorySelectItem>>(cacheKey).GetAwaiter().GetResult();
        if (cached != null)
        {
            LogDebug("Cache hit for flattened category tree with key {CacheKey}", cacheKey);
            return cached;
        }

        LogDebug("Cache miss for flattened category tree with key {CacheKey}, flattening tree", cacheKey);
        
        var items = new List<CategorySelectItem>();
        excludedIds ??= new HashSet<int>();

        FlattenTreeRecursive(tree, items, excludedIds, "");
        
        // Cache the result (fire and forget)
        _ = Cache.SetAsync(cacheKey, items);
        
        return items;
    }

    #region Private Helper Methods

    /// <summary>
    /// Maps a CategoryDto to a CategoryTreeNode
    /// </summary>
    private static CategoryTreeNode MapToTreeNode(CategoryDto category)
    {
        return new CategoryTreeNode
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            FullPath = category.FullPath,
            ParentId = category.ParentId,
            Level = category.Level,
            PageCount = category.PageCount,
            CreatedOn = category.CreatedOn,
            ModifiedOn = category.ModifiedOn,
            ModifiedBy = category.ModifiedBy,
            Children = new List<CategoryTreeNode>()
        };
    }

    /// <summary>
    /// Sorts tree nodes by name
    /// </summary>
    private static void SortTree(List<CategoryTreeNode> nodes, bool recursive)
    {
        nodes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        
        if (recursive)
        {
            foreach (var node in nodes)
            {
                if (node.Children.Any())
                {
                    SortTree(node.Children, recursive);
                }
            }
        }
    }

    /// <summary>
    /// Limits tree depth to specified maximum
    /// </summary>
    private static List<CategoryTreeNode> LimitDepth(List<CategoryTreeNode> tree, int maxDepth)
    {
        if (maxDepth <= 0) return new List<CategoryTreeNode>();
        
        var result = new List<CategoryTreeNode>();
        foreach (var node in tree)
        {
            var limitedNode = LimitNodeDepth(node, maxDepth, 1);
            result.Add(limitedNode);
        }
        
        return result;
    }

    /// <summary>
    /// Recursively limits node depth
    /// </summary>
    private static CategoryTreeNode LimitNodeDepth(CategoryTreeNode node, int maxDepth, int currentDepth)
    {
        var limitedNode = new CategoryTreeNode
        {
            Id = node.Id,
            Name = node.Name,
            Slug = node.Slug,
            Description = node.Description,
            FullPath = node.FullPath,
            ParentId = node.ParentId,
            Level = node.Level,
            PageCount = node.PageCount,
            SubcategoryCount = node.SubcategoryCount,
            CreatedOn = node.CreatedOn,
            ModifiedOn = node.ModifiedOn,
            ModifiedBy = node.ModifiedBy,
            Children = new List<CategoryTreeNode>()
        };

        if (currentDepth < maxDepth)
        {
            foreach (var child in node.Children)
            {
                limitedNode.Children.Add(LimitNodeDepth(child, maxDepth, currentDepth + 1));
            }
        }

        return limitedNode;
    }

    /// <summary>
    /// Applies authorization filtering to tree and updates page counts
    /// </summary>
    private async Task ApplyAuthorizationFilterAsync(
        List<CategoryTreeNode> tree,
        Func<PageDto, Task<bool>> authorizationFilter)
    {
        foreach (var node in tree)
        {
            try
            {
                // Get pages and filter by authorization
                var pages = await _pageService.GetByCategoryAsync(node.Id);
                var authorizedCount = 0;
                
                foreach (var page in pages)
                {
                    if (await authorizationFilter(page))
                    {
                        authorizedCount++;
                    }
                }
                
                node.PageCount = authorizedCount;
                
                // Recursively apply to children
                if (node.Children.Any())
                {
                    await ApplyAuthorizationFilterAsync(node.Children, authorizationFilter);
                }
            }
            catch (Exception ex)
            {
                LogError(ex, "Error applying authorization filter to category {CategoryId}", node.Id);
                node.PageCount = 0;
            }
        }
    }

    /// <summary>
    /// Recursively flattens tree into a list with indentation
    /// Note: Visual hierarchy is handled by CategorySelectItem.DisplayName property
    /// </summary>
    private static void FlattenTreeRecursive(
        IEnumerable<CategoryTreeNode> nodes,
        List<CategorySelectItem> items,
        HashSet<int> excludedIds,
        string prefix)
    {
        foreach (var node in nodes)
        {
            items.Add(new CategorySelectItem
            {
                Id = node.Id,
                Name = node.Name,  // Store plain name; DisplayName property adds visual hierarchy
                FullPath = node.FullPath,
                Level = node.Level,
                IsDisabled = excludedIds.Contains(node.Id)
            });

            if (node.Children.Any())
            {
                FlattenTreeRecursive(node.Children, items, excludedIds, prefix);  // prefix no longer used
            }
        }
    }

    /// <summary>
    /// Generates cache key suffix based on categories and options
    /// </summary>
    private static string GenerateCacheKeySuffix(
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

    /// <summary>
    /// Generates cache key suffix for flattened tree
    /// </summary>
    private static string GenerateFlattenCacheKeySuffix(
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

    /// <summary>
    /// Gets all node IDs from tree recursively
    /// </summary>
    private static IEnumerable<int> GetAllNodeIds(IEnumerable<CategoryTreeNode> nodes)
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

    /// <summary>
    /// Creates a simple hash of options for cache key
    /// </summary>
    private static string GetOptionsHash(CategoryTreeOptions options)
    {
        return $"sort:{options.SortByName}_depth:{options.MaxDepth?.ToString() ?? "none"}";
    }

    #endregion
}
