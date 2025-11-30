using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Services.Categories;

/// <summary>
/// Service for building and manipulating hierarchical category trees
/// </summary>
public interface ICategoryTreeBuilder
{
    /// <summary>
    /// Builds a category tree from a flat list of categories
    /// </summary>
    /// <param name="categories">Flat list of categories</param>
    /// <param name="options">Optional configuration for tree building</param>
    /// <returns>Hierarchical tree structure</returns>
    Task<List<CategoryTreeNode>> BuildTreeAsync(
        IEnumerable<CategoryDto> categories, 
        CategoryTreeOptions? options = null);
    
    /// <summary>
    /// Builds a category tree with authorization filtering and page counts
    /// </summary>
    /// <param name="categories">Flat list of categories</param>
    /// <param name="authorizationFilter">Function to filter pages by authorization</param>
    /// <param name="options">Optional configuration for tree building</param>
    /// <returns>Hierarchical tree structure with filtered page counts</returns>
    Task<List<CategoryTreeNode>> BuildTreeWithAuthorizationAsync(
        IEnumerable<CategoryDto> categories,
        Func<PageDto, Task<bool>> authorizationFilter,
        CategoryTreeOptions? options = null);
    
    /// <summary>
    /// Converts a category tree to a different node type
    /// </summary>
    /// <typeparam name="TNode">Target node type</typeparam>
    /// <param name="tree">Source tree</param>
    /// <param name="converter">Conversion function</param>
    /// <returns>Converted tree</returns>
    Task<List<TNode>> ConvertTreeAsync<TNode>(
        IEnumerable<CategoryTreeNode> tree,
        Func<CategoryTreeNode, Task<TNode>> converter) 
        where TNode : class;
    
    /// <summary>
    /// Flattens a tree into a list with indentation for dropdowns
    /// </summary>
    /// <param name="tree">Source tree</param>
    /// <param name="excludedIds">Optional set of IDs to mark as disabled</param>
    /// <returns>Flattened list suitable for select dropdowns</returns>
    List<CategorySelectItem> FlattenTreeForSelection(
        IEnumerable<CategoryTreeNode> tree,
        HashSet<int>? excludedIds = null);
}

/// <summary>
/// Configuration options for category tree building
/// </summary>
public class CategoryTreeOptions
{
    /// <summary>
    /// Include page counts in tree nodes
    /// </summary>
    public bool IncludePageCounts { get; set; } = true;
    
    /// <summary>
    /// Include subcategory counts in tree nodes
    /// </summary>
    public bool IncludeSubcategoryCounts { get; set; } = true;
    
    /// <summary>
    /// Sort nodes by name
    /// </summary>
    public bool SortByName { get; set; } = true;
    
    /// <summary>
    /// Sort recursively through all levels
    /// </summary>
    public bool SortRecursively { get; set; } = true;
    
    /// <summary>
    /// Maximum depth to include in tree (null = unlimited)
    /// </summary>
    public int? MaxDepth { get; set; }
}

/// <summary>
/// Tree node representing a category in hierarchical structure
/// </summary>
public class CategoryTreeNode
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string FullPath { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public int Level { get; set; }
    public int PageCount { get; set; }
    public int SubcategoryCount { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public string? ModifiedBy { get; set; }
    public List<CategoryTreeNode> Children { get; set; } = new();
}

/// <summary>
/// Item for category selection dropdowns
/// </summary>
public class CategorySelectItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public int Level { get; set; }
    public bool IsDisabled { get; set; }
    
    /// <summary>
    /// Display name with visual hierarchy indication using em-dashes
    /// </summary>
    public string DisplayName => Level > 0 ? new string('—', Level) + " " + Name : Name;
}
