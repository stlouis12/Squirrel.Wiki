using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Services.Menus;
using System.Text.RegularExpressions;

namespace Squirrel.Wiki.Web.ViewComponents;

/// <summary>
/// View component for rendering the main navigation menu
/// </summary>
public class MainNavigationViewComponent : ViewComponent
{
    private readonly IMenuService _menuService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<MainNavigationViewComponent> _logger;

    public MainNavigationViewComponent(
        IMenuService menuService,
        IAuthorizationService authorizationService,
        ILogger<MainNavigationViewComponent> logger)
    {
        _menuService = menuService;
        _authorizationService = authorizationService;
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
                // Filter menu markup based on authorization before rendering
                var filteredMarkup = await FilterMenuMarkupByAuthorizationAsync(mainMenu.MenuMarkup);
                
                // Render the filtered menu HTML
                var menuHtml = await _menuService.RenderMenuMarkupAsNavbarAsync(filteredMarkup);
                
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

    /// <summary>
    /// Filters menu markup to remove items the user doesn't have permission to see
    /// </summary>
    private async Task<string> FilterMenuMarkupByAuthorizationAsync(string menuMarkup)
    {
        if (string.IsNullOrWhiteSpace(menuMarkup))
        {
            return menuMarkup;
        }

        var userRole = HttpContext.User.Identity?.IsAuthenticated == true 
            ? HttpContext.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value 
            : null;

        var lines = menuMarkup.Split('\n');
        var filteredLines = new List<string>();

        foreach (var line in lines)
        {
            // Check if line contains restricted tokens
            if (ShouldIncludeMenuItem(line, userRole))
            {
                filteredLines.Add(line);
            }
        }

        return string.Join('\n', filteredLines);
    }

    /// <summary>
    /// Determines if a menu item should be included based on user authorization
    /// </summary>
    private bool ShouldIncludeMenuItem(string line, string? userRole)
    {
        // Extract URL from menu markup line (format: * [Text](URL) or ** [Text](URL))
        var urlMatch = Regex.Match(line, @"\]\(([^\)]+)\)");
        if (!urlMatch.Success)
        {
            // No URL found, include the line (might be a header)
            return true;
        }

        var url = urlMatch.Groups[1].Value.Trim();

        // Check for %ADMIN% token - only for Admin role
        if (url.Equals("%ADMIN%", StringComparison.OrdinalIgnoreCase))
        {
            return userRole?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;
        }

        // Check for %NEWPAGE% token - only for Admin and Editor roles
        if (url.Equals("%NEWPAGE%", StringComparison.OrdinalIgnoreCase))
        {
            return userRole?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
                   userRole?.Equals("Editor", StringComparison.OrdinalIgnoreCase) == true;
        }

        // All other items are visible to everyone
        return true;
    }
}
