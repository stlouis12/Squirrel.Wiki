namespace Squirrel.Wiki.Core.Services.Pages;

/// <summary>
/// Service for managing links between pages
/// </summary>
public interface IPageLinkService
{
    /// <summary>
    /// Update all links to a page when its title changes
    /// </summary>
    Task UpdateLinksToPageAsync(string oldTitle, string newTitle, CancellationToken cancellationToken = default);
}
