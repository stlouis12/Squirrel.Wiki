using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Repositories;

/// <summary>
/// Repository interface for Page operations
/// </summary>
public interface IPageRepository : IRepository<Page, int>
{
    /// <summary>
    /// Gets a page by its slug (URL-friendly identifier)
    /// </summary>
    Task<Page?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a page by its title (case-insensitive)
    /// </summary>
    Task<Page?> GetByTitleAsync(string title, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all pages in a specific category
    /// </summary>
    Task<IEnumerable<Page>> GetByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all pages created by a specific user
    /// </summary>
    Task<IEnumerable<Page>> GetByCreatedByAsync(string username, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all pages modified by a specific user
    /// </summary>
    Task<IEnumerable<Page>> GetByModifiedByAsync(string username, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all pages containing a specific tag
    /// </summary>
    Task<IEnumerable<Page>> GetByTagAsync(string tag, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all non-deleted pages
    /// </summary>
    Task<IEnumerable<Page>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all deleted pages
    /// </summary>
    Task<IEnumerable<Page>> GetAllDeletedAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Searches pages by title or content
    /// </summary>
    Task<IEnumerable<Page>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the latest content for a page
    /// </summary>
    Task<PageContent?> GetLatestContentAsync(int pageId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all content versions for a page
    /// </summary>
    Task<IEnumerable<PageContent>> GetAllContentVersionsAsync(int pageId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a specific content version
    /// </summary>
    Task<PageContent?> GetContentByVersionAsync(int pageId, int versionNumber, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adds a new content version to a page
    /// </summary>
    Task<PageContent> AddContentVersionAsync(PageContent content, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an existing content version
    /// </summary>
    Task UpdateContentVersionAsync(PageContent content, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Soft deletes a page (sets IsDeleted flag)
    /// </summary>
    Task SoftDeleteAsync(int pageId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Restores a soft-deleted page
    /// </summary>
    Task RestoreAsync(int pageId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all pages by author username
    /// </summary>
    Task<IEnumerable<Page>> GetByAuthorAsync(string authorUsername, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all page content history by author username
    /// </summary>
    Task<IEnumerable<PageContent>> GetContentHistoryByAuthorAsync(string authorUsername, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all pages in a specific category
    /// </summary>
    Task<IEnumerable<Page>> GetByCategoryAsync(int categoryId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates the tags for a page
    /// </summary>
    Task UpdateTagsAsync(int pageId, IEnumerable<string> tagNames, CancellationToken cancellationToken = default);
}
