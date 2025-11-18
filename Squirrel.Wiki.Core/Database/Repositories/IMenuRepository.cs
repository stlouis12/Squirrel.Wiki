using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Repositories;

/// <summary>
/// Repository interface for Menu operations
/// </summary>
public interface IMenuRepository : IRepository<Menu, int>
{
    /// <summary>
    /// Gets a menu by its name
    /// </summary>
    Task<Menu?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the active menu for a specific menu type
    /// </summary>
    Task<Menu?> GetActiveByTypeAsync(MenuType menuType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all menus of a specific type
    /// </summary>
    Task<IEnumerable<Menu>> GetByTypeAsync(MenuType menuType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if there is an active menu of the specified type (optionally excluding a specific menu)
    /// </summary>
    Task<bool> HasActiveMenuOfTypeAsync(MenuType menuType, int? excludeId = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all active menus
    /// </summary>
    Task<IEnumerable<Menu>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all menus ordered by display order
    /// </summary>
    Task<IEnumerable<Menu>> GetAllOrderedAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reorders menus
    /// </summary>
    Task ReorderAsync(int menuId, int newDisplayOrder, CancellationToken cancellationToken = default);
}
