using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Services.Pages;
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

    /// <summary>
    /// Home page - demonstrates Result Pattern for MVC controllers
    /// Shows how to handle optional custom home page with clean error handling
    /// </summary>
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Home page accessed");
        
        // Wrap service call in Result pattern for clean error handling
        var result = await ExecuteWithResult(async () => 
            await _pageService.GetBySlugAsync("home", cancellationToken));
        
        // Use Result pattern to handle success/failure elegantly
        // Match returns IActionResult, so both lambdas must return IActionResult
        return result.Match<IActionResult>(
            onSuccess: homePage =>
            {
                if (homePage != null)
                {
                    // Redirect to the custom Home wiki page
                    _logger.LogInformation("Redirecting to custom Home page (ID: {PageId})", homePage.Id);
                    return RedirectToAction("Index", "Wiki", new { id = homePage.Id, slug = homePage.Slug, isHomePage = true });
                }
                
                // No custom home page, show default view
                return View();
            },
            onFailure: (error, code) =>
            {
                // Log warning but don't show error to user - just show default home
                _logger.LogWarning("Error checking for custom Home page: {Error}. Falling back to default", error);
                return View();
            }
        );
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

    #region Helper Methods

    /// <summary>
    /// Helper method to wrap service calls in Result pattern
    /// This shows how to adapt existing services for MVC controllers
    /// </summary>
    private async Task<Result<T>> ExecuteWithResult<T>(Func<Task<T?>> operation) where T : class
    {
        try
        {
            var value = await operation();
            
            // For nullable results, null is not an error - it's a valid "not found" state
            // We return success with null value, and let the caller decide what to do
            return Result<T>.Success(value!);
        }
        catch (Core.Exceptions.EntityNotFoundException ex)
        {
            _logger.LogWarning(ex, "Entity not found");
            return Result<T>.Failure(ex.Message, "ENTITY_NOT_FOUND");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in operation");
            return Result<T>.Failure("An unexpected error occurred", "INTERNAL_ERROR");
        }
    }

    #endregion
}
