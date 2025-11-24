namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Service for managing cache invalidation across the application
/// Centralizes cache dependency management to ensure consistency
/// </summary>
public interface ICacheInvalidationService
{
    /// <summary>
    /// Invalidate all caches related to a specific page
    /// </summary>
    Task InvalidatePageAsync(int pageId, CancellationToken ct = default);

    /// <summary>
    /// Invalidate all caches related to a specific category
    /// </summary>
    Task InvalidateCategoryAsync(int categoryId, CancellationToken ct = default);

    /// <summary>
    /// Invalidate all caches related to tags
    /// </summary>
    Task InvalidateTagsAsync(CancellationToken ct = default);

    /// <summary>
    /// Invalidate all caches related to menus
    /// </summary>
    Task InvalidateMenusAsync(CancellationToken ct = default);

    /// <summary>
    /// Invalidate all caches related to settings
    /// </summary>
    Task InvalidateSettingsAsync(string? settingKey = null, CancellationToken ct = default);

    /// <summary>
    /// Invalidate all caches in the application
    /// </summary>
    Task InvalidateAllAsync(CancellationToken ct = default);
}
