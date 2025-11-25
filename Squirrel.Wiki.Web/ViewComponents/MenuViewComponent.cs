using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Squirrel.Wiki.Core.Services.Menus;

namespace Squirrel.Wiki.Web.ViewComponents;

/// <summary>
/// View component for rendering menu markup
/// </summary>
public class MenuViewComponent : ViewComponent
{
    private readonly IMenuService _menuService;
    private readonly ILogger<MenuViewComponent> _logger;

    public MenuViewComponent(IMenuService menuService, ILogger<MenuViewComponent> logger)
    {
        _menuService = menuService;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the menu view component to render a menu by name
    /// </summary>
    /// <param name="menuName">The name of the menu to render</param>
    /// <returns>HTML content of the rendered menu</returns>
    public async Task<IViewComponentResult> InvokeAsync(string menuName)
    {
        try
        {
            // Get menu by name
            var menu = await _menuService.GetByNameAsync(menuName);
            
            if (menu == null || !menu.IsEnabled)
            {
                _logger.LogDebug("Menu '{MenuName}' not found or inactive", menuName);
                return Content(string.Empty);
            }

            // Render the menu markup to HTML
            var html = await _menuService.RenderMenuMarkupAsync(menu.MenuMarkup);
            
            return new ContentViewComponentResult(html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering menu '{MenuName}'", menuName);
            return Content(string.Empty);
        }
    }
}
