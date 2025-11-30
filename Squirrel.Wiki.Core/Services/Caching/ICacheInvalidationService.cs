namespace Squirrel.Wiki.Core.Services.Caching;

/// <summary>
/// Service for managing cache invalidation across the application
/// Centralizes cache dependency management to ensure consistency
/// </summary>
public interface ICacheInvalidationService
{
    /// <summary>
    /// Invalidate all caches related to a specific page
    /// </summary>
    Task InvalidatePageAsync(int pageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate all caches related to a specific category
    /// </summary>
    Task InvalidateCategoryAsync(int categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate all caches related to tags
    /// </summary>
    Task InvalidateTagsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate all caches related to menus
    /// </summary>
    Task InvalidateMenusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate all caches related to settings
    /// </summary>
    Task InvalidateSettingsAsync(string? settingKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate all caches in the application
    /// </summary>
    Task InvalidateAllAsync(CancellationToken cancellationToken = default);
}
