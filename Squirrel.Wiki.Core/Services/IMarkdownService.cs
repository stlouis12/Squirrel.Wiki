namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Service interface for Markdown rendering and processing
/// </summary>
public interface IMarkdownService
{
    /// <summary>
    /// Converts Markdown content to HTML
    /// </summary>
    Task<string> ToHtmlAsync(string markdown, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Converts Markdown content to plain text (strips HTML)
    /// </summary>
    Task<string> ToPlainTextAsync(string markdown, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Processes wiki-specific tokens in content (e.g., @@tagcloud@@, @@mainpage@@)
    /// </summary>
    Task<string> ProcessTokensAsync(string content, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extracts all internal page links from Markdown content
    /// </summary>
    Task<IEnumerable<string>> ExtractPageLinksAsync(string markdown, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates page links in content when a page is renamed
    /// </summary>
    Task<string> UpdatePageLinksAsync(string content, string oldTitle, string newTitle, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Converts internal slug-only links in HTML to full /wiki/{id}/{slug} URLs
    /// </summary>
    Task<string> ConvertInternalLinksAsync(string html, Func<string, Task<(int? id, string? slug)>> slugLookup, CancellationToken cancellationToken = default);
}
