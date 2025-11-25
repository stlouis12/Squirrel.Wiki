using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Services.Pages;

/// <summary>
/// Service for core page management operations (CRUD)
/// </summary>
public interface IPageService
{
    /// <summary>
    /// Get a page by its ID
    /// </summary>
    Task<PageDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a page by its slug
    /// </summary>
    Task<PageDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a page by its title
    /// </summary>
    Task<PageDto?> GetByTitleAsync(string title, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all active pages
    /// </summary>
    Task<IEnumerable<PageDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all pages in a specific category
    /// </summary>
    Task<IEnumerable<PageDto>> GetByCategoryAsync(int categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all pages with a specific tag
    /// </summary>
    Task<IEnumerable<PageDto>> GetByTagAsync(string tag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all pages created by a specific user
    /// </summary>
    Task<IEnumerable<PageDto>> GetByAuthorAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new page
    /// </summary>
    Task<PageDto> CreateAsync(PageCreateDto createDto, string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing page
    /// </summary>
    Task<PageDto> UpdateAsync(int id, PageUpdateDto updateDto, string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft delete a page
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restore a soft-deleted page
    /// </summary>
    Task<PageDto> RestoreAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a unique slug for a page title
    /// </summary>
    Task<string> GenerateSlugAsync(string title, int? excludePageId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for pages by query
    /// </summary>
    Task<IEnumerable<PageDto>> SearchAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the designated home page
    /// </summary>
    Task<PageDto?> GetHomePageAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all tags for a specific page
    /// </summary>
    Task<IEnumerable<TagDto>> GetPageTagsAsync(int pageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get category information for a page
    /// </summary>
    Task<CategoryDto?> GetCategoryAsync(int categoryId, CancellationToken cancellationToken = default);
}
