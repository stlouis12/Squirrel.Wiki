using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Services;
using Squirrel.Wiki.Web.Extensions;
using Squirrel.Wiki.Web.Models.Admin;
using Squirrel.Wiki.Web.Services;

namespace Squirrel.Wiki.Web.Controllers;

/// <summary>
/// Controller for managing menus
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class MenusController : BaseController
{
    private readonly IMenuService _menuService;
    private readonly IUserContext _userContext;

    public MenusController(
        IMenuService menuService,
        IUserContext userContext,
        ITimezoneService timezoneService,
        ILogger<MenusController> logger,
        INotificationService notifications)
        : base(logger, notifications, timezoneService, null)
    {
        _menuService = menuService;
        _userContext = userContext;
    }

    /// <summary>
    /// List all menus
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return await ExecuteAsync(async () =>
        {
            var model = new MenuViewModel();
            var menus = await _menuService.GetAllAsync();

            model.Menus = menus.Select(m => new MenuListItem
            {
                Id = m.Id,
                Name = m.Name,
                MenuType = ((MenuType)m.MenuType).ToString(),
                Description = m.Description ?? string.Empty,
                ItemCount = CountMenuItems(m.MenuMarkup),
                IsEnabled = m.IsEnabled,
                ModifiedOn = m.ModifiedOn,
                ModifiedBy = m.ModifiedBy ?? "System"
            }).ToList();

            PopulateBaseViewModel(model);
            return View(model);
        });
    }

    /// <summary>
    /// Create new menu
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        var model = new EditMenuViewModel
        {
            MenuType = (int)MenuType.MainNavigation,
            MenuMarkup = GetDefaultMenuMarkup(),
            IsEnabled = false
        };

        PopulateMenuTypes(model);
        return View("Edit", model);
    }

    /// <summary>
    /// Edit existing menu
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        return await ExecuteAsync(async () =>
        {
            var menu = await _menuService.GetByIdAsync(id);
            if (menu == null)
            {
                return NotFound();
            }

            var model = new EditMenuViewModel
            {
                Id = menu.Id,
                Name = menu.Name,
                MenuType = menu.MenuType,
                Description = menu.Description,
                MenuMarkup = menu.MenuMarkup,
                FooterLeftZone = menu.FooterLeftZone,
                FooterRightZone = menu.FooterRightZone,
                IsEnabled = menu.IsEnabled
            };

            PopulateMenuTypes(model);
            return View(model);
        });
    }

    /// <summary>
    /// Check if there's an active menu conflict (AJAX)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CheckActiveConflict([FromBody] CheckConflictRequest request)
    {
        try
        {
            if (!request.IsActive)
            {
                return Json(new { hasConflict = false });
            }

            var menuType = (MenuType)request.MenuType;
            var hasActive = await _menuService.HasActiveMenuOfTypeAsync(menuType, request.MenuId);

            if (hasActive)
            {
                // Get the name of the conflicting menu
                var menus = await _menuService.GetAllAsync();
                var conflictingMenu = menus.FirstOrDefault(m => 
                    m.MenuType == request.MenuType && 
                    m.IsEnabled && 
                    m.Id != request.MenuId);

                return Json(new 
                { 
                    hasConflict = true,
                    conflictingMenuName = conflictingMenu?.Name ?? "Unknown",
                    menuTypeName = menuType.ToString()
                });
            }

            return Json(new { hasConflict = false });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking active conflict");
            return Json(new { hasConflict = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Save menu (create or update) - Refactored with Result Pattern
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditMenuViewModel model, bool forceActivation = false)
    {
        if (!ValidateModelState())
        {
            PopulateMenuTypes(model);
            return View(model);
        }

        // Execute save operation with Result Pattern
        var result = model.IsNew
            ? await CreateMenuWithResult(model, forceActivation)
            : await UpdateMenuWithResult(model, forceActivation);

        // Handle success/failure
        if (result.IsSuccess)
        {
            var action = model.IsNew ? "Created" : "Updated";
            var notificationKey = model.IsNew ? "Notification_MenuCreated" : "Notification_MenuUpdated";
            
            _logger.LogInformation("{Action} menu '{MenuName}' (ID: {MenuId}) by {User}", 
                action, result.Value!.Name, result.Value.Id, _userContext.Username ?? "System");
            
            NotifyLocalizedSuccess(notificationKey, result.Value.Name);
            
            // Check if menu was automatically set to inactive
            if (model.IsEnabled && !result.Value.IsEnabled)
            {
                NotifyWarning($"Menu set to inactive because another {((MenuType)model.MenuType).ToString()} menu is already active.");
            }
            
            return RedirectToAction(nameof(Index));
        }
        else
        {
            ModelState.AddModelError("", result.Error!);
            PopulateMenuTypes(model);
            return View(model);
        }
    }

    /// <summary>
    /// Toggle menu activation status (AJAX)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ToggleActivation(int id)
    {
        try
        {
            var menu = await _menuService.GetByIdAsync(id);
            if (menu == null)
            {
                return Json(new { success = false, message = "Menu not found." });
            }

            if (menu.IsEnabled)
            {
                await _menuService.DeactivateAsync(id);
            }
            else
            {
                await _menuService.ActivateAsync(id);
            }

            return Json(new { success = true, IsEnabled = !menu.IsEnabled });
        }
        catch (InvalidOperationException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling menu activation {Id}", id);
            return Json(new { success = false, message = "Error updating menu status. Please try again." });
        }
    }

    /// <summary>
    /// Delete menu (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var menu = await _menuService.GetByIdAsync(id);
            if (menu == null)
            {
                return Json(new { success = false, message = "Menu not found." });
            }

            var menuName = menu.Name;
            await _menuService.DeleteAsync(id);

            _logger.LogInformation("Menu {Name} deleted by {User}", menuName, _userContext.Username);

            return Json(new { success = true, message = $"Menu '{menuName}' deleted successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting menu {Id}", id);
            return Json(new { success = false, message = "Error deleting menu. Please try again." });
        }
    }

    /// <summary>
    /// Preview menu rendering as Bootstrap navbar
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Preview([FromBody] MenuPreviewViewModel model)
    {
        try
        {
            // Render as Bootstrap navbar HTML for accurate preview
            var navbarHtml = await _menuService.RenderMenuMarkupAsNavbarAsync(model.MenuMarkup);
            
            // Wrap in a navbar container for proper styling in preview
            var wrappedHtml = $@"
<nav class=""navbar navbar-expand-lg navbar-light bg-light"">
    <div class=""container-fluid"">
        <ul class=""navbar-nav"">
            {navbarHtml}
        </ul>
    </div>
</nav>";
            
            return Json(new
            {
                success = true,
                html = wrappedHtml
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing menu");
            return Json(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    #region Helper Methods - Result Pattern

    /// <summary>
    /// Creates a new menu with Result Pattern
    /// Encapsulates menu creation logic with validation
    /// </summary>
    private async Task<Result<MenuDto>> CreateMenuWithResult(EditMenuViewModel model, bool forceActivation)
    {
        try
        {
            var username = _userContext.Username ?? "System";
            
            var createDto = new MenuCreateDto
            {
                Name = model.Name,
                MenuType = model.MenuType,
                Description = model.Description ?? string.Empty,
                MenuMarkup = model.MenuMarkup,
                FooterLeftZone = model.FooterLeftZone,
                FooterRightZone = model.FooterRightZone,
                DisplayOrder = 0,
                IsEnabled = model.IsEnabled,
                ModifiedBy = username
            };

            var created = await _menuService.CreateAsync(createDto, forceActivation);
            return Result<MenuDto>.Success(created);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validation error creating menu");
            return Result<MenuDto>.Failure(ex.Message, "MENU_VALIDATION_ERROR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating menu");
            return Result<MenuDto>.Failure($"Error saving menu: {ex.Message}", "MENU_CREATE_ERROR");
        }
    }

    /// <summary>
    /// Updates an existing menu with Result Pattern
    /// Encapsulates menu update logic with validation
    /// </summary>
    private async Task<Result<MenuDto>> UpdateMenuWithResult(EditMenuViewModel model, bool forceActivation)
    {
        try
        {
            var username = _userContext.Username ?? "System";
            
            var updateDto = new MenuUpdateDto
            {
                Name = model.Name,
                MenuType = model.MenuType,
                Description = model.Description ?? string.Empty,
                MenuMarkup = model.MenuMarkup,
                FooterLeftZone = model.FooterLeftZone,
                FooterRightZone = model.FooterRightZone,
                DisplayOrder = 0,
                IsEnabled = model.IsEnabled,
                ModifiedBy = username
            };

            var updated = await _menuService.UpdateAsync(model.Id, updateDto, forceActivation);
            return Result<MenuDto>.Success(updated);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validation error updating menu");
            return Result<MenuDto>.Failure(ex.Message, "MENU_VALIDATION_ERROR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating menu");
            return Result<MenuDto>.Failure($"Error saving menu: {ex.Message}", "MENU_UPDATE_ERROR");
        }
    }

    #endregion

    #region Private Helper Methods

    private void PopulateMenuTypes(EditMenuViewModel model)
    {
        model.MenuTypes = Enum.GetValues<MenuType>()
            .Select(mt => new SelectListItem
            {
                Value = ((int)mt).ToString(),
                Text = mt.ToString(),
                Selected = model.MenuType == (int)mt
            })
            .ToList();
    }

    private int CountMenuItems(string menuMarkup)
    {
        if (string.IsNullOrWhiteSpace(menuMarkup))
        {
            return 0;
        }

        // Simple count of lines that start with * or -
        var lines = menuMarkup.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.Count(line => 
        {
            var trimmed = line.TrimStart();
            return trimmed.StartsWith("*") || trimmed.StartsWith("-");
        });
    }

    private string GetDefaultMenuMarkup()
    {
        return @"* [Home](/)
* [All Pages](/Pages/AllPages)
* [Tags](/Pages/AllTags)
* [Search](/Search)";
    }

    #endregion
}
