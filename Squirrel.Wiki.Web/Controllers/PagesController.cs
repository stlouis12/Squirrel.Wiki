using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Services;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Web.Models;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace Squirrel.Wiki.Web.Controllers;

/// <summary>
/// Controller for page CRUD operations, history, and tag management
/// </summary>
public class PagesController : Controller
{
    private readonly IPageService _pageService;
    private readonly ITagService _tagService;
    private readonly ICategoryService _categoryService;
    private readonly IMarkdownService _markdownService;
    private readonly ISearchService _searchService;
    private readonly ILogger<PagesController> _logger;

    public PagesController(
        IPageService pageService,
        ITagService tagService,
        ICategoryService categoryService,
        IMarkdownService markdownService,
        ISearchService searchService,
        ILogger<PagesController> logger)
    {
        _pageService = pageService;
        _tagService = tagService;
        _categoryService = categoryService;
        _markdownService = markdownService;
        _searchService = searchService;
        _logger = logger;
    }

    /// <summary>
    /// Displays a list of all pages
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 300)]
    public async Task<IActionResult> AllPages(CancellationToken cancellationToken)
    {
        try
        {
            var pages = await _pageService.GetAllPagesAsync(cancellationToken);
            var categories = await _categoryService.GetAllCategoriesAsync(cancellationToken);
            var categoryDict = categories.ToDictionary(c => c.Id, c => c.Name);
            
            var pageSummaries = new List<PageSummaryViewModel>();
            
            foreach (var page in pages)
            {
                // Get tags for this page
                var tags = await _pageService.GetPageTagsAsync(page.Id, cancellationToken);
                
                var summary = new PageSummaryViewModel
                {
                    Id = page.Id,
                    Title = page.Title,
                    Slug = page.Slug,
                    CategoryName = page.CategoryId.HasValue && categoryDict.ContainsKey(page.CategoryId.Value) 
                        ? categoryDict[page.CategoryId.Value] 
                        : null,
                    Tags = tags.Select(t => t.Name).ToList(),
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
                Pages = pageSummaries
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading all pages");
            return StatusCode(500, "An error occurred while loading pages");
        }
    }

    /// <summary>
    /// Displays all tags with page counts
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 300)]
    public async Task<IActionResult> AllTags(CancellationToken cancellationToken)
    {
        try
        {
            var tagsWithCounts = await _tagService.GetAllWithCountsAsync(cancellationToken);
            
            // Filter to only show tags that have at least one page
            // Group by normalized name to handle any duplicate tags in the database
            var viewModel = tagsWithCounts
                .Where(t => t.PageCount > 0) // Only show tags with pages
                .GroupBy(t => t.Name.ToLowerInvariant())
                .Select(g => g.First()) // Take the first tag from each group
                .Select(t => new TagViewModel
                {
                    Id = t.Id,
                    Name = t.Name,
                    PageCount = t.PageCount
                })
                .OrderBy(t => t.Name)
                .ToList();

            return View(viewModel);
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
            
            var pages = await _pageService.GetPagesByAuthorAsync(username, cancellationToken);
            
            var viewModel = new PageListViewModel
            {
                FilterBy = "User",
                FilterValue = username,
                Pages = pages.Select(p => new PageSummaryViewModel
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
    /// Displays all pages in a specific category, or shows hierarchical category browser if no parameters
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Category(int? id, string? categoryName, CancellationToken cancellationToken)
    {
        try
        {
            var categories = await _categoryService.GetAllCategoriesAsync(cancellationToken);
            
            // If no parameters provided, show hierarchical category browser
            if (!id.HasValue && string.IsNullOrEmpty(categoryName))
            {
                var categoryTree = await _categoryService.GetCategoryTreeAsync(cancellationToken);
                return View("CategoryBrowser", categoryTree);
            }
            
            CategoryDto? category = null;
            
            // Prefer ID lookup if provided
            if (id.HasValue)
            {
                category = categories.FirstOrDefault(c => c.Id == id.Value);
            }
            // Fall back to name lookup
            else if (!string.IsNullOrEmpty(categoryName))
            {
                categoryName = Uri.UnescapeDataString(categoryName);
                category = categories.FirstOrDefault(c => 
                    c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase) ||
                    c.Slug.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
            }
            
            if (category == null)
            {
                var identifier = id.HasValue ? $"ID {id.Value}" : $"'{categoryName}'";
                return NotFound($"Category {identifier} not found");
            }
            
            var pages = await _pageService.GetByCategoryAsync(category.Id, cancellationToken);
            
            var viewModel = new PageListViewModel
            {
                FilterBy = "Category",
                FilterValue = category.Name,
                Pages = pages.Select(p => new PageSummaryViewModel
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

            ViewData["CategoryName"] = category.Name;
            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pages by category {CategoryId}/{CategoryName}", id, categoryName);
            return StatusCode(500, "An error occurred while loading pages");
        }
    }

    /// <summary>
    /// Displays all pages with a specific tag
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Tag(string tagName, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(tagName))
            {
                return BadRequest("Tag name is required");
            }
            
            tagName = Uri.UnescapeDataString(tagName);
            var pages = await _pageService.GetPagesByTagAsync(tagName, cancellationToken);
            
            var viewModel = new PageListViewModel
            {
                FilterBy = "Tag",
                FilterValue = tagName,
                Pages = pages.Select(p => new PageSummaryViewModel
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

            ViewData["Tagname"] = tagName;
            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pages by tag {Tag}", tagName);
            return StatusCode(500, "An error occurred while loading pages");
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

            var viewModel = new PageViewModel
            {
                Title = title,
                RawTags = tags,
                AllTags = allTags.Select(t => new TagViewModel { Id = t.Id, Name = t.Name }).ToList(),
                AllCategories = MapCategoriesToViewModel(allCategories)
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
    /// Creates a new page
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> New(PageViewModel model, CancellationToken cancellationToken)
    {
        // Ensure RawTags is never null
        model.RawTags ??= string.Empty;
        
        _logger.LogInformation("New page POST - Title: {Title}, RawTags: '{RawTags}', RawTags IsNull: {IsNull}", 
            model.Title, model.RawTags, model.RawTags == null);
        
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState is invalid. Errors: {Errors}", 
                string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            
            // Reload dropdown data
            var allTags = await _tagService.GetAllTagsAsync(cancellationToken);
            var allCategories = await _categoryService.GetAllCategoriesAsync(cancellationToken);
            model.AllTags = allTags.Select(t => new TagViewModel { Id = t.Id, Name = t.Name }).ToList();
            model.AllCategories = MapCategoriesToViewModel(allCategories);
            
            return View("Edit", model);
        }

        try
        {
            var createDto = new CreatePageDto
            {
                Title = model.Title,
                Content = model.Content,
                CategoryId = model.CategoryId,
                Tags = string.IsNullOrWhiteSpace(model.RawTags) 
                    ? new List<string>() 
                    : model.RawTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                CreatedBy = User.Identity?.Name ?? "Anonymous" // TODO: Get from authenticated user
            };

            var createdPage = await _pageService.CreatePageAsync(createDto, cancellationToken);

            // Index the page for search
            await _searchService.IndexPageAsync(createdPage.Id, cancellationToken);

            _logger.LogInformation("Page {PageId} created: {Title}", createdPage.Id, createdPage.Title);
            
            return RedirectToAction("Index", "Wiki", new { id = createdPage.Id, slug = createdPage.Slug });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new page");
            ModelState.AddModelError(string.Empty, "An error occurred while creating the page");
            
            // Reload dropdown data
            var allTags = await _tagService.GetAllTagsAsync(cancellationToken);
            var allCategories = await _categoryService.GetAllCategoriesAsync(cancellationToken);
            model.AllTags = allTags.Select(t => new TagViewModel { Id = t.Id, Name = t.Name }).ToList();
            model.AllCategories = MapCategoriesToViewModel(allCategories);
            
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
                TempData["Warning"] = "This page is locked and can only be edited by administrators.";
            }

            var content = await _pageService.GetLatestContentAsync(id, cancellationToken);
            var tags = await _pageService.GetPageTagsAsync(id, cancellationToken);
            var allTags = await _tagService.GetAllTagsAsync(cancellationToken);
            var allCategories = await _categoryService.GetAllCategoriesAsync(cancellationToken);

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
                RawTags = string.Join(", ", tags.Select(t => t.Name)),
                Tags = tags.Select(t => t.Name).ToList(),
                CreatedBy = page.CreatedBy,
                CreatedOn = page.CreatedOn,
                ModifiedBy = page.ModifiedBy,
                ModifiedOn = page.ModifiedOn,
                AllTags = allTags.Select(t => new TagViewModel { Id = t.Id, Name = t.Name }).ToList(),
                AllCategories = MapCategoriesToViewModel(allCategories)
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
    /// Updates an existing page
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> Edit(PageViewModel model, CancellationToken cancellationToken)
    {
        // Ensure RawTags is never null
        model.RawTags ??= string.Empty;
        
        if (!ModelState.IsValid)
        {
            // Reload dropdown data
            var allTags = await _tagService.GetAllTagsAsync(cancellationToken);
            var allCategories = await _categoryService.GetAllCategoriesAsync(cancellationToken);
            model.AllTags = allTags.Select(t => new TagViewModel { Id = t.Id, Name = t.Name }).ToList();
            model.AllCategories = MapCategoriesToViewModel(allCategories);
            
            return View(model);
        }

        try
        {
            var updateDto = new UpdatePageDto
            {
                Id = model.Id,
                Title = model.Title,
                Content = model.Content,
                CategoryId = model.CategoryId,
                Tags = string.IsNullOrWhiteSpace(model.RawTags) 
                    ? new List<string>() 
                    : model.RawTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                ModifiedBy = User.Identity?.Name ?? "Anonymous" // TODO: Get from authenticated user
            };

            await _pageService.UpdatePageAsync(updateDto, cancellationToken);

            // Update search index
            await _searchService.IndexPageAsync(model.Id, cancellationToken);

            _logger.LogInformation("Page {PageId} updated: {Title}", model.Id, model.Title);
            
            return RedirectToAction("Index", "Wiki", new { id = model.Id, slug = model.Slug });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating page {PageId}", model.Id);
            ModelState.AddModelError(string.Empty, "An error occurred while updating the page");
            
            // Reload dropdown data
            var allTags = await _tagService.GetAllTagsAsync(cancellationToken);
            var allCategories = await _categoryService.GetAllCategoriesAsync(cancellationToken);
            model.AllTags = allTags.Select(t => new TagViewModel { Id = t.Id, Name = t.Name }).ToList();
            model.AllCategories = MapCategoriesToViewModel(allCategories);
            
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
        try
        {
            await _pageService.DeletePageAsync(id, cancellationToken);
            
            // Remove from search index
            await _searchService.RemoveFromIndexAsync(id, cancellationToken);

            _logger.LogInformation("Page {PageId} deleted", id);
            
            TempData["Success"] = "Page deleted successfully";
            return RedirectToAction(nameof(AllPages));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting page {PageId}", id);
            TempData["Error"] = "An error occurred while deleting the page";
            return RedirectToAction(nameof(AllPages));
        }
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
    /// Displays recently updated pages
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 60)]
    public async Task<IActionResult> RecentlyUpdated(CancellationToken cancellationToken)
    {
        try
        {
            var pages = await _pageService.GetAllPagesAsync(cancellationToken);
            var categories = await _categoryService.GetAllCategoriesAsync(cancellationToken);
            var categoryDict = categories.ToDictionary(c => c.Id, c => c.Name);
            
            var recentPages = pages
                .OrderByDescending(p => p.ModifiedOn)
                .Take(10)
                .ToList();
            
            var pageSummaries = new List<PageSummaryViewModel>();
            
            foreach (var page in recentPages)
            {
                // Get tags for this page
                var tags = await _pageService.GetPageTagsAsync(page.Id, cancellationToken);
                
                var summary = new PageSummaryViewModel
                {
                    Id = page.Id,
                    Title = page.Title,
                    Slug = page.Slug,
                    CategoryName = page.CategoryId.HasValue && categoryDict.ContainsKey(page.CategoryId.Value) 
                        ? categoryDict[page.CategoryId.Value] 
                        : null,
                    Tags = tags.Select(t => t.Name).ToList(),
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
                FilterBy = "Recently Updated",
                FilterValue = "Last 10 updates",
                Pages = pageSummaries
            };

            return View("AllPages", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading recently updated pages");
            return StatusCode(500, "An error occurred while loading pages");
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

            var history = await _pageService.GetPageHistoryAsync(id, cancellationToken);
            
            var viewModel = history.Select(h => new PageHistoryViewModel
            {
                VersionId = h.Id,
                PageId = id,
                PageTitle = page.Title,
                VersionNumber = h.Version,  // Use actual database version number
                EditedBy = h.CreatedBy,
                EditedOn = h.CreatedOn,
                ChangeComment = h.ChangeComment
            }).ToList();

            ViewData["PageId"] = id;
            ViewData["PageTitle"] = page.Title;
            
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
            var history = await _pageService.GetPageHistoryAsync(id, cancellationToken);
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

            var history = await _pageService.GetPageHistoryAsync(id, cancellationToken);
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
        
        try
        {
            var username = User.Identity?.Name ?? "Anonymous";
            
            _logger.LogInformation("Calling RevertToVersionAsync for page {PageId} to version {Version}", id, versionNumber);
            var result = await _pageService.RevertToVersionAsync(id, versionNumber, username, cancellationToken);
            _logger.LogInformation("RevertToVersionAsync completed successfully. New version: {NewVersion}", result.Version);

            // Update search index
            await _searchService.IndexPageAsync(id, cancellationToken);

            _logger.LogInformation("Page {PageId} reverted to version {Version} by {Username}", id, versionNumber, username);
            
            TempData["Success"] = $"Page successfully reverted to version {versionNumber}. A new version has been created.";
            return RedirectToAction("Index", "Wiki", new { id });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Version not found when reverting page {PageId} to version {Version}", id, versionNumber);
            TempData["Error"] = $"Version {versionNumber} was not found for this page";
            return RedirectToAction(nameof(History), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reverting page {PageId} to version {Version}. Exception: {Message}", id, versionNumber, ex.Message);
            TempData["Error"] = $"An error occurred while reverting the page: {ex.Message}";
            return RedirectToAction(nameof(History), new { id });
        }
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
}
