using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Services.Menus;

/// <summary>
/// Service interface for menu management operations
/// </summary>
public interface IMenuService
{
    /// <summary>
    /// Gets a menu by its ID
    /// </summary>
    Task<MenuDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a menu by its name
    /// </summary>
    Task<MenuDto?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active menu for a specific menu type
    /// </summary>
    Task<MenuDto?> GetActiveMenuByTypeAsync(MenuType menuType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all menus
    /// </summary>
    Task<IEnumerable<MenuDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active menus ordered by display order
    /// </summary>
    Task<IEnumerable<MenuDto>> GetActiveMenusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new menu
    /// </summary>
    /// <param name="createDto">Menu creation data</param>
    /// <param name="forceActivation">If true, automatically deactivates other menus of the same type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<MenuDto> CreateAsync(MenuCreateDto createDto, bool forceActivation = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing menu
    /// </summary>
    /// <param name="id">Menu ID</param>
    /// <param name="updateDto">Menu update data</param>
    /// <param name="forceActivation">If true, automatically deactivates other menus of the same type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<MenuDto> UpdateAsync(int id, MenuUpdateDto updateDto, bool forceActivation = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a menu
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reorders menus
    /// </summary>
    Task ReorderAsync(Dictionary<int, int> menuOrders, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses MenuMarkup syntax into a structured menu
    /// </summary>
    Task<MenuStructureDto> ParseMenuMarkupAsync(string menuMarkup, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Parses menu markup into a structured format without resolving URLs (for sidebar use)
    /// </summary>
    Task<MenuStructureDto> ParseMenuMarkupWithoutResolvingAsync(string menuMarkup, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a menu to HTML
    /// </summary>
    Task<string> RenderMenuAsync(int menuId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a menu from MenuMarkup syntax to HTML
    /// </summary>
    Task<string> RenderMenuMarkupAsync(string menuMarkup, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders menu markup as Bootstrap navbar-compatible HTML
    /// </summary>
    Task<string> RenderMenuMarkupAsNavbarAsync(string menuMarkup, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates MenuMarkup syntax
    /// </summary>
    Task<MenuValidationResult> ValidateMenuMarkupAsync(string menuMarkup, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the default main navigation menu
    /// </summary>
    Task<MenuDto?> GetMainNavigationMenuAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a menu
    /// </summary>
    Task ActivateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates a menu
    /// </summary>
    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if there's an active menu of the specified type
    /// </summary>
    /// <param name="menuType">The menu type to check</param>
    /// <param name="excludeMenuId">Optional menu ID to exclude from the check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if an active menu of the type exists (excluding the specified ID)</returns>
    Task<bool> HasActiveMenuOfTypeAsync(MenuType menuType, int? excludeMenuId = null, CancellationToken cancellationToken = default);
}
