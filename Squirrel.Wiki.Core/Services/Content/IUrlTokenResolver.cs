namespace Squirrel.Wiki.Core.Services.Content;

/// <summary>
/// Service for resolving URL tokens used in menus and navigation
/// </summary>
public interface IUrlTokenResolver
{
    /// <summary>
    /// Resolves a URL token to its actual URL
    /// </summary>
    /// <param name="token">The token to resolve (e.g., %ALLPAGES%, tag:tutorial, category:docs)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The resolved URL, or null if the token cannot be resolved</returns>
    Task<string?> ResolveTokenAsync(string token, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a token is a standard token (starts with % and ends with %)
    /// </summary>
    bool IsStandardToken(string token);
    
    /// <summary>
    /// Checks if a token is a dynamic token (tag: or category: prefix)
    /// </summary>
    bool IsDynamicToken(string token);
}
