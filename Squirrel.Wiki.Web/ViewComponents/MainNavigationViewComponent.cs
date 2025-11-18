using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Services;

namespace Squirrel.Wiki.Web.ViewComponents;

/// <summary>
/// View component for rendering the main navigation menu
/// </summary>
public class MainNavigationViewComponent : ViewComponent
{
    private readonly IMenuService _menuService;
    private readonly ILogger<MainNavigationViewComponent> _logger;

    public MainNavigationViewComponent(
        IMenuService menuService,
        ILogger<MainNavigationViewComponent> logger)
    {
        _menuService = menuService;
        _logger = logger;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        try
        {
            // Get the active MainNavigation menu by type
            var mainMenu = await _menuService.GetActiveMenuByTypeAsync(MenuType.MainNavigation);
            
            if (mainMenu != null)
            {
                // Render the menu HTML
                var menuHtml = await _menuService.RenderMenuAsync(mainMenu.Id);
                // Return raw HTML content
                ViewData["MenuHtml"] = new HtmlString(menuHtml);
                return View("Custom");
            }
            
            // Fallback to default navigation if no main menu exists
            _logger.LogDebug("No active MainNavigation menu found, using default navigation");
            return View("Default");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading main navigation menu");
            // Fallback to default navigation on error
            return View("Default");
        }
    }
}
