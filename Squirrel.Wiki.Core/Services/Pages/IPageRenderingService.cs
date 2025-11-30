namespace Squirrel.Wiki.Core.Services.Pages;

/// <summary>
/// Service for rendering page content from Markdown to HTML
/// </summary>
public interface IPageRenderingService
{
    /// <summary>
    /// Render markdown content to HTML with token processing
    /// </summary>
    Task<string> RenderContentAsync(string markdownContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Render the latest content of a page to HTML
    /// </summary>
    Task<string> RenderPageAsync(int pageId, CancellationToken cancellationToken = default);
}
