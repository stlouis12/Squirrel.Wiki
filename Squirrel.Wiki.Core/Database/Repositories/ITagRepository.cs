using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Repositories;

/// <summary>
/// Repository interface for Tag operations
/// </summary>
public interface ITagRepository : IRepository<Tag, int>
{
    /// <summary>
    /// Gets a tag by its name (case-insensitive)
    /// </summary>
    Task<Tag?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all tags ordered by name
    /// </summary>
    Task<IEnumerable<Tag>> GetAllOrderedAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all tags with their page counts
    /// </summary>
    Task<IEnumerable<(Tag Tag, int PageCount)>> GetTagsWithCountsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the most popular tags (by page count)
    /// </summary>
    Task<IEnumerable<Tag>> GetPopularTagsAsync(int count, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Searches tags by name (partial match)
    /// </summary>
    Task<IEnumerable<Tag>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets or creates a tag by name
    /// </summary>
    Task<Tag> GetOrCreateAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the page count for a tag
    /// </summary>
    Task<int> GetPageCountAsync(int tagId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all tags for a specific page
    /// </summary>
    Task<IEnumerable<Tag>> GetTagsForPageAsync(int pageId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all unused tags (tags with no pages)
    /// </summary>
    Task<IEnumerable<Tag>> GetUnusedTagsAsync(CancellationToken cancellationToken = default);
}
