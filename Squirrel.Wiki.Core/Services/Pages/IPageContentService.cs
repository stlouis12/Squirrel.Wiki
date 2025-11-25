using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Services.Pages;

/// <summary>
/// Service for managing page content versioning and history
/// </summary>
public interface IPageContentService
{
    /// <summary>
    /// Get the latest content version for a page
    /// </summary>
    Task<PageContentDto> GetLatestContentAsync(int pageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all content versions for a page (history)
    /// </summary>
    Task<IEnumerable<PageContentDto>> GetContentHistoryAsync(int pageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific content version for a page
    /// </summary>
    Task<PageContentDto?> GetContentByVersionAsync(int pageId, int version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revert a page to a previous content version
    /// </summary>
    Task<PageDto> RevertToVersionAsync(int pageId, int version, string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new content version for a page
    /// </summary>
    Task CreateContentVersionAsync(int pageId, string content, string username, string? changeComment = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the latest content version (when versioning is disabled)
    /// </summary>
    Task UpdateContentVersionAsync(int pageId, string content, string username, string? changeComment = null, CancellationToken cancellationToken = default);
}
