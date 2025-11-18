using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Service interface for tag management operations
/// </summary>
public interface ITagService
{
    /// <summary>
    /// Gets a tag by its ID
    /// </summary>
    Task<TagDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a tag by its name
    /// </summary>
    Task<TagDto?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all tags
    /// </summary>
    Task<IEnumerable<TagDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all tags with their usage counts
    /// </summary>
    Task<IEnumerable<TagWithCountDto>> GetAllWithCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most popular tags
    /// </summary>
    Task<IEnumerable<TagWithCountDto>> GetPopularTagsAsync(int count = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all tags used by a specific page
    /// </summary>
    Task<IEnumerable<TagDto>> GetTagsForPageAsync(int pageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all pages with a specific tag
    /// </summary>
    Task<IEnumerable<PageDto>> GetPagesWithTagAsync(string tagName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tag cloud data for visualization
    /// </summary>
    Task<IEnumerable<TagCloudItemDto>> GetTagCloudAsync(int minCount = 1, int maxTags = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new tag
    /// </summary>
    Task<TagDto> CreateAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or creates a tag by name
    /// </summary>
    Task<TagDto> GetOrCreateAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing tag
    /// </summary>
    Task<TagDto> UpdateAsync(int id, string newName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a tag (updates all page associations)
    /// </summary>
    Task<TagDto> RenameAsync(int id, string newName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges two tags (moves all pages from source to target tag)
    /// </summary>
    Task MergeTagsAsync(int sourceTagId, int targetTagId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a tag
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes unused tags (tags with no associated pages)
    /// </summary>
    Task<int> CleanupUnusedTagsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for tags by name pattern
    /// </summary>
    Task<IEnumerable<TagDto>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets related tags (tags that frequently appear together)
    /// </summary>
    Task<IEnumerable<TagDto>> GetRelatedTagsAsync(int tagId, int count = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tag statistics
    /// </summary>
    Task<TagStatsDto> GetTagStatsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all tags (alias for GetAllAsync for controller compatibility)
    /// </summary>
    Task<IEnumerable<TagDto>> GetAllTagsAsync(CancellationToken cancellationToken = default);
}
