namespace Squirrel.Wiki.Core.Services.Caching;

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
/// Centralized cache key definitions for the application
/// Provides type-safe cache key generation and eliminates magic strings
/// </summary>
public static class CacheKeys
{
    #region Page Cache Keys
    
    /// <summary>
    /// Cache key for a specific page by ID
    /// </summary>
    public static string Page(int id) => $"page:{id}";
    
    /// <summary>
    /// Cache key for all pages
    /// </summary>
    public const string AllPages = "pages:all";
    
    /// <summary>
    /// Cache key for pages filtered by category
    /// </summary>
    public static string PagesByCategory(int categoryId) => $"pages:category:{categoryId}";
    
    /// <summary>
    /// Cache key for pages filtered by tag
    /// </summary>
    public static string PagesByTag(string tag) => $"pages:tag:{tag}";
    
    /// <summary>
    /// Cache key for pages filtered by author
    /// </summary>
    public static string PagesByAuthor(string author) => $"pages:author:{author}";
    
    /// <summary>
    /// Pattern for all pages by category caches
    /// </summary>
    public const string PagesByCategoryPattern = "pages:category:*";
    
    /// <summary>
    /// Pattern for all pages by tag caches
    /// </summary>
    public const string PagesByTagPattern = "pages:tag:*";
    
    /// <summary>
    /// Pattern for all pages by author caches
    /// </summary>
    public const string PagesByAuthorPattern = "pages:author:*";
    
    #endregion

    #region Category Cache Keys
    
    /// <summary>
    /// Cache key for a specific category by ID
    /// </summary>
    public static string Category(int id) => $"category:{id}";
    
    /// <summary>
    /// Cache key for the category tree
    /// </summary>
    public const string CategoryTree = "category:tree";
    
    /// <summary>
    /// Cache key for all categories
    /// </summary>
    public const string AllCategories = "categories:all";
    
    /// <summary>
    /// Pattern for all category caches
    /// </summary>
    public const string CategoriesPattern = "category:*";
    
    #endregion

    #region Tag Cache Keys
    
    /// <summary>
    /// Cache key for all tags
    /// </summary>
    public const string AllTags = "tags:all";
    
    /// <summary>
    /// Cache key for a specific tag by name
    /// </summary>
    public static string Tag(string name) => $"tags:{name}";
    
    /// <summary>
    /// Pattern for all tag caches
    /// </summary>
    public const string TagsPattern = "tags:*";
    
    #endregion

    #region Menu Cache Keys
    
    /// <summary>
    /// Cache key for a specific menu by ID
    /// </summary>
    public static string Menu(int id) => $"menu:{id}";
    
    /// <summary>
    /// Cache key for a menu by name
    /// </summary>
    public static string MenuByName(string name) => $"menu:name:{name}";
    
    /// <summary>
    /// Cache key for a rendered menu for a specific role
    /// </summary>
    public static string RenderedMenu(int id, string role) => $"menu:{id}:rendered:{role}";
    
    /// <summary>
    /// Cache key for a rendered menu by name for a specific role
    /// </summary>
    public static string RenderedMenuByName(string name, string role) => $"menu:name:{name}:rendered:{role}";
    
    /// <summary>
    /// Pattern for all menu caches
    /// </summary>
    public const string MenusPattern = "menu:*";
    
    #endregion

    #region Settings Cache Keys
    
    /// <summary>
    /// Cache key for a specific setting
    /// </summary>
    public static string Setting(string key) => $"settings:{key}";
    
    /// <summary>
    /// Pattern for all settings caches
    /// </summary>
    public const string SettingsPattern = "settings:*";
    
    #endregion

    #region User Cache Keys
    
    /// <summary>
    /// Cache key for a specific user by ID
    /// </summary>
    public static string User(int id) => $"user:{id}";
    
    /// <summary>
    /// Cache key for a user by username
    /// </summary>
    public static string UserByUsername(string username) => $"user:username:{username}";
    
    /// <summary>
    /// Pattern for all user caches
    /// </summary>
    public const string UsersPattern = "user:*";
    
    #endregion
    
    #region Legacy Support
    
    /// <summary>
    /// Legacy cache key prefixes (kept for backward compatibility)
    /// </summary>
    public const string Categories = "categories";
    public const string Tags = "tags";
    public const string Pages = "pages";
    public const string Menus = "menus";
    public const string Settings = "settings";
    
    /// <summary>
    /// Build a cache key with prefix and identifier (legacy method)
    /// </summary>
    public static string Build(string prefix, params object[] parts)
    {
        return $"{prefix}:{string.Join(":", parts)}";
    }
    
    #endregion
}
