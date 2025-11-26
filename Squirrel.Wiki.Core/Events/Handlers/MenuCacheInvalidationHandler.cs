using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Events.Menus;
using Squirrel.Wiki.Core.Services.Caching;

namespace Squirrel.Wiki.Core.Events.Handlers;

/// <summary>
/// Handles cache invalidation for menu-related events
/// </summary>
public class MenuCacheInvalidationHandler : IEventHandler<MenuChangedEvent>
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<MenuCacheInvalidationHandler> _logger;

    public MenuCacheInvalidationHandler(
        ICacheService cacheService,
        ILogger<MenuCacheInvalidationHandler> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task HandleAsync(MenuChangedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Invalidating cache for menu changed: {MenuId} - {MenuName}", 
            domainEvent.MenuId, domainEvent.MenuName);

        // Invalidate all menu-related caches
        await _cacheService.RemoveByPatternAsync(CacheKeys.MenusPattern, cancellationToken);

        _logger.LogDebug("Menu cache invalidation complete for menu: {MenuId}", domainEvent.MenuId);
    }
}
