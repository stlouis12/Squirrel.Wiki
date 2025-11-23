namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Generic caching service for application-wide caching needs
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Get a cached value by key
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
    
    /// <summary>
    /// Set a cached value with optional expiration
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;
    
    /// <summary>
    /// Remove a cached value by key
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Remove all cached values matching a pattern (e.g., "categories:*")
    /// </summary>
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if caching is enabled
    /// </summary>
    bool IsEnabled { get; }
}

/// <summary>
/// Cache key prefixes for different areas of the application
/// </summary>
public static class CacheKeys
{
    public const string Categories = "categories";
    public const string Tags = "tags";
    public const string Pages = "pages";
    public const string Menus = "menus";
    public const string Settings = "settings";
    
    /// <summary>
    /// Build a cache key with prefix and identifier
    /// </summary>
    public static string Build(string prefix, params object[] parts)
    {
        return $"{prefix}:{string.Join(":", parts)}";
    }
}
