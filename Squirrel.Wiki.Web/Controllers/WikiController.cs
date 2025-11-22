using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Services;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Web.Models;

namespace Squirrel.Wiki.Web.Controllers;

/// <summary>
/// Controller for displaying wiki pages at /wiki/{id}/{slug}
/// </summary>
public class WikiController : Controller
{
    private readonly IPageService _pageService;
    private readonly IMarkdownService _markdownService;
    private readonly ICategoryService _categoryService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IPageRepository _pageRepository;
    private readonly ILogger<WikiController> _logger;

    public WikiController(
        IPageService pageService,
        IMarkdownService markdownService,
        ICategoryService categoryService,
        IAuthorizationService authorizationService,
        IPageRepository pageRepository,
        ILogger<WikiController> logger)
    {
        _pageService = pageService;
        _markdownService = markdownService;
        _categoryService = categoryService;
        _authorizationService = authorizationService;
        _pageRepository = pageRepository;
        _logger = logger;
    }

    /// <summary>
    /// Displays a wiki page by ID and optional slug
    /// </summary>
    /// <param name="id">The page ID</param>
    /// <param name="slug">The page slug (optional, for SEO-friendly URLs)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The page view</returns>
    [HttpGet("/wiki/{id:int}/{slug?}")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "id" }, VaryByHeader = "Cookie")]
    public async Task<IActionResult> Index(int id, string? slug, bool isHomePage = false, CancellationToken cancellationToken = default)
    {
        if (id < 1)
        {
            return RedirectToAction("Index", "Home");
        }

        try
        {
            var pageDto = await _pageService.GetByIdAsync(id, cancellationToken);
            
            if (pageDto == null)
            {
                _logger.LogWarning("Page with ID {PageId} not found", id);
                return NotFound($"The page with ID {id} could not be found");
            }

            // Check authorization to view this page
            var pageEntity = await _pageRepository.GetByIdAsync(id, cancellationToken);
            if (pageEntity == null || !await _authorizationService.CanViewPageAsync(pageEntity, cancellationToken))
            {
                _logger.LogWarning("User {User} denied access to page {PageId}", 
                    User.Identity?.Name ?? "Anonymous", id);
                
                if (!_authorizationService.IsAuthenticated())
                {
                    // Redirect to login if not authenticated
                    return RedirectToAction("Login", "Account", new { returnUrl = Request.Path });
                }
                
                // Return 403 Forbidden if authenticated but not authorized
                return StatusCode(403, "You do not have permission to view this page");
            }

            // Get the latest content
            var contentDto = await _pageService.GetLatestContentAsync(id, cancellationToken);
            
            if (contentDto == null)
            {
                _logger.LogWarning("No content found for page {PageId}", id);
                return NotFound($"No content found for page {id}");
            }

            // Convert markdown to HTML
            var htmlContent = await _markdownService.ToHtmlAsync(contentDto.Content, cancellationToken);

            // Convert internal slug-only links to full /wiki/{id}/{slug} URLs
            htmlContent = await _markdownService.ConvertInternalLinksAsync(
                htmlContent,
                async (slug) =>
                {
                    var page = await _pageService.GetBySlugAsync(slug, cancellationToken);
                    return page != null ? (page.Id, page.Slug) : (null, null);
                },
                cancellationToken);

            // Check edit and delete permissions for this specific page
            var username = User.Identity?.Name;
            var userRole = User.IsInRole("Admin") ? "Admin" : User.IsInRole("Editor") ? "Editor" : User.IsInRole("Viewer") ? "Viewer" : null;
            
            // Map to view model
            var viewModel = new PageViewModel
            {
                Id = pageDto.Id,
                Title = pageDto.Title,
                Slug = pageDto.Slug,
                Content = contentDto.Content,
                HtmlContent = htmlContent,
                CategoryId = pageDto.CategoryId,
                IsLocked = pageDto.IsLocked,
                CreatedBy = pageDto.CreatedBy,
                CreatedOn = pageDto.CreatedOn,
                ModifiedBy = pageDto.ModifiedBy,
                ModifiedOn = pageDto.ModifiedOn,
                CanEdit = await _authorizationService.CanEditPageAsync(pageDto, username, userRole),
                CanDelete = await _authorizationService.CanDeletePageAsync(pageDto, userRole),
                IsHomePage = isHomePage
            };

            // Get tags
            var tags = await _pageService.GetPageTagsAsync(id, cancellationToken);
            viewModel.Tags = tags.Select(t => t.Name).ToList();
            viewModel.RawTags = string.Join(", ", viewModel.Tags);

            // Get full category path if applicable
            if (pageDto.CategoryId.HasValue)
            {
                var allCategories = await _categoryService.GetAllCategoriesAsync(cancellationToken);
                var categoryDict = allCategories.ToDictionary(c => c.Id);
                
                if (categoryDict.TryGetValue(pageDto.CategoryId.Value, out var category))
                {
                    // Build full path with category objects for clickable breadcrumbs
                    var pathCategories = new List<CategoryDto>();
                    var current = category;
                    
                    while (current != null)
                    {
                        pathCategories.Insert(0, current);
                        if (current.ParentCategoryId.HasValue && categoryDict.ContainsKey(current.ParentCategoryId.Value))
                        {
                            current = categoryDict[current.ParentCategoryId.Value];
                        }
                        else
                        {
                            current = null;
                        }
                    }
                    
                    // Store category path for breadcrumb display
                    viewModel.CategoryPath = pathCategories.Select(c => new CategoryViewModel
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Slug = c.Slug
                    }).ToList();
                    
                    // Also set the display name with backslash separator
                    viewModel.CategoryName = string.Join(" \\ ", pathCategories.Select(c => c.Name));
                }
            }

            // Redirect to correct slug if provided slug doesn't match
            if (!string.IsNullOrEmpty(slug) && !string.Equals(slug, pageDto.Slug, StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Index), new { id = pageDto.Id, slug = pageDto.Slug });
            }

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error displaying page {PageId}", id);
            return StatusCode(500, "An error occurred while loading the page");
        }
    }

    /// <summary>
    /// Displays the page toolbar (for AJAX loading)
    /// </summary>
    /// <param name="id">The page ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Partial view with page toolbar</returns>
    [HttpGet]
    public async Task<IActionResult> PageToolbar(int id, CancellationToken cancellationToken)
    {
        if (id < 1)
        {
            return Content(string.Empty);
        }

        try
        {
            var pageDto = await _pageService.GetByIdAsync(id, cancellationToken);
            
            if (pageDto == null)
            {
                return Content($"The page with ID {id} could not be found");
            }

            var viewModel = new PageViewModel
            {
                Id = pageDto.Id,
                Title = pageDto.Title,
                Slug = pageDto.Slug,
                IsLocked = pageDto.IsLocked,
                CanEdit = true, // TODO: Check user permissions
                CanDelete = false // TODO: Check user permissions
            };

            return PartialView("_PageToolbar", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading page toolbar for page {PageId}", id);
            return Content(string.Empty);
        }
    }

    /// <summary>
    /// 404 Not Found page
    /// </summary>
    [HttpGet("/wiki/notfound")]
    [ResponseCache(Duration = 3600)]
    public new IActionResult NotFound()
    {
        Response.StatusCode = 404;
        return View("404");
    }

    /// <summary>
    /// 500 Server Error page
    /// </summary>
    [HttpGet("/wiki/error")]
    [ResponseCache(Duration = 0, NoStore = true)]
    public IActionResult ServerError()
    {
        Response.StatusCode = 500;
        return View("500");
    }
}
