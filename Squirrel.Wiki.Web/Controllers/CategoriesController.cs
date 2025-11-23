using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Services;
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
    private readonly IPageService _pageService;
    private readonly IUserContext _userContext;

    public CategoriesController(
        ICategoryService categoryService,
        IPageService pageService,
        IUserContext userContext,
        ILogger<CategoriesController> logger,
        INotificationService notifications)
        : base(logger, notifications)
    {
        _categoryService = categoryService;
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
                SuccessMessage = TempData["SuccessMessage"] as string,
                ErrorMessage = TempData["ErrorMessage"] as string
            };

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
    /// Create or update a category
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(EditCategoryViewModel model)
    {
        if (!ValidateModelState())
        {
            model.AvailableParents = await GetAvailableParentsAsync(model.Id == 0 ? null : model.Id);
            return View("Edit", model);
        }

        return await ExecuteAsync(async () =>
        {
            var username = _userContext.Username ?? "System";

            if (model.IsNew)
            {
                // Create new category
                var createDto = new CategoryCreateDto
                {
                    Name = model.Name,
                    Description = model.Description,
                    ParentId = model.ParentId,
                    CreatedBy = username
                };

                var created = await _categoryService.CreateAsync(createDto);
                
                _logger.LogInformation("Created category '{CategoryName}' (ID: {CategoryId}) by {User}", 
                    created.Name, created.Id, username);
                
                NotifyLocalizedSuccess("Notification_CategoryCreated", created.Name);
            }
            else
            {
                // Update existing category
                var updateDto = new CategoryUpdateDto
                {
                    Name = model.Name,
                    Description = model.Description,
                    ParentId = model.ParentId,
                    ModifiedBy = username
                };

                var updated = await _categoryService.UpdateAsync(model.Id, updateDto);
                
                _logger.LogInformation("Updated category '{CategoryName}' (ID: {CategoryId}) by {User}", 
                    updated.Name, updated.Id, username);
                
                NotifyLocalizedSuccess("Notification_CategoryUpdated", updated.Name);
            }

            return RedirectToAction(nameof(Index));
        },
        ex =>
        {
            if (ex is InvalidOperationException)
            {
                _logger.LogWarning(ex, "Validation error saving category");
                ModelState.AddModelError("", ex.Message);
            }
            else
            {
                _logger.LogError(ex, "Error saving category");
                ModelState.AddModelError("", $"Error saving category: {ex.Message}");
            }
            model.AvailableParents = GetAvailableParentsAsync(model.Id == 0 ? null : model.Id).Result;
            return View("Edit", model);
        });
    }

    /// <summary>
    /// Show category details
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        return await ExecuteAsync(async () =>
        {
            var category = await _categoryService.GetByIdAsync(id);
            if (!ValidateEntityExists(category, "Category"))
                return RedirectToAction(nameof(Index));

            // Get subcategories
            var allCategories = await _categoryService.GetAllAsync();
            var subcategories = allCategories
                .Where(c => c.ParentId == id)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name);

            // Get pages in this category
            var pages = await _pageService.GetByCategoryAsync(id);

            // Build breadcrumbs
            var breadcrumbs = new List<string>();
            var current = category;
            while (current != null)
            {
                breadcrumbs.Insert(0, current.Name);
                if (current.ParentId.HasValue)
                {
                    current = await _categoryService.GetByIdAsync(current.ParentId.Value);
                }
                else
                {
                    break;
                }
            }

            var model = new CategoryDetailsViewModel
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                Description = category.Description,
                FullPath = category.FullPath,
                ParentId = category.ParentId,
                Level = category.Level,
                PageCount = category.PageCount,
                DirectPageCount = pages.Count(),
                SubcategoryCount = subcategories.Count(),
                CreatedOn = category.CreatedOn,
                CreatedBy = category.CreatedBy,
                ModifiedOn = category.ModifiedOn,
                ModifiedBy = category.ModifiedBy,
                Breadcrumbs = breadcrumbs,
                Subcategories = await BuildCategoryTreeAsync(subcategories),
                Pages = pages.Select(p => new CategoryPageItem
                {
                    Id = p.Id,
                    Title = p.Title,
                    Slug = p.Slug,
                    ModifiedOn = p.ModifiedOn,
                    ModifiedBy = p.ModifiedBy
                }).ToList()
            };

            if (category.ParentId.HasValue)
            {
                var parent = await _categoryService.GetByIdAsync(category.ParentId.Value);
                model.ParentName = parent?.Name;
            }

            return View(model);
        },
        "Error loading category.",
        $"Error loading category details for ID {id}");
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

    #region Helper Methods

    /// <summary>
    /// Build hierarchical category tree
    /// </summary>
    private async Task<List<CategoryTreeNode>> BuildCategoryTreeAsync(IEnumerable<CategoryDto> categories)
    {
        var categoryList = categories.ToList();
        var tree = new List<CategoryTreeNode>();
        var lookup = new Dictionary<int, CategoryTreeNode>();

        // Create nodes
        foreach (var category in categoryList)
        {
            var node = new CategoryTreeNode
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                Description = category.Description,
                FullPath = category.FullPath,
                ParentId = category.ParentId,
                Level = category.Level,
                PageCount = category.PageCount,
                CreatedOn = category.CreatedOn,
                ModifiedOn = category.ModifiedOn,
                ModifiedBy = category.ModifiedBy,
                Children = new List<CategoryTreeNode>()
            };

            lookup[category.Id] = node;
        }

        // Build tree structure
        foreach (var category in categoryList)
        {
            var node = lookup[category.Id];
            
            if (category.ParentId.HasValue && lookup.ContainsKey(category.ParentId.Value))
            {
                lookup[category.ParentId.Value].Children.Add(node);
            }
            else
            {
                tree.Add(node);
            }
        }

        // Calculate subcategory counts
        foreach (var node in lookup.Values)
        {
            node.SubcategoryCount = node.Children.Count;
        }

        // Sort at each level
        SortTreeNodes(tree);

        return tree;
    }

    /// <summary>
    /// Recursively sort tree nodes
    /// </summary>
    private void SortTreeNodes(List<CategoryTreeNode> nodes)
    {
        nodes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        
        foreach (var node in nodes)
        {
            if (node.Children.Any())
            {
                SortTreeNodes(node.Children);
            }
        }
    }

    /// <summary>
    /// Get available parent categories (excluding self and descendants)
    /// </summary>
    private async Task<List<CategorySelectItem>> GetAvailableParentsAsync(int? excludeId)
    {
        var allCategories = await _categoryService.GetAllAsync();
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

        // Build flat list with indentation
        var tree = await BuildCategoryTreeAsync(allCategories.Where(c => !excludedIds.Contains(c.Id)));
        AddTreeToSelectList(tree, items, excludedIds);

        return items;
    }

    /// <summary>
    /// Recursively add tree nodes to select list
    /// </summary>
    private void AddTreeToSelectList(List<CategoryTreeNode> nodes, List<CategorySelectItem> items, HashSet<int> excludedIds, string prefix = "")
    {
        foreach (var node in nodes)
        {
            items.Add(new CategorySelectItem
            {
                Id = node.Id,
                Name = prefix + node.Name,
                FullPath = node.FullPath,
                Level = node.Level,
                IsDisabled = excludedIds.Contains(node.Id)
            });

            if (node.Children.Any())
            {
                AddTreeToSelectList(node.Children, items, excludedIds, prefix + "  ");
            }
        }
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
