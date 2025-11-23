using Markdig;
using System.Text.RegularExpressions;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Service for Markdown rendering and processing using Markdig
/// </summary>
public class MarkdownService : IMarkdownService
{
    private readonly MarkdownPipeline _pipeline;
    private readonly ISlugGenerator _slugGenerator;
    private static readonly Regex WikiLinkRegex = new(@"\[\[([^\]]+)\]\]", RegexOptions.Compiled);
    private static readonly Regex MarkdownLinkRegex = new(@"\[([^\]]+)\]\(([^\)]+)\)", RegexOptions.Compiled);

    public MarkdownService(ISlugGenerator slugGenerator)
    {
        _slugGenerator = slugGenerator;
        
        // Configure Markdig pipeline with common extensions
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseAutoLinks()
            .UseTaskLists()
            .UseEmojiAndSmiley()
            .UsePipeTables()
            .UseGridTables()
            .UseFootnotes()
            .UseAutoIdentifiers()
            .Build();
    }

    public Task<string> ToHtmlAsync(string markdown, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return Task.FromResult(string.Empty);

        // Convert wiki-style links [[Page Title]] to markdown links
        markdown = ConvertWikiLinksToMarkdown(markdown);

        // Convert to HTML using Markdig
        var html = Markdown.ToHtml(markdown, _pipeline);

        return Task.FromResult(html);
    }

    public Task<string> ToPlainTextAsync(string markdown, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return Task.FromResult(string.Empty);

        // Convert to HTML first
        var html = Markdown.ToHtml(markdown, _pipeline);

        // Strip HTML tags
        var plainText = Regex.Replace(html, "<.*?>", string.Empty);
        
        // Decode HTML entities
        plainText = System.Net.WebUtility.HtmlDecode(plainText);
        
        // Normalize whitespace
        plainText = Regex.Replace(plainText, @"\s+", " ").Trim();

        return Task.FromResult(plainText);
    }

    public Task<string> ProcessTokensAsync(string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Task.FromResult(string.Empty);

        // Process special wiki tokens
        // These will be replaced with actual content by the PageService
        // For now, we just mark them for processing
        
        // @@TOC@@ - Table of Contents
        content = content.Replace("@@TOC@@", "<div class=\"wiki-toc\" data-token=\"toc\"></div>", StringComparison.OrdinalIgnoreCase);
        
        // @@TAGCLOUD@@ - Tag cloud
        content = content.Replace("@@TAGCLOUD@@", "<div class=\"wiki-tagcloud\" data-token=\"tagcloud\"></div>", StringComparison.OrdinalIgnoreCase);
        
        // @@MAINPAGE@@ - Main page link
        content = content.Replace("@@MAINPAGE@@", "<a href=\"/\" class=\"wiki-mainpage\">Main Page</a>", StringComparison.OrdinalIgnoreCase);
        
        // @@ALLPAGES@@ - All pages list
        content = content.Replace("@@ALLPAGES@@", "<div class=\"wiki-allpages\" data-token=\"allpages\"></div>", StringComparison.OrdinalIgnoreCase);
        
        // @@CATEGORIES@@ - Category list
        content = content.Replace("@@CATEGORIES@@", "<div class=\"wiki-categories\" data-token=\"categories\"></div>", StringComparison.OrdinalIgnoreCase);

        return Task.FromResult(content);
    }

    public Task<IEnumerable<string>> ExtractPageLinksAsync(string markdown, CancellationToken cancellationToken = default)
    {
        var links = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(markdown))
            return Task.FromResult<IEnumerable<string>>(links);

        // Extract wiki-style links [[Page Title]]
        var wikiMatches = WikiLinkRegex.Matches(markdown);
        foreach (Match match in wikiMatches)
        {
            if (match.Groups.Count > 1)
            {
                var linkText = match.Groups[1].Value.Trim();
                // Handle [[Page Title|Display Text]] format
                var parts = linkText.Split('|');
                links.Add(parts[0].Trim());
            }
        }

        // Extract markdown-style links that might be internal pages
        var markdownMatches = MarkdownLinkRegex.Matches(markdown);
        foreach (Match match in markdownMatches)
        {
            if (match.Groups.Count > 2)
            {
                var url = match.Groups[2].Value.Trim();
                // Only include relative URLs (internal pages)
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("//", StringComparison.Ordinal))
                {
                    // Remove leading slash and .html extension if present
                    url = url.TrimStart('/').Replace(".html", "", StringComparison.OrdinalIgnoreCase);
                    if (!string.IsNullOrWhiteSpace(url))
                        links.Add(url);
                }
            }
        }

        return Task.FromResult<IEnumerable<string>>(links);
    }

    public Task<string> UpdatePageLinksAsync(string content, string oldTitle, string newTitle, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(oldTitle) || string.IsNullOrWhiteSpace(newTitle))
            return Task.FromResult(content);

        // Update wiki-style links [[Old Title]] to [[New Title]]
        var wikiLinkPattern = $@"\[\[{Regex.Escape(oldTitle)}(\|[^\]]+)?\]\]";
        content = Regex.Replace(content, wikiLinkPattern, match =>
        {
            var displayText = match.Groups[1].Value; // Includes the | if present
            return $"[[{newTitle}{displayText}]]";
        }, RegexOptions.IgnoreCase);

        // Update markdown-style links [text](old-title) to [text](new-title)
        var oldSlug = _slugGenerator.GenerateSlug(oldTitle);
        var newSlug = _slugGenerator.GenerateSlug(newTitle);
        var markdownLinkPattern = $@"\[([^\]]+)\]\({Regex.Escape(oldSlug)}(\.html)?\)";
        content = Regex.Replace(content, markdownLinkPattern, match =>
        {
            var linkText = match.Groups[1].Value;
            return $"[{linkText}]({newSlug})";
        }, RegexOptions.IgnoreCase);

        return Task.FromResult(content);
    }

    /// <summary>
    /// Converts wiki-style links [[Page Title]] to markdown links
    /// </summary>
    private string ConvertWikiLinksToMarkdown(string markdown)
    {
        return WikiLinkRegex.Replace(markdown, match =>
        {
            var linkText = match.Groups[1].Value.Trim();
            
            // Handle [[Page Title|Display Text]] format
            var parts = linkText.Split('|');
            var pageTitle = parts[0].Trim();
            var displayText = parts.Length > 1 ? parts[1].Trim() : pageTitle;
            
            // Generate slug from page title
            var slug = _slugGenerator.GenerateSlug(pageTitle);
            
            return $"[{displayText}](/{slug})";
        });
    }


    public async Task<string> ConvertInternalLinksAsync(string html, Func<string, Task<(int? id, string? slug)>> slugLookup, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(html))
            return html;

        // Pattern to match <a href="/slug"> or <a href="slug"> (internal links without http/https)
        var linkPattern = new Regex(@"<a\s+([^>]*\s+)?href=""/?([^"":/]+)""([^>]*)>", RegexOptions.IgnoreCase);
        
        var matches = linkPattern.Matches(html);
        var replacements = new Dictionary<string, string>();

        foreach (Match match in matches)
        {
            var fullMatch = match.Value;
            var beforeHref = match.Groups[1].Value;
            var slug = match.Groups[2].Value;
            var afterHref = match.Groups[3].Value;

            // Skip if it's already a full path (contains /)
            if (slug.Contains('/'))
                continue;

            // Skip common non-page links
            if (slug.Equals("javascript:void(0)", StringComparison.OrdinalIgnoreCase) ||
                slug.StartsWith("#", StringComparison.Ordinal) ||
                slug.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                continue;

            // Look up the page by slug
            var (pageId, actualSlug) = await slugLookup(slug);

            if (pageId.HasValue && !string.IsNullOrEmpty(actualSlug))
            {
                // Convert to full wiki URL
                var newHref = $"/wiki/{pageId.Value}/{actualSlug}";
                var newLink = $"<a {beforeHref}href=\"{newHref}\"{afterHref}>";
                
                if (!replacements.ContainsKey(fullMatch))
                {
                    replacements[fullMatch] = newLink;
                }
            }
        }

        // Apply all replacements
        foreach (var replacement in replacements)
        {
            html = html.Replace(replacement.Key, replacement.Value);
        }

        return html;
    }
}
