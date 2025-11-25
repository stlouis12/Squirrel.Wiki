using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Services.Categories;

namespace Squirrel.Wiki.Core.Services.Content;

/// <summary>
/// Service implementation for resolving URL tokens used in menus and navigation
/// </summary>
public class UrlTokenResolver : IUrlTokenResolver
{
    private readonly ICategoryService _categoryService;
    private readonly IPageRepository _pageRepository;
    private readonly ILogger<UrlTokenResolver> _logger;

    // Standard URL tokens that map to fixed URLs
    private static readonly Dictionary<string, string> StandardTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        { "%ALLPAGES%", "/Pages/AllPages" },
        { "%ALLTAGS%", "/Pages/AllTags" },
        { "%ALLCATEGORIES%", "/Pages/Category" },
        { "%SEARCH%", "/Search" },
        { "%NEWPAGE%", "/Pages/New" },
        { "%HOME%", "/Home" },
        { "%CATEGORIES%", "/Categories" },
        { "%SITEMAP%", "/Sitemap" },
        { "%RECENTLYUPDATED%", "/Pages/AllPages?recent=10" },
        { "%ADMIN%", "/Admin" }
    };

    public UrlTokenResolver(
        ICategoryService categoryService,
        IPageRepository pageRepository,
        ILogger<UrlTokenResolver> logger)
    {
        _categoryService = categoryService;
        _pageRepository = pageRepository;
        _logger = logger;
    }

    /// <summary>
    /// Resolves a URL token to its actual URL
    /// </summary>
    public async Task<string?> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        // Check for tag: prefix (e.g., tag:tutorial)
        if (token.StartsWith("tag:", StringComparison.OrdinalIgnoreCase))
        {
            var tagName = token.Substring("tag:".Length).Trim();
            return $"/Pages/AllPages?tag={Uri.EscapeDataString(tagName)}";
        }

        // Check for category: prefix with path support (e.g., category:documentation or category:docs:api)
        if (token.StartsWith("category:", StringComparison.OrdinalIgnoreCase))
        {
            var categoryPath = token.Substring("category:".Length).Trim();
            
            try
            {
                // Try to resolve the category by path
                var category = await _categoryService.GetByPathAsync(categoryPath, cancellationToken);
                
                if (category != null)
                {
                    // Use AllPages with categoryId parameter for reliable navigation
                    _logger.LogDebug("Resolved category path '{CategoryPath}' (ID: {CategoryId}) to URL '/Pages/AllPages?categoryId={CategoryId}'", 
                        categoryPath, category.Id, category.Id);
                    
                    return $"/Pages/AllPages?categoryId={category.Id}";
                }
                else
                {
                    // Category not found - log warning and return null (token cannot be resolved)
                    _logger.LogWarning("Category path '{CategoryPath}' not found. Token cannot be resolved.", categoryPath);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve category path '{CategoryPath}'", categoryPath);
                
                // Return fallback URL on error using category name
                var categoryName = categoryPath.Split(':', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? categoryPath;
                return $"/Pages/Category?categoryName={Uri.EscapeDataString(categoryName)}";
            }
        }

        // Check for standard tokens
        if (StandardTokens.TryGetValue(token, out var url))
        {
            return url;
        }

        // Check if it's already a full path (starts with / or http)
        if (token.StartsWith("/") || token.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            token.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return token;
        }

        // Assume it's a page slug - try to resolve it
        try
        {
            var page = await _pageRepository.GetBySlugAsync(token, cancellationToken);
            if (page != null)
            {
                return $"/wiki/{page.Id}/{page.Slug}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve page slug '{Slug}'", token);
        }

        // If we can't resolve it, return as-is (might be an external link or typo)
        return token;
    }

    /// <summary>
    /// Checks if a token is a standard token (starts with % and ends with %)
    /// </summary>
    public bool IsStandardToken(string token)
    {
        return !string.IsNullOrWhiteSpace(token) && 
               token.StartsWith("%") && 
               token.EndsWith("%");
    }

    /// <summary>
    /// Checks if a token is a dynamic token (tag: or category: prefix)
    /// </summary>
    public bool IsDynamicToken(string token)
    {
        return !string.IsNullOrWhiteSpace(token) && 
               (token.StartsWith("tag:", StringComparison.OrdinalIgnoreCase) || 
                token.StartsWith("category:", StringComparison.OrdinalIgnoreCase));
    }
}
