using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Service for building and manipulating hierarchical category trees
/// </summary>
public class CategoryTreeBuilder : ICategoryTreeBuilder
{
    private readonly IPageService _pageService;
    private readonly ILogger<CategoryTreeBuilder> _logger;

    public CategoryTreeBuilder(
        IPageService pageService,
        ILogger<CategoryTreeBuilder> logger)
    {
        _pageService = pageService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<List<CategoryTreeNode>> BuildTreeAsync(
        IEnumerable<CategoryDto> categories, 
        CategoryTreeOptions? options = null)
    {
        options ??= new CategoryTreeOptions();
        
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

        return await Task.FromResult(tree);
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
        var items = new List<CategorySelectItem>();
        excludedIds ??= new HashSet<int>();
        
        FlattenTreeRecursive(tree, items, excludedIds, "");
        
        return items;
    }

    #region Private Helper Methods

    /// <summary>
    /// Maps a CategoryDto to a CategoryTreeNode
    /// </summary>
    private CategoryTreeNode MapToTreeNode(CategoryDto category)
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
    private void SortTree(List<CategoryTreeNode> nodes, bool recursive)
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
    private List<CategoryTreeNode> LimitDepth(List<CategoryTreeNode> tree, int maxDepth)
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
    private CategoryTreeNode LimitNodeDepth(CategoryTreeNode node, int maxDepth, int currentDepth)
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
                _logger.LogError(ex, "Error applying authorization filter to category {CategoryId}", node.Id);
                node.PageCount = 0;
            }
        }
    }

    /// <summary>
    /// Recursively flattens tree into a list with indentation
    /// </summary>
    private void FlattenTreeRecursive(
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
                Name = prefix + node.Name,
                FullPath = node.FullPath,
                Level = node.Level,
                IsDisabled = excludedIds.Contains(node.Id)
            });

            if (node.Children.Any())
            {
                FlattenTreeRecursive(node.Children, items, excludedIds, prefix + "  ");
            }
        }
    }

    #endregion
}
