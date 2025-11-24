using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Services;
using Squirrel.Wiki.Web.Extensions;
using Squirrel.Wiki.Web.Models.Admin;
using Squirrel.Wiki.Web.Services;

namespace Squirrel.Wiki.Web.Controllers;

/// <summary>
/// Controller for category management
/// PHASE 8.3: Category Management UI
/// </summary>
[Authorize(Policy = "RequireEditor")]
public class CategoriesController : BaseController
{
    private readonly ICategoryService _categoryService;
    private readonly ICategoryTreeBuilder _treeBuilder;
    private readonly IPageService _pageService;
    private readonly IUserContext _userContext;

    public CategoriesController(
        ICategoryService categoryService,
        ICategoryTreeBuilder treeBuilder,
        IPageService pageService,
        IUserContext userContext,
        ITimezoneService timezoneService,
        ILogger<CategoriesController> logger,
        INotificationService notifications)
        : base(logger, notifications, timezoneService, null)
    {
        _categoryService = categoryService;
        _treeBuilder = treeBuilder;
        _pageService = pageService;
        _userContext = userContext;
    }

    /// <summary>
    /// Category management index page with tree view
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return await ExecuteAsync(async () =>
        {
            var categories = await _categoryService.GetAllAsync();
            var tree = await BuildCategoryTreeAsync(categories);

            var model = new CategoryViewModel
            {
                Categories = tree,
                MaxCategoryDepth = _categoryService.GetMaxCategoryDepth()
            };

            PopulateBaseViewModel(model);
            return View(model);
        },
        ex =>
        {
            NotifyError($"Error loading categories: {ex.Message}");
            return View(new CategoryViewModel());
        });
    }

    /// <summary>
    /// Show create category form
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create(int? parentId = null)
    {
        return await ExecuteAsync(async () =>
        {
            var model = new EditCategoryViewModel
            {
                ParentId = parentId,
                AvailableParents = await GetAvailableParentsAsync(null)
            };

            if (parentId.HasValue)
            {
                var parent = await _categoryService.GetByIdAsync(parentId.Value);
                if (parent != null)
                {
                    model.CurrentPath = parent.FullPath;
                }
            }

            return View("Edit", model);
        },
        "Error loading form.",
        "Error loading create category form");
    }

    /// <summary>
    /// Show edit category form
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        return await ExecuteAsync(async () =>
        {
            var category = await _categoryService.GetByIdAsync(id);
            if (!ValidateEntityExists(category, "Category"))
                return RedirectToAction(nameof(Index));

            var model = new EditCategoryViewModel
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                ParentId = category.ParentId,
                CurrentPath = category.FullPath,
                AvailableParents = await GetAvailableParentsAsync(id)
            };

            return View(model);
        },
        "Error loading category.",
        $"Error loading edit category form for ID {id}");
    }

    /// <summary>
    /// Create or update a category - Refactored with Result Pattern
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(EditCategoryViewModel model)
    {
        if (!ValidateModelState())
        {
            await ReloadFormDataAsync(model);
            return View("Edit", model);
        }

        // Execute save operation with Result Pattern
        var result = model.IsNew 
            ? await CreateCategoryWithResult(model)
            : await UpdateCategoryWithResult(model);

        // Handle success/failure
        if (result.IsSuccess)
        {
            var action = model.IsNew ? "Created" : "Updated";
            var notificationKey = model.IsNew ? "Notification_CategoryCreated" : "Notification_CategoryUpdated";
            
            _logger.LogInformation("{Action} category '{CategoryName}' (ID: {CategoryId}) by {User}", 
                action, result.Value!.Name, result.Value.Id, _userContext.Username ?? "System");
            
            NotifyLocalizedSuccess(notificationKey, result.Value.Name);
            return RedirectToAction(nameof(Index));
        }
        else
        {
            ModelState.AddModelError("", result.Error!);
            await ReloadFormDataAsync(model);
            return View("Edit", model);
        }
    }

    /// <summary>
    /// Delete a category
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var category = await _categoryService.GetByIdAsync(id);
            if (category == null)
            {
                return Json(new { success = false, message = "Category not found." });
            }

            // Check if category has subcategories
            var allCategories = await _categoryService.GetAllAsync();
            var hasSubcategories = allCategories.Any(c => c.ParentId == id);
            
            if (hasSubcategories)
            {
                return Json(new { success = false, message = "Cannot delete category with subcategories. Please delete or move subcategories first." });
            }

            // Check if category has pages
            var pages = await _pageService.GetByCategoryAsync(id);
            if (pages.Any())
            {
                return Json(new { success = false, message = $"Cannot delete category with {pages.Count()} page(s). Please remove or recategorize pages first." });
            }

            await _categoryService.DeleteAsync(id);
            
            _logger.LogInformation("Deleted category '{CategoryName}' (ID: {CategoryId}) by {User}", 
                category.Name, id, _userContext.Username ?? "System");

            return Json(new { success = true, message = $"Category '{category.Name}' deleted successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting category ID {CategoryId}", id);
            return Json(new { success = false, message = $"Error deleting category: {ex.Message}" });
        }
    }

    /// <summary>
    /// Move a category to a new parent
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move([FromBody] MoveCategoryRequest request)
    {
        try
        {
            var category = await _categoryService.GetByIdAsync(request.CategoryId);
            if (category == null)
            {
                return Json(new { success = false, message = "Category not found." });
            }

            // Validate that we're not moving to a descendant
            if (request.NewParentId.HasValue)
            {
                var newParent = await _categoryService.GetByIdAsync(request.NewParentId.Value);
                if (newParent == null)
                {
                    return Json(new { success = false, message = "Target parent category not found." });
                }

                // Check if new parent is a descendant of the category being moved
                if (await IsDescendantAsync(request.NewParentId.Value, request.CategoryId))
                {
                    return Json(new { success = false, message = "Cannot move category to its own descendant." });
                }
            }

            var updateDto = new CategoryUpdateDto
            {
                Name = category.Name,
                Description = category.Description,
                ParentId = request.NewParentId,
                ModifiedBy = _userContext.Username ?? "System"
            };

            var updated = await _categoryService.UpdateAsync(request.CategoryId, updateDto);
            
            _logger.LogInformation("Moved category '{CategoryName}' (ID: {CategoryId}) to parent {ParentId} by {User}", 
                category.Name, request.CategoryId, request.NewParentId, _userContext.Username ?? "System");

            NotifyLocalizedSuccess("Notification_CategoryMoved", category.Name);
            
            return Json(new { 
                success = true, 
                message = $"Category '{category.Name}' moved successfully.",
                newPath = updated.FullPath
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving category ID {CategoryId}", request.CategoryId);
            return Json(new { success = false, message = $"Error moving category: {ex.Message}" });
        }
    }

    #region Helper Methods - Result Pattern

    /// <summary>
    /// Creates a new category with Result Pattern
    /// Encapsulates category creation logic with validation
    /// </summary>
    private async Task<Result<CategoryDto>> CreateCategoryWithResult(EditCategoryViewModel model)
    {
        try
        {
            var username = _userContext.Username ?? "System";
            
            var createDto = new CategoryCreateDto
            {
                Name = model.Name,
                Description = model.Description,
                ParentId = model.ParentId,
                CreatedBy = username
            };

            var created = await _categoryService.CreateAsync(createDto);
            return Result<CategoryDto>.Success(created);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validation error creating category");
            return Result<CategoryDto>.Failure(ex.Message, "CATEGORY_VALIDATION_ERROR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating category");
            return Result<CategoryDto>.Failure($"Error saving category: {ex.Message}", "CATEGORY_CREATE_ERROR");
        }
    }

    /// <summary>
    /// Updates an existing category with Result Pattern
    /// Encapsulates category update logic with validation
    /// </summary>
    private async Task<Result<CategoryDto>> UpdateCategoryWithResult(EditCategoryViewModel model)
    {
        try
        {
            var username = _userContext.Username ?? "System";
            
            var updateDto = new CategoryUpdateDto
            {
                Name = model.Name,
                Description = model.Description,
                ParentId = model.ParentId,
                ModifiedBy = username
            };

            var updated = await _categoryService.UpdateAsync(model.Id, updateDto);
            return Result<CategoryDto>.Success(updated);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validation error updating category");
            return Result<CategoryDto>.Failure(ex.Message, "CATEGORY_VALIDATION_ERROR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating category");
            return Result<CategoryDto>.Failure($"Error saving category: {ex.Message}", "CATEGORY_UPDATE_ERROR");
        }
    }

    /// <summary>
    /// Reloads form data for the category edit view
    /// Centralizes form data reloading to eliminate duplication
    /// </summary>
    private async Task ReloadFormDataAsync(EditCategoryViewModel model)
    {
        model.AvailableParents = await GetAvailableParentsAsync(model.Id == 0 ? null : model.Id);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Build hierarchical category tree using the tree builder service
    /// </summary>
    private async Task<List<CategoryTreeNode>> BuildCategoryTreeAsync(IEnumerable<CategoryDto> categories)
    {
        return await _treeBuilder.BuildTreeAsync(categories);
    }

    /// <summary>
    /// Get available parent categories (excluding self and descendants) using the tree builder service
    /// Also excludes categories at maximum depth to prevent exceeding depth limit
    /// </summary>
    private async Task<List<CategorySelectItem>> GetAvailableParentsAsync(int? excludeId)
    {
        var allCategories = await _categoryService.GetAllAsync();
        var maxDepth = _categoryService.GetMaxCategoryDepth();
        var items = new List<CategorySelectItem>();

        // Add "None" option for root level
        items.Add(new CategorySelectItem
        {
            Id = 0,
            Name = "(None - Root Level)",
            FullPath = "",
            Level = 0,
            IsDisabled = false
        });

        // Get descendants to exclude if editing
        var excludedIds = new HashSet<int>();
        if (excludeId.HasValue)
        {
            excludedIds.Add(excludeId.Value);
            await AddDescendantsAsync(excludeId.Value, excludedIds, allCategories);
        }

        // Also exclude categories at maximum depth (they can't have children)
        var categoriesAtMaxDepth = allCategories
            .Where(c => c.Level >= maxDepth - 1)
            .Select(c => c.Id)
            .ToHashSet();
        
        foreach (var id in categoriesAtMaxDepth)
        {
            excludedIds.Add(id);
        }

        // Build tree and flatten using the tree builder service
        var tree = await _treeBuilder.BuildTreeAsync(allCategories.Where(c => !excludedIds.Contains(c.Id)));
        var flattenedItems = _treeBuilder.FlattenTreeForSelection(tree, excludedIds);
        
        items.AddRange(flattenedItems);

        return items;
    }

    /// <summary>
    /// Recursively add all descendants to the excluded set
    /// </summary>
    private async Task AddDescendantsAsync(int categoryId, HashSet<int> excludedIds, IEnumerable<CategoryDto> allCategories)
    {
        var children = allCategories.Where(c => c.ParentId == categoryId);
        foreach (var child in children)
        {
            excludedIds.Add(child.Id);
            await AddDescendantsAsync(child.Id, excludedIds, allCategories);
        }
    }

    /// <summary>
    /// Check if a category is a descendant of another
    /// </summary>
    private async Task<bool> IsDescendantAsync(int potentialDescendantId, int ancestorId)
    {
        var category = await _categoryService.GetByIdAsync(potentialDescendantId);
        
        while (category != null && category.ParentId.HasValue)
        {
            if (category.ParentId.Value == ancestorId)
            {
                return true;
            }
            category = await _categoryService.GetByIdAsync(category.ParentId.Value);
        }

        return false;
    }

    #endregion
}
