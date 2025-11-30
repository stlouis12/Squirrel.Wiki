using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Web.Extensions;
using Squirrel.Wiki.Web.Models;
using Squirrel.Wiki.Web.Filters;
using Squirrel.Wiki.Web.Services;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Squirrel.Wiki.Core.Services.Pages;
using Squirrel.Wiki.Core.Services.Tags;
using Squirrel.Wiki.Core.Services.Categories;
using Squirrel.Wiki.Core.Services.Content;
using Squirrel.Wiki.Core.Services.Search;
using Squirrel.Wiki.Core.Services.Configuration;

namespace Squirrel.Wiki.Web.Controllers;

/// <summary>
/// Controller for page CRUD operations, history, and tag management
/// </summary>
public class PagesController : BaseController
{
    private readonly IPageService _pageService;
    private readonly ITagService _tagService;
    private readonly ICategoryService _categoryService;
    private readonly IMarkdownService _markdownService;
    private readonly ISearchService _searchService;
    private readonly Squirrel.Wiki.Core.Security.IAuthorizationService _coreAuthorizationService;
    private readonly Microsoft.AspNetCore.Authorization.IAuthorizationService _authorizationService;
    private readonly IPageRepository _pageRepository;
    private readonly IPageContentService _pageContent;
    private readonly ISettingsService _settingsService;

    public PagesController(
        IPageService pageService,
        ITagService tagService,
        ICategoryService categoryService,
        IMarkdownService markdownService,
        ISearchService searchService,
        Squirrel.Wiki.Core.Security.IAuthorizationService coreAuthorizationService,
        Microsoft.AspNetCore.Authorization.IAuthorizationService authorizationService,
        IPageRepository pageRepository,
        IPageContentService pageContent,
        ISettingsService settingsService,
        ITimezoneService timezoneService,
        ILogger<PagesController> logger,
        INotificationService notifications)
        : base(logger, notifications, timezoneService, null)
    {
        _pageService = pageService;
        _tagService = tagService;
        _categoryService = categoryService;
        _markdownService = markdownService;
        _searchService = searchService;
        _coreAuthorizationService = coreAuthorizationService;
        _authorizationService = authorizationService;
        _pageRepository = pageRepository;
        _pageContent = pageContent;
        _settingsService = settingsService;
    }

    /// <summary>
    /// Displays a list of all pages, optionally filtered by recent updates, tag, user, or category
    /// </summary>
    /// <param name="recent">If specified, shows only the N most recently updated pages</param>
    /// <param name="tag">If specified, filters pages by tag</param>
    /// <param name="user">If specified, filters pages by author</param>
    /// <param name="categoryId">If specified, filters pages by category ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet]
    [DynamicResponseCache(VaryByQueryKeys = new[] { "recent", "tag", "user", "categoryId" })]
    public async Task<IActionResult> AllPages(int? recent, string? tag, string? user, int? categoryId, CancellationToken cancellationToken)
    {
        try
        {
            IEnumerable<PageDto> allPages;
            string? filterBy = null;
            string? filterValue = null;
            
            // Determine which filter to apply
            if (!string.IsNullOrEmpty(tag))
            {
                // Filter by tag
                tag = Uri.UnescapeDataString(tag);
                allPages = await _pageService.GetByTagAsync(tag, cancellationToken);
                filterBy = "Tag";
                filterValue = tag;
            }
            else if (!string.IsNullOrEmpty(user))
            {
                // Filter by user
                user = Uri.UnescapeDataString(user);
                allPages = await _pageService.GetByAuthorAsync(user, cancellationToken);
                filterBy = "User";
                filterValue = user;
            }
            else if (categoryId.HasValue)
            {
                // Filter by category
                allPages = await _pageService.GetByCategoryAsync(categoryId.Value, cancellationToken);
                var allCategories = await _categoryService.GetAllCategoriesAsync(cancellationToken);
                var category = allCategories.FirstOrDefault(c => c.Id == categoryId.Value);
                filterBy = "Category";
                filterValue = category?.Name ?? $"ID {categoryId.Value}";
            }
            else
            {
                // No filter - get all pages
                allPages = await _pageService.GetAllAsync(cancellationToken);
            }
            
            var categories = await _categoryService.GetAllCategoriesAsync(cancellationToken);
            var categoryDict = categories.ToDictionary(c => c.Id, c => c.Name);
            
            // Filter pages based on authorization using batch check
            var authorizedPages = await FilterAuthorizedPagesAsync(allPages, cancellationToken);
            
            // Apply recent filter if specified (can be combined with other filters)
            IEnumerable<PageDto> filteredPages = authorizedPages;
            if (recent.HasValue && recent.Value > 0)
            {
                filteredPages = authorizedPages
                    .OrderByDescending(p => p.ModifiedOn)
                    .Take(recent.Value);
                
                // Update filter display
                if (filterBy != null)
                {
                    filterBy = $"{filterBy} (Recently Updated)";
                    filterValue = $"{filterValue} - Last {recent.Value} updates";
                }
                else
                {
                    filterBy = "Recently Updated";
                    filterValue = $"Last {recent.Value} updates";
                }
            }
            
            var pageSummaries = new List<PageSummaryViewModel>();
            
            foreach (var page in filteredPages)
            {
                // Get tags for this page
                var pageTags = await _pageService.GetPageTagsAsync(page.Id, cancellationToken);
                
                var summary = new PageSummaryViewModel
                {
                    Id = page.Id,
                    Title = page.Title,
                    Slug = page.Slug,
                    CategoryName = page.CategoryId.HasValue && categoryDict.ContainsKey(page.CategoryId.Value) 
                        ? categoryDict[page.CategoryId.Value] 
                        : null,
                    Tags = pageTags.Select(t => t.Name).ToList(),
                    CreatedBy = page.CreatedBy,
                    CreatedOn = page.CreatedOn,
                    ModifiedBy = page.ModifiedBy,
                    ModifiedOn = page.ModifiedOn,
                    IsLocked = page.IsLocked
                };
                
                pageSummaries.Add(summary);
            }
            
            var viewModel = new PageListViewModel
            {
                FilterBy = filterBy,
                FilterValue = filterValue,
                Pages = pageSummaries
            };

            // Populate timezone service for view
            PopulateBaseViewModel(viewModel);

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading all pages");
            return StatusCode(500, "An error occurred while loading pages");
        }
    }

    /// <summary>
    /// Displays all tags with page counts (filtered by authorization)
    /// </summary>
    [HttpGet]
    [DynamicResponseCache]
    public async Task<IActionResult> AllTags(CancellationToken cancellationToken)
    {
        try
        {
            // Get all tags
            var allTags = await _tagService.GetAllTagsAsync(cancellationToken);
            
            var tagViewModels = new List<TagViewModel>();
            
            // For each tag, count only the pages the user can see
            foreach (var tag in allTags.OrderBy(t => t.Name))
            {
                var tagPages = await _pageService.GetByTagAsync(tag.Name, cancellationToken);
                
                // ✅ Get all page IDs and batch load entities in a single query
                var pageIds = tagPages.Select(p => p.Id).ToList();
                var pageEntities = await _pageRepository.GetByIdsAsync(pageIds, cancellationToken);
                
                // ✅ Batch authorization check
                var authResults = await _coreAuthorizationService.CanViewPagesAsync(pageEntities, cancellationToken);
                var authorizedCount = authResults.Count(r => r.Value);
                
                // Only include tags that have at least one visible page
                if (authorizedCount > 0)
                {
                    tagViewModels.Add(new TagViewModel
                    {
                        Id = tag.Id,
                        Name = tag.Name,
                        PageCount = authorizedCount
                    });
                }
            }

            return View(tagViewModels);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading all tags");
            return StatusCode(500, "An error occurred while loading tags");
        }
    }

    /// <summary>
    /// Returns all tags as JSON for autocomplete
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AllTagsAsJson(string term = "", CancellationToken cancellationToken = default)
    {
        try
        {
            var tags = await _tagService.GetAllTagsAsync(cancellationToken);
            
            if (!string.IsNullOrEmpty(term))
            {
                tags = tags.Where(t => t.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase));
            }

            var tagNames = tags.Select(t => t.Name).ToList();
            return Json(tagNames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading tags for autocomplete");
            return Json(new List<string>());
        }
    }

    /// <summary>
    /// Displays all pages created by a specific user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ByUser(string id, bool encoded = false, CancellationToken cancellationToken = default)
    {
        try
        {
            // Decode username if it was base64 encoded
            string username = encoded ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(id)) : id;
            
            var pages = await _pageService.GetByAuthorAsync(username, cancellationToken);
            
            // Filter pages based on authorization
            var authorizedPages = await FilterAuthorizedPagesAsync(pages, cancellationToken);
            
            var viewModel = new PageListViewModel
            {
                FilterBy = "User",
                FilterValue = username,
                Pages = authorizedPages.Select(p => new PageSummaryViewModel
                {
                    Id = p.Id,
                    Title = p.Title,
                    Slug = p.Slug,
                    CreatedBy = p.CreatedBy,
                    CreatedOn = p.CreatedOn,
                    ModifiedBy = p.ModifiedBy,
                    ModifiedOn = p.ModifiedOn,
                    IsLocked = p.IsLocked
                }).ToList()
            };

            ViewData["Username"] = username;
            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pages by user {Username}", id);
            return StatusCode(500, "An error occurred while loading pages");
        }
    }

    /// <summary>
    /// Displays hierarchical category browser
    /// Note: To view pages in a category, use AllPages action with categoryId parameter
    /// </summary>
    [HttpGet]
    [DynamicResponseCache(VaryByHeader = "Cookie")]
    public async Task<IActionResult> Category(CancellationToken cancellationToken)
    {
        try
        {
            var categoryTree = await _categoryService.GetCategoryTreeAsync(cancellationToken);
            return View("CategoryBrowser", categoryTree);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading category browser");
            return StatusCode(500, "An error occurred while loading categories");
        }
    }

    /// <summary>
    /// Displays the new page form
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> New(string title = "", string tags = "", CancellationToken cancellationToken = default)
    {
        try
        {
            var allTags = await _tagService.GetAllTagsAsync(cancellationToken);
            var allCategories = await _categoryService.GetAllCategoriesAsync(cancellationToken);

            // Load settings
            var defaultTemplate = await _settingsService.GetSettingAsync<string>("SQUIRREL_DEFAULT_PAGE_TEMPLATE", cancellationToken);
            var maxTitleLength = await _settingsService.GetSettingAsync<int?>("SQUIRREL_MAX_PAGE_TITLE_LENGTH", cancellationToken);

            var viewModel = new PageViewModel
            {
                Title = title,
                Content = defaultTemplate ?? string.Empty,
                RawTags = tags,
                AllTags = allTags.Select(t => new TagViewModel { Id = t.Id, Name = t.Name }).ToList(),
                AllCategories = MapCategoriesToViewModel(allCategories),
                MaxTitleLength = maxTitleLength ?? 200
            };

            return View("Edit", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading new page form");
            return StatusCode(500, "An error occurred while loading the page form");
        }
    }

    /// <summary>
    /// Creates a new page - Refactored with Result Pattern
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> New(PageViewModel model, CancellationToken cancellationToken)
    {
        // Ensure RawTags is never null
        model.RawTags ??= string.Empty;
        
        _logger.LogInformation("New page POST - Title: {Title}, RawTags: '{RawTags}'", 
            model.Title, model.RawTags);
        
        // Handle validation errors early
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState is invalid. Errors: {Errors}", 
                string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            
            await ReloadPageFormDataAsync(model, cancellationToken);
            return View("Edit", model);
        }

        // Execute page creation with Result Pattern
        var result = await CreatePageWithResult(model, cancellationToken);
        
        // Use Result Pattern to handle success/failure
        if (result.IsSuccess)
        {
            _logger.LogInformation("Page {PageId} created: {Title}", result.Value!.Id, result.Value.Title);
            return RedirectToAction("Index", "Wiki", new { id = result.Value.Id, slug = result.Value.Slug });
        }
        else
        {
            _logger.LogError("Error creating page: {Error}", result.Error);
            ModelState.AddModelError(string.Empty, result.Error!);
            await ReloadPageFormDataAsync(model, cancellationToken);
            return View("Edit", model);
        }
    }

    /// <summary>
    /// Displays the edit page form
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        try
        {
            var page = await _pageService.GetByIdAsync(id, cancellationToken);
            
            if (page == null)
            {
                return NotFound($"Page with ID {id} not found");
            }

            // Check if page is locked (admin only)
            if (page.IsLocked)
            {
                // TODO: Check if user is admin
                // For now, show a message
                NotifyWarning("This page is locked and can only be edited by administrators.");
            }

            var content = await _pageContent.GetLatestContentAsync(id, cancellationToken);
            var tags = await _pageService.GetPageTagsAsync(id, cancellationToken);
            var allTags = await _tagService.GetAllTagsAsync(cancellationToken);
            var allCategories = await _categoryService.GetAllCategoriesAsync(cancellationToken);

            // Load settings
            var maxTitleLength = await _settingsService.GetSettingAsync<int?>("SQUIRREL_MAX_PAGE_TITLE_LENGTH", cancellationToken);

            // Get full category path if page has a category
            string? categoryFullPath = null;
            if (page.CategoryId.HasValue)
            {
                var mappedCategories = MapCategoriesToViewModel(allCategories);
                var pageCategory = mappedCategories.FirstOrDefault(c => c.Id == page.CategoryId.Value);
                categoryFullPath = pageCategory?.FullPath;
            }

            var viewModel = new PageViewModel
            {
                Id = page.Id,
                Title = page.Title,
                Content = content?.Content ?? string.Empty,
                Slug = page.Slug,
                CategoryId = page.CategoryId,
                CategoryName = categoryFullPath,
                IsLocked = page.IsLocked,
                Visibility = page.Visibility,
                RawTags = string.Join(", ", tags.Select(t => t.Name)),
                Tags = tags.Select(t => t.Name).ToList(),
                CreatedBy = page.CreatedBy,
                CreatedOn = page.CreatedOn,
                ModifiedBy = page.ModifiedBy,
                ModifiedOn = page.ModifiedOn,
                AllTags = allTags.Select(t => new TagViewModel { Id = t.Id, Name = t.Name }).ToList(),
                AllCategories = MapCategoriesToViewModel(allCategories),
                MaxTitleLength = maxTitleLength ?? 200
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading edit form for page {PageId}", id);
            return StatusCode(500, "An error occurred while loading the page");
        }
    }

    /// <summary>
    /// Updates an existing page - Refactored with Result Pattern
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> Edit(PageViewModel model, CancellationToken cancellationToken)
    {
        // Ensure RawTags is never null
        model.RawTags ??= string.Empty;
        
        // Handle validation errors early
        if (!ModelState.IsValid)
        {
            await ReloadPageFormDataAsync(model, cancellationToken);
            return View(model);
        }

        // Execute page update with Result Pattern
        var result = await UpdatePageWithResult(model, cancellationToken);
        
        // Use Result Pattern to handle success/failure
        if (result.IsSuccess)
        {
            _logger.LogInformation("Page {PageId} updated: {Title}", model.Id, model.Title);
            return RedirectToAction("Index", "Wiki", new { id = model.Id, slug = model.Slug });
        }
        else
        {
            _logger.LogError("Error updating page {PageId}: {Error}", model.Id, result.Error);
            ModelState.AddModelError(string.Empty, result.Error!);
            await ReloadPageFormDataAsync(model, cancellationToken);
            return View(model);
        }
    }

    /// <summary>
    /// Deletes a page
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            await _pageService.DeleteAsync(id, cancellationToken);
            
            // Remove from search index
            await _searchService.RemoveFromIndexAsync(id, cancellationToken);

            _logger.LogInformation("Page {PageId} deleted", id);
            
            NotifyLocalizedSuccess("Notification_PageDeleted");
            return RedirectToAction(nameof(AllPages));
        });
    }

    /// <summary>
    /// Gets HTML preview of markdown content (AJAX)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> GetPreview([FromBody] string content, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(content))
            {
                return Content(string.Empty);
            }

            var html = await _markdownService.ToHtmlAsync(content, cancellationToken);
            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating preview");
            return Content("<p>Error generating preview</p>", "text/html");
        }
    }

    /// <summary>
    /// Displays page history
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> History(int id, CancellationToken cancellationToken)
    {
        try
        {
            var page = await _pageService.GetByIdAsync(id, cancellationToken);
            
            if (page == null)
            {
                return NotFound($"Page with ID {id} not found");
            }

            var history = await _pageContent.GetContentHistoryAsync(id, cancellationToken);
            
            var versions = history.Select(h => new PageHistoryViewModel
            {
                VersionId = h.Id,
                PageId = id,
                PageTitle = page.Title,
                VersionNumber = h.Version,  // Use actual database version number
                EditedBy = h.CreatedBy,
                EditedOn = h.CreatedOn,
                ChangeComment = h.ChangeComment
            }).ToList();

            var viewModel = new PageHistoryListViewModel
            {
                Versions = versions,
                PageId = id,
                PageTitle = page.Title
            };

            // Populate timezone service for view
            PopulateBaseViewModel(viewModel);
            
            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading history for page {PageId}", id);
            return StatusCode(500, "An error occurred while loading page history");
        }
    }

    /// <summary>
    /// Displays a specific version of a page
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Version(int id, Guid versionId, CancellationToken cancellationToken)
    {
        try
        {
            var page = await _pageService.GetByIdAsync(id, cancellationToken);
            if (page == null)
            {
                return NotFound($"Page with ID {id} not found");
            }

            // Get all versions to find the version number
            var history = await _pageContent.GetContentHistoryAsync(id, cancellationToken);
            var versionContent = history.FirstOrDefault(h => h.Id == versionId);
            
            if (versionContent == null)
            {
                return NotFound($"Version {versionId} not found for page {id}");
            }

            // Render the content
            var renderedContent = await _markdownService.ToHtmlAsync(versionContent.Content, cancellationToken);

            var viewModel = new PageViewModel
            {
                Id = page.Id,
                Title = page.Title,
                Content = versionContent.Content,
                HtmlContent = renderedContent,
                Slug = page.Slug,
                CategoryId = page.CategoryId,
                IsLocked = page.IsLocked,
                CreatedBy = page.CreatedBy,
                CreatedOn = page.CreatedOn,
                ModifiedBy = versionContent.CreatedBy,
                ModifiedOn = versionContent.CreatedOn
            };

            ViewData["VersionNumber"] = versionContent.Version;
            ViewData["IsHistoricalVersion"] = true;
            
            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading version {VersionId} for page {PageId}", versionId, id);
            return StatusCode(500, "An error occurred while loading the page version");
        }
    }

    /// <summary>
    /// Compares two versions of a page
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Compare(int id, Guid version1, Guid version2, CancellationToken cancellationToken)
    {
        try
        {
            var page = await _pageService.GetByIdAsync(id, cancellationToken);
            if (page == null)
            {
                return NotFound($"Page with ID {id} not found");
            }

            var history = await _pageContent.GetContentHistoryAsync(id, cancellationToken);
            var content1 = history.FirstOrDefault(h => h.Id == version1);
            var content2 = history.FirstOrDefault(h => h.Id == version2);

            if (content1 == null || content2 == null)
            {
                return NotFound("One or both versions not found");
            }

            // Use DiffPlex to generate a side-by-side diff
            // Trim content to remove excessive whitespace
            var diffBuilder = new SideBySideDiffBuilder(new Differ());
            var diff = diffBuilder.BuildDiffModel(
                (content1.Content ?? ""), 
                (content2.Content ?? ""));

            ViewData["PageTitle"] = page.Title;
            ViewData["PageId"] = id;
            ViewData["Version1Number"] = content1.Version;
            ViewData["Version2Number"] = content2.Version;
            ViewData["Version1Id"] = version1;
            ViewData["Version2Id"] = version2;
            ViewData["Diff"] = diff;

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing versions for page {PageId}", id);
            return StatusCode(500, "An error occurred while comparing versions");
        }
    }

    /// <summary>
    /// Reverts a page to a previous version
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> Revert(int id, int versionNumber, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Revert action called - PageId: {PageId}, VersionNumber: {VersionNumber}", id, versionNumber);
        
        return await ExecuteAsync(async () =>
        {
            var username = User.Identity?.Name ?? "Anonymous";
            
            _logger.LogInformation("Calling RevertToVersionAsync for page {PageId} to version {Version}", id, versionNumber);
            var result = await _pageContent.RevertToVersionAsync(id, versionNumber, username, cancellationToken);
            _logger.LogInformation("RevertToVersionAsync completed successfully. New version: {NewVersion}", result.Version);

            // Update search index
            await _searchService.IndexPageAsync(id, cancellationToken);

            _logger.LogInformation("Page {PageId} reverted to version {Version} by {Username}", id, versionNumber, username);
            
            NotifyLocalizedSuccess("Notification_PageReverted", versionNumber.ToString());
            return RedirectToAction("Index", "Wiki", new { id });
        });
    }

    #region Helper Methods - Result Pattern

    /// <summary>
    /// Creates a new page with Result Pattern
    /// Encapsulates page creation logic including search indexing
    /// </summary>
    private async Task<Result<PageDto>> CreatePageWithResult(PageViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var createDto = new PageCreateDto
            {
                Title = model.Title,
                Content = model.Content,
                Slug = model.Slug,
                CategoryId = model.CategoryId,
                Visibility = model.Visibility,
                Tags = string.IsNullOrWhiteSpace(model.RawTags) 
                    ? new List<string>() 
                    : model.RawTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                IsLocked = model.IsLocked
            };

            var username = User.Identity?.Name ?? "Anonymous";
            var createdPage = await _pageService.CreateAsync(createDto, username, cancellationToken);

            // Index the page for search
            await _searchService.IndexPageAsync(createdPage.Id, cancellationToken);

            return Result<PageDto>.Success(createdPage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new page");
            return Result<PageDto>.Failure("An error occurred while creating the page", "PAGE_CREATE_ERROR");
        }
    }

    /// <summary>
    /// Updates an existing page with Result Pattern
    /// Encapsulates page update logic including search indexing
    /// </summary>
    private async Task<Result> UpdatePageWithResult(PageViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var updateDto = new PageUpdateDto
            {
                Title = model.Title,
                Content = model.Content,
                Slug = model.Slug,
                CategoryId = model.CategoryId,
                Visibility = model.Visibility,
                Tags = string.IsNullOrWhiteSpace(model.RawTags) 
                    ? new List<string>() 
                    : model.RawTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                IsLocked = model.IsLocked
                // Note: ChangeComment is not captured in the view model, could be added if needed
            };

            var username = User.Identity?.Name ?? "Anonymous";
            await _pageService.UpdateAsync(model.Id, updateDto, username, cancellationToken);

            // Update search index
            await _searchService.IndexPageAsync(model.Id, cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating page {PageId}", model.Id);
            return Result.Failure("An error occurred while updating the page", "PAGE_UPDATE_ERROR");
        }
    }

    /// <summary>
    /// Reloads dropdown data for page form (tags and categories)
    /// Used when returning to form after validation errors
    /// </summary>
    private async Task ReloadPageFormDataAsync(PageViewModel model, CancellationToken cancellationToken)
    {
        var allTags = await _tagService.GetAllTagsAsync(cancellationToken);
        var allCategories = await _categoryService.GetAllCategoriesAsync(cancellationToken);
        model.AllTags = allTags.Select(t => new TagViewModel { Id = t.Id, Name = t.Name }).ToList();
        model.AllCategories = MapCategoriesToViewModel(allCategories);
    }

    #endregion

    #region Helper Methods - Authorization & Mapping

    /// <summary>
    /// Filters a list of pages to only include those the current user is authorized to view
    /// Uses batch authorization checking for better performance
    /// </summary>
    private async Task<List<PageDto>> FilterAuthorizedPagesAsync(IEnumerable<PageDto> pages, CancellationToken cancellationToken)
    {
        var pagesList = pages.ToList();
        if (!pagesList.Any())
        {
            return new List<PageDto>();
        }
        
        // ✅ Batch load all page entities in a single query
        var pageIds = pagesList.Select(p => p.Id).ToList();
        var pageEntities = await _pageRepository.GetByIdsAsync(pageIds, cancellationToken);
        
        // Create lookup dictionary
        var pageIdToDto = pagesList.ToDictionary(p => p.Id);
        
        // ✅ Batch authorization check
        var authResults = await _coreAuthorizationService.CanViewPagesAsync(pageEntities, cancellationToken);
        
        // Filter to only authorized pages
        var authorizedPages = new List<PageDto>();
        foreach (var result in authResults.Where(r => r.Value))
        {
            if (pageIdToDto.ContainsKey(result.Key))
            {
                authorizedPages.Add(pageIdToDto[result.Key]);
            }
        }
        
        return authorizedPages;
    }

    /// <summary>
    /// Maps category DTOs to view models with hierarchy information
    /// </summary>
    private List<CategoryViewModel> MapCategoriesToViewModel(IEnumerable<CategoryDto> categories)
    {
        var categoryList = categories.ToList();
        var result = new List<CategoryViewModel>();
        
        // Build a dictionary for quick parent lookup
        var categoryDict = categoryList.ToDictionary(c => c.Id);
        
        foreach (var category in categoryList)
        {
            // Calculate level and full path
            var level = 0;
            var pathParts = new List<string>();
            var current = category;
            
            while (current != null)
            {
                pathParts.Insert(0, current.Name);
                if (current.ParentCategoryId.HasValue && categoryDict.ContainsKey(current.ParentCategoryId.Value))
                {
                    current = categoryDict[current.ParentCategoryId.Value];
                    level++;
                }
                else
                {
                    current = null;
                }
            }
            
            result.Add(new CategoryViewModel
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                Description = category.Description,
                ParentCategoryId = category.ParentCategoryId,
                Level = level,
                FullPath = string.Join(" \\ ", pathParts)
            });
        }
        
        return result;
    }

    #endregion
}
