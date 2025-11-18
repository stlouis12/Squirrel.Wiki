using System.Text.RegularExpressions;
using Squirrel.Wiki.Core.Database.Repositories;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Parses footer markup into HTML for the three footer zones
/// </summary>
public class FooterMarkupParser
{
    private readonly IPageRepository _pageRepository;

    public FooterMarkupParser(IPageRepository pageRepository)
    {
        _pageRepository = pageRepository;
    }
    /// <summary>
    /// Parses footer zone content into HTML
    /// Supports both [[Text|URL]] and [Text](URL) syntax for links
    /// </summary>
    public async Task<string> ParseZoneContentAsync(string? content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;
        
        var html = content;
        
        // Parse [[Text|URL]] syntax for links (Roadkill style)
        html = await ProcessLinksAsync(html, @"\[\[([^\|]+)\|([^\]]+)\]\]", cancellationToken);
        
        // Parse [Text](URL) syntax for links (Markdown style)
        html = await ProcessLinksAsync(html, @"\[([^\]]+)\]\(([^\)]+)\)", cancellationToken);
        
        // Convert line breaks to <br> but preserve existing HTML
        html = html.Replace("\r\n", "\n").Replace("\n", "<br>");
        
        return html.Trim();
    }

    /// <summary>
    /// Synchronous version for backward compatibility
    /// </summary>
    public string ParseZoneContent(string? content)
    {
        return ParseZoneContentAsync(content).GetAwaiter().GetResult();
    }

    private async Task<string> ProcessLinksAsync(string html, string pattern, CancellationToken cancellationToken)
    {
        var matches = Regex.Matches(html, pattern);
        
        // Process matches in reverse order to maintain string positions
        for (int i = matches.Count - 1; i >= 0; i--)
        {
            var match = matches[i];
            var text = match.Groups[1].Value.Trim();
            var url = match.Groups[2].Value.Trim();
            
            // Resolve URL (handle page slugs, external links, etc.)
            var resolvedUrl = await ResolveUrlAsync(url, cancellationToken);
            
            // Determine if external link
            var isExternal = resolvedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                            resolvedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            
            var target = isExternal ? " target=\"_blank\" rel=\"noopener noreferrer\"" : "";
            var icon = isExternal ? " <i class=\"bi bi-box-arrow-up-right\"></i>" : "";
            
            var replacement = $"<a href=\"{resolvedUrl}\"{target}>{text}{icon}</a>";
            
            html = html.Substring(0, match.Index) + replacement + html.Substring(match.Index + match.Length);
        }
        
        return html;
    }

    private async Task<string> ResolveUrlAsync(string url, CancellationToken cancellationToken)
    {
        // If it's already a full URL (starts with / or http), return as-is
        if (url.StartsWith("/") || 
            url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        // Try to resolve as a page slug
        try
        {
            var page = await _pageRepository.GetBySlugAsync(url, cancellationToken);
            if (page != null)
            {
                return $"/wiki/{page.Id}/{page.Slug}";
            }
        }
        catch
        {
            // If resolution fails, return the original URL
        }

        // If we can't resolve it, return as-is (might be a relative path or typo)
        return url;
    }
}

/// <summary>
/// Represents parsed footer content for the two zones
/// </summary>
public class FooterContent
{
    public string LeftZone { get; set; } = string.Empty;
    public string RightZone { get; set; } = string.Empty;
    
    public bool HasContent => 
        !string.IsNullOrWhiteSpace(LeftZone) || 
        !string.IsNullOrWhiteSpace(RightZone);
}
