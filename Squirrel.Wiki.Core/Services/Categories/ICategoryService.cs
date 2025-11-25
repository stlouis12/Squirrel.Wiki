using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Services.Categories;

/// <summary>
/// Service interface for category management operations
/// </summary>
public interface ICategoryService
{
    /// <summary>
    /// Gets a category by its ID
    /// </summary>
    Task<CategoryDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a category by its name
    /// </summary>
    Task<CategoryDto?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all categories
    /// </summary>
    Task<IEnumerable<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all root categories (categories without a parent)
    /// </summary>
    Task<IEnumerable<CategoryDto>> GetRootCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all child categories of a parent category
    /// </summary>
    Task<IEnumerable<CategoryDto>> GetChildrenAsync(int parentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the complete category tree starting from root categories
    /// </summary>
    Task<IEnumerable<CategoryTreeDto>> GetCategoryTreeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the category tree starting from a specific category
    /// </summary>
    Task<CategoryTreeDto?> GetCategorySubtreeAsync(int categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full path from root to a specific category
    /// </summary>
    Task<IEnumerable<CategoryDto>> GetCategoryPathAsync(int categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all categories at a specific depth level
    /// </summary>
    Task<IEnumerable<CategoryDto>> GetCategoriesByDepthAsync(int depth, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new category
    /// </summary>
    Task<CategoryDto> CreateAsync(CategoryCreateDto createDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing category
    /// </summary>
    Task<CategoryDto> UpdateAsync(int id, CategoryUpdateDto updateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a category to a new parent
    /// </summary>
    Task<CategoryDto> MoveAsync(int categoryId, int? newParentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a category
    /// </summary>
    Task DeleteAsync(int id, bool deleteChildren = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the page count for a category (including subcategories if specified)
    /// </summary>
    Task<int> GetPageCountAsync(int categoryId, bool includeSubcategories = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all pages in a category
    /// </summary>
    Task<IEnumerable<PageDto>> GetPagesAsync(int categoryId, bool includeSubcategories = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a category name is available
    /// </summary>
    Task<bool> IsCategoryNameAvailableAsync(string name, int? excludeCategoryId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that moving a category won't create a circular reference
    /// </summary>
    Task<bool> ValidateMoveAsync(int categoryId, int? newParentId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all categories (alias for GetAllAsync for controller compatibility)
    /// </summary>
    Task<IEnumerable<CategoryDto>> GetAllCategoriesAsync(CancellationToken cancellationToken = default);
    
    // ============================================================================
    // PHASE 8: NESTED CATEGORIES - NEW METHODS
    // ============================================================================
    
    /// <summary>
    /// Gets a category by its path (e.g., "documentation:gettingstarted:installation")
    /// </summary>
    Task<CategoryDto?> GetByPathAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the full path string for a category (e.g., "documentation/gettingstarted/installation")
    /// </summary>
    Task<string> GetFullPathAsync(int categoryId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the depth level of a category (root = 1, child = 2, grandchild = 3)
    /// </summary>
    Task<int> GetDepthAsync(int categoryId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all subcategories of a parent category (direct children only)
    /// </summary>
    Task<IEnumerable<CategoryDto>> GetSubCategoriesAsync(int parentId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a category can be deleted (has no subcategories or pages)
    /// </summary>
    Task<bool> CanDeleteAsync(int categoryId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reorders a category within its parent
    /// </summary>
    Task ReorderAsync(int categoryId, int newDisplayOrder, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a category with options for handling pages and subcategories
    /// </summary>
    Task DeleteAsync(int id, CategoryDeleteOptions options, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the maximum allowed category depth
    /// </summary>
    int GetMaxCategoryDepth();
}

/// <summary>
/// Options for deleting a category
/// </summary>
public class CategoryDeleteOptions
{
    public CategoryDeleteAction Action { get; set; }
    public int? TargetCategoryId { get; set; }
}

/// <summary>
/// Actions to take when deleting a category that contains pages
/// </summary>
public enum CategoryDeleteAction
{
    /// <summary>
    /// Move pages to parent category (or uncategorized if root)
    /// </summary>
    MoveToParent,
    
    /// <summary>
    /// Move pages to a specified category
    /// </summary>
    MoveToCategory,
    
    /// <summary>
    /// Remove category from pages (pages become uncategorized)
    /// </summary>
    RemoveCategory
}
