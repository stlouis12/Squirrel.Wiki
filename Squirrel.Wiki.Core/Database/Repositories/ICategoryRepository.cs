using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Repositories;

/// <summary>
/// Repository interface for Category operations
/// </summary>
public interface ICategoryRepository : IRepository<Category, int>
{
    /// <summary>
    /// Gets a category by its slug
    /// </summary>
    Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all root categories (categories without a parent)
    /// </summary>
    Task<IEnumerable<Category>> GetRootCategoriesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all child categories of a parent category
    /// </summary>
    Task<IEnumerable<Category>> GetChildCategoriesAsync(int parentCategoryId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the full category tree starting from root categories
    /// </summary>
    Task<IEnumerable<Category>> GetCategoryTreeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the category path from root to the specified category
    /// </summary>
    Task<IEnumerable<Category>> GetCategoryPathAsync(int categoryId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all categories ordered by display order
    /// </summary>
    Task<IEnumerable<Category>> GetAllOrderedAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a category has child categories
    /// </summary>
    Task<bool> HasChildrenAsync(int categoryId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a category has pages
    /// </summary>
    Task<bool> HasPagesAsync(int categoryId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a category by its name
    /// </summary>
    Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all child categories of a parent
    /// </summary>
    Task<IEnumerable<Category>> GetChildrenAsync(int parentId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all categories at a specific depth level
    /// </summary>
    Task<IEnumerable<Category>> GetCategoriesByDepthAsync(int depth, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the page count for a category
    /// </summary>
    Task<int> GetPageCountAsync(int categoryId, CancellationToken cancellationToken = default);
}
