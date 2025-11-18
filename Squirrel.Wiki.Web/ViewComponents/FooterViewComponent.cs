using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Services;

namespace Squirrel.Wiki.Web.ViewComponents;

/// <summary>
/// View component for rendering the site footer
/// </summary>
public class FooterViewComponent : ViewComponent
{
    private readonly IMenuService _menuService;
    private readonly FooterMarkupParser _footerParser;
    private readonly ILogger<FooterViewComponent> _logger;

    public FooterViewComponent(
        IMenuService menuService,
        FooterMarkupParser footerParser,
        ILogger<FooterViewComponent> logger)
    {
        _menuService = menuService;
        _footerParser = footerParser;
        _logger = logger;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        try
        {
            // Get active footer menu
            var footerMenu = await _menuService.GetActiveMenuByTypeAsync(MenuType.Footer, CancellationToken.None);
            
            if (footerMenu == null || !footerMenu.IsEnabled)
            {
                // Return empty footer if no active footer configured
                return View("Default", new FooterContent());
            }

            // Parse footer zones
            var footerContent = new FooterContent
            {
                LeftZone = await _footerParser.ParseZoneContentAsync(footerMenu.FooterLeftZone, CancellationToken.None),
                RightZone = await _footerParser.ParseZoneContentAsync(footerMenu.FooterRightZone, CancellationToken.None)
            };

            return View("Default", footerContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering footer");
            return View("Default", new FooterContent());
        }
    }
}
