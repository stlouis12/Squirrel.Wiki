using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Services.Categories;
using Squirrel.Wiki.Core.Services.Configuration;
using Squirrel.Wiki.Core.Services.Content;
using Squirrel.Wiki.Core.Services.Pages;
using Squirrel.Wiki.Web.Extensions;
using Squirrel.Wiki.Web.Filters;
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
    private readonly IAuthorizationService _coreAuthorizationService;
    private readonly Microsoft.AspNetCore.Authorization.IAuthorizationService _authorizationService;
    private readonly IPageRepository _pageRepository;
    private readonly ITimezoneService _timezoneService;
    private readonly ILogger<WikiController> _logger;

    public WikiController(
        IPageService pageService,
        IMarkdownService markdownService,
        ICategoryService categoryService,
        IAuthorizationService coreAuthorizationService,
        Microsoft.AspNetCore.Authorization.IAuthorizationService authorizationService,
        IPageRepository pageRepository,
        ITimezoneService timezoneService,
        ILogger<WikiController> logger)
    {
        _pageService = pageService;
        _markdownService = markdownService;
        _categoryService = categoryService;
        _coreAuthorizationService = coreAuthorizationService;
        _authorizationService = authorizationService;
        _pageRepository = pageRepository;
        _timezoneService = timezoneService;
        _logger = logger;
    }

    /// <summary>
    /// Displays a wiki page by ID and optional slug - Refactored with Result Pattern
    /// </summary>
    /// <param name="id">The page ID</param>
    /// <param name="slug">The page slug (optional, for SEO-friendly URLs)</param>
    /// <param name="isHomePage">Whether this is being displayed as the home page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The page view</returns>
    [HttpGet("/wiki/{id:int}/{slug?}")]
    [DynamicResponseCache(VaryByQueryKeys = new[] { "id" }, VaryByHeader = "Cookie")]
    public async Task<IActionResult> Index(int id, string? slug, bool isHomePage = false, CancellationToken cancellationToken = default)
    {
        if (id < 1)
        {
            return RedirectToAction("Index", "Home");
        }

        var result = await LoadPageWithResult(id, isHomePage, cancellationToken);

        return result.Match<IActionResult>(
            onSuccess: viewModel =>
            {
                // Redirect to correct slug if provided slug doesn't match
                if (!string.IsNullOrEmpty(slug) && !string.Equals(slug, viewModel.Slug, StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction(nameof(Index), new { id = viewModel.Id, slug = viewModel.Slug });
                }

                return View(viewModel);
            },
            onFailure: (error, code) =>
            {
                _logger.LogError("Failed to load page {PageId}: {Error} (Code: {Code})", id, error, code);

                return code switch
                {
                    "PAGE_NOT_FOUND" => NotFound($"The page with ID {id} could not be found"),
                    "PAGE_NO_CONTENT" => NotFound($"No content found for page {id}"),
                    "PAGE_UNAUTHORIZED" => !_coreAuthorizationService.IsAuthenticated()
                        ? RedirectToAction("Login", "Account", new { returnUrl = Request.Path })
                        : StatusCode(403, "You do not have permission to view this page"),
                    _ => StatusCode(500, "An error occurred while loading the page")
                };
            }
        );
    }

    /// <summary>
    /// Displays the page toolbar (for AJAX loading) - Refactored with Result Pattern
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

        var result = await LoadPageToolbarWithResult(id, cancellationToken);

        return result.Match<IActionResult>(
            onSuccess: viewModel => PartialView("_PageToolbar", viewModel),
            onFailure: (error, code) =>
            {
                _logger.LogWarning("Failed to load page toolbar for {PageId}: {Error}", id, error);
                return Content(string.Empty);
            }
        );
    }

    /// <summary>
    /// 404 Not Found page
    /// </summary>
    [HttpGet("/wiki/notfound")]
    [DynamicResponseCache(OverrideDuration = 3600)]
    public new IActionResult NotFound()
    {
        Response.StatusCode = 404;
        return View("404");
    }

    /// <summary>
    /// 500 Server Error page
    /// </summary>
    [HttpGet("/wiki/error")]
    [DynamicResponseCache(NoStore = true)]
    public IActionResult ServerError()
    {
        Response.StatusCode = 500;
        return View("500");
    }

    #region Helper Methods - Result Pattern

    /// <summary>
    /// Loads a wiki page with Result Pattern
    /// Encapsulates all page loading logic including authorization and content rendering
    /// </summary>
    private async Task<Result<PageViewModel>> LoadPageWithResult(
        int id,
        bool isHomePage, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Get and validate page
            var pageDto = await _pageService.GetByIdAsync(id, cancellationToken);
            if (pageDto == null)
            {
                _logger.LogWarning("Page with ID {PageId} not found", id);
                return Result<PageViewModel>.Failure(
                    $"The page with ID {id} could not be found",
                    "PAGE_NOT_FOUND"
                ).WithContext("PageId", id);
            }

            // Check authorization
            var authResult = await CheckPageAuthorizationAsync(id, cancellationToken);
            if (!authResult.IsSuccess)
            {
                return Result<PageViewModel>.Failure(authResult.Error!, authResult.ErrorCode!)
                    .WithContext("PageId", id);
            }
            var pageEntity = authResult.Value!;

            // Get and validate content
            var contentDto = await _pageRepository.GetLatestContentAsync(id, cancellationToken);
            if (contentDto == null)
            {
                _logger.LogWarning("No content found for page {PageId}", id);
                return Result<PageViewModel>.Failure(
                    $"No content found for page {id}",
                    "PAGE_NO_CONTENT"
                ).WithContext("PageId", id);
            }

            // Process content
            var htmlContent = await ProcessPageContentAsync(contentDto.Text, cancellationToken);

            // Get permissions
            var canEdit = await _authorizationService.IsAuthorizedAsync(User, pageEntity, "CanEditPage");
            var canDelete = await _authorizationService.IsAuthorizedAsync(User, pageEntity, "CanDeletePage");

            // Build view model
            var viewModel = BuildPageViewModel(pageDto, contentDto, htmlContent, canEdit, canDelete, isHomePage);

            // Add tags
            await PopulatePageTagsAsync(viewModel, id, cancellationToken);

            // Add category information
            await PopulateCategoryPathAsync(viewModel, pageDto.CategoryId, cancellationToken);

            return Result<PageViewModel>.Success(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading page {PageId}", id);
            return Result<PageViewModel>.Failure(
                "An error occurred while loading the page",
                "PAGE_LOAD_ERROR"
            ).WithContext("PageId", id);
        }
    }

    private async Task<Result<Core.Database.Entities.Page>> CheckPageAuthorizationAsync(
        int id, 
        CancellationToken cancellationToken)
    {
        var pageEntity = await _pageRepository.GetByIdAsync(id, cancellationToken);
        if (pageEntity == null)
        {
            _logger.LogWarning("Page entity with ID {PageId} not found", id);
            return Result<Core.Database.Entities.Page>.Failure(
                $"The page with ID {id} could not be found",
                "PAGE_NOT_FOUND"
            );
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, pageEntity, "CanViewPage");
        if (!authResult.Succeeded)
        {
            var user = User.Identity?.Name ?? "Anonymous";
            _logger.LogWarning("User {User} denied access to page {PageId}", user, id);
            
            return Result<Core.Database.Entities.Page>.Failure(
                "You do not have permission to view this page",
                "PAGE_UNAUTHORIZED"
            );
        }

        return Result<Core.Database.Entities.Page>.Success(pageEntity);
    }

    private async Task<string> ProcessPageContentAsync(string markdownContent, CancellationToken cancellationToken)
    {
        // Convert markdown to HTML
        var htmlContent = await _markdownService.ToHtmlAsync(markdownContent, cancellationToken);

        // Convert internal slug-only links to full /wiki/{id}/{slug} URLs
        htmlContent = await _markdownService.ConvertInternalLinksAsync(
            htmlContent,
            async (linkSlug) =>
            {
                var page = await _pageService.GetBySlugAsync(linkSlug, cancellationToken);
                return page != null ? (page.Id, page.Slug) : (null, null);
            },
            cancellationToken);

        return htmlContent;
    }

    private PageViewModel BuildPageViewModel(
        PageDto pageDto,
        Core.Database.Entities.PageContent contentDto,
        string htmlContent,
        bool canEdit,
        bool canDelete,
        bool isHomePage)
    {
        return new PageViewModel
        {
            Id = pageDto.Id,
            Title = pageDto.Title,
            Slug = pageDto.Slug,
            Content = contentDto.Text,
            HtmlContent = htmlContent,
            CategoryId = pageDto.CategoryId,
            IsLocked = pageDto.IsLocked,
            CreatedBy = pageDto.CreatedBy,
            CreatedOn = pageDto.CreatedOn,
            ModifiedBy = pageDto.ModifiedBy,
            ModifiedOn = pageDto.ModifiedOn,
            CanEdit = canEdit,
            CanDelete = canDelete,
            IsHomePage = isHomePage,
            TimezoneService = _timezoneService
        };
    }

    private async Task PopulatePageTagsAsync(PageViewModel viewModel, int pageId, CancellationToken cancellationToken)
    {
        var tags = await _pageService.GetPageTagsAsync(pageId, cancellationToken);
        viewModel.Tags = tags.Select(t => t.Name).ToList();
        viewModel.RawTags = string.Join(", ", viewModel.Tags);
    }

    private async Task PopulateCategoryPathAsync(
        PageViewModel viewModel, 
        int? categoryId, 
        CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
            return;

        var allCategories = await _categoryService.GetAllCategoriesAsync(cancellationToken);
        var categoryDict = allCategories.ToDictionary(c => c.Id);

        if (!categoryDict.TryGetValue(categoryId.Value, out var category))
            return;

        var pathCategories = BuildCategoryPath(category, categoryDict);

        viewModel.CategoryPath = pathCategories.Select(c => new CategoryViewModel
        {
            Id = c.Id,
            Name = c.Name,
            Slug = c.Slug
        }).ToList();

        viewModel.CategoryName = string.Join(" \\ ", pathCategories.Select(c => c.Name));
    }

    private static List<CategoryDto> BuildCategoryPath(CategoryDto category, Dictionary<int, CategoryDto> categoryDict)
    {
        var pathCategories = new List<CategoryDto>();
        var current = category;

        while (current != null)
        {
            pathCategories.Insert(0, current);
            
            if (current.ParentCategoryId.HasValue && 
                categoryDict.TryGetValue(current.ParentCategoryId.Value, out var parent))
            {
                current = parent;
            }
            else
            {
                current = null;
            }
        }

        return pathCategories;
    }

    /// <summary>
    /// Loads page toolbar with Result Pattern
    /// Used for AJAX loading of page toolbar
    /// </summary>
    private async Task<Result<PageViewModel>> LoadPageToolbarWithResult(int id, CancellationToken cancellationToken)
    {
        try
        {
            var pageDto = await _pageService.GetByIdAsync(id, cancellationToken);
            
            if (pageDto == null)
            {
                return Result<PageViewModel>.Failure(
                    $"The page with ID {id} could not be found",
                    "PAGE_NOT_FOUND"
                ).WithContext("PageId", id);
            }

            // Get page entity for authorization checks
            var pageEntity = await _pageRepository.GetByIdAsync(id, cancellationToken);
            if (pageEntity == null)
            {
                return Result<PageViewModel>.Failure(
                    $"The page with ID {id} could not be found",
                    "PAGE_NOT_FOUND"
                ).WithContext("PageId", id);
            }

            // Check edit and delete permissions using new policy-based authorization
            var canEdit = await _authorizationService.IsAuthorizedAsync(User, pageEntity, "CanEditPage");
            var canDelete = await _authorizationService.IsAuthorizedAsync(User, pageEntity, "CanDeletePage");

            var viewModel = new PageViewModel
            {
                Id = pageDto.Id,
                Title = pageDto.Title,
                Slug = pageDto.Slug,
                IsLocked = pageDto.IsLocked,
                CanEdit = canEdit,
                CanDelete = canDelete
            };

            return Result<PageViewModel>.Success(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading page toolbar for page {PageId}", id);
            return Result<PageViewModel>.Failure(
                "Error loading page toolbar",
                "PAGE_TOOLBAR_ERROR"
            ).WithContext("PageId", id);
        }
    }

    #endregion
}
