using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Services;
using Squirrel.Wiki.Web.Filters;

namespace Squirrel.Wiki.Web.Controllers;

public class HomeController : Controller
{
    private readonly IPageService _pageService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IPageService pageService, ILogger<HomeController> logger)
    {
        _pageService = pageService;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Home page accessed");
        
        // Check if a custom "Home" page exists
        try
        {
            var homePage = await _pageService.GetBySlugAsync("home", cancellationToken);
            
            if (homePage != null)
            {
                // Redirect to the custom Home wiki page with a flag to hide title/metadata
                _logger.LogInformation("Redirecting to custom Home page (ID: {PageId})", homePage.Id);
                return RedirectToAction("Index", "Wiki", new { id = homePage.Id, slug = homePage.Slug, isHomePage = true });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking for custom Home page, falling back to default");
        }
        
        // Fall back to default home view if no custom page exists
        return View();
    }

    public IActionResult About()
    {
        return View();
    }

    [DynamicResponseCache(NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}
