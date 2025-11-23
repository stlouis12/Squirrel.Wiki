using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Contracts.Authentication;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Services;
using Squirrel.Wiki.Web.Models.Admin;
using Squirrel.Wiki.Web.Services;

namespace Squirrel.Wiki.Web.Controllers;

/// <summary>
/// Controller for user management (admin only)
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class UsersController : BaseController
{
    private readonly IUserService _userService;

    public UsersController(
        IUserService userService,
        ILogger<UsersController> logger,
        INotificationService notifications)
        : base(logger, notifications)
    {
        _userService = userService;
    }

    /// <summary>
    /// List all users
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search = null,
        string? role = null,
        string? provider = null,
        bool? isActive = null,
        int page = 1,
        int pageSize = 20)
    {
        return await ExecuteAsync(async () =>
        {
            var users = await _userService.GetAllAsync();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                users = users.Where(u =>
                    u.Username.ToLower().Contains(search) ||
                    u.Email.ToLower().Contains(search) ||
                    u.DisplayName.ToLower().Contains(search)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                users = users.Where(u => u.Roles.Contains(role)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(provider))
            {
                var providerEnum = Enum.Parse<AuthenticationProvider>(provider, true);
                users = users.Where(u => u.Provider == providerEnum).ToList();
            }

            if (isActive.HasValue)
            {
                users = users.Where(u => u.IsActive == isActive.Value).ToList();
            }

            // Pagination
            var totalCount = users.Count();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            var pagedUsers = users
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var model = new UserListViewModel
            {
                Users = pagedUsers,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalCount = totalCount,
                PageSize = pageSize,
                SearchTerm = search,
                RoleFilter = role,
                ProviderFilter = provider,
                IsActiveFilter = isActive
            };

            return View(model);
        },
        "Error loading users. Please try again.",
        "Error loading user list");
    }

    /// <summary>
    /// Show user details
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        return await ExecuteAsync(async () =>
        {
            var user = await _userService.GetByIdAsync(id);
            if (!ValidateEntityExists(user, "User"))
                return RedirectToAction(nameof(Index));

            var model = new UserDetailsViewModel
            {
                User = user
            };

            return View(model);
        },
        "Error loading user details.",
        $"Error loading user details for {id}");
    }

    /// <summary>
    /// Show create user form
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        var model = new UserEditViewModel
        {
            IsActive = true,
            Roles = new List<string>()
        };

        return View(model);
    }

    /// <summary>
    /// Create new user
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserEditViewModel model)
    {
        if (!ValidateModelState())
            return View(model);

        return await ExecuteAsync(async () =>
        {
            // Validate password confirmation
            if (model.Password != model.ConfirmPassword)
            {
                ModelState.AddModelError(nameof(model.ConfirmPassword), "Passwords do not match.");
                return View(model);
            }

            // Create user
            // If no roles are selected, user will be a Viewer (read-only)
            var user = await _userService.CreateLocalUserAsync(
                username: model.Username,
                email: model.Email,
                password: model.Password!,
                displayName: model.DisplayName,
                isAdmin: model.Roles?.Contains("Admin") ?? false,
                isEditor: (model.Roles?.Contains("Editor") ?? false) || (model.Roles?.Contains("Admin") ?? false)
            );

            // Set additional properties
            if (!model.IsActive)
            {
                await _userService.DeactivateAccountAsync(user.Id);
            }

            _logger.LogInformation("User {Username} created by {AdminUser}", model.Username, User.Identity?.Name);
            NotifyLocalizedSuccess("Notification_UserCreated", model.Username);
            return RedirectToAction(nameof(Details), new { id = user.Id });
        },
        ex =>
        {
            if (ex is InvalidOperationException)
            {
                ModelState.AddModelError("", ex.Message);
            }
            else
            {
                _logger.LogError(ex, "Error creating user {Username}", model.Username);
                ModelState.AddModelError("", "An error occurred while creating the user. Please try again.");
            }
            return View(model);
        });
    }

    /// <summary>
    /// Show edit user form
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        return await ExecuteAsync(async () =>
        {
            var user = await _userService.GetByIdAsync(id);
            if (!ValidateEntityExists(user, "User"))
                return RedirectToAction(nameof(Index));

            var model = new UserEditViewModel
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                DisplayName = user.DisplayName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive,
                Roles = user.Roles.ToList(),
                Provider = user.Provider
            };

            return View(model);
        },
        "Error loading user.",
        $"Error loading user for edit: {id}");
    }

    /// <summary>
    /// Update user
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserEditViewModel model)
    {
        if (!ValidateModelState())
            return View(model);

        return await ExecuteAsync(async () =>
        {
            var user = await _userService.GetByIdAsync(model.Id);
            if (!ValidateEntityExists(user, "User"))
                return RedirectToAction(nameof(Index));

            // Update user via DTO
            // If no roles are selected, user will be a Viewer (read-only)
            var updateDto = new UserUpdateDto
            {
                Email = model.Email,
                DisplayName = model.DisplayName,
                FirstName = model.FirstName,
                LastName = model.LastName,
                IsAdmin = model.Roles?.Contains("Admin") ?? false,
                IsEditor = (model.Roles?.Contains("Editor") ?? false) || (model.Roles?.Contains("Admin") ?? false)
            };

            await _userService.UpdateAsync(user.Id, updateDto);

            // Update active status if changed
            if (model.IsActive != user.IsActive)
            {
                if (model.IsActive)
                {
                    await _userService.ActivateAccountAsync(user.Id);
                }
                else
                {
                    await _userService.DeactivateAccountAsync(user.Id);
                }
            }

            // Update password if provided
            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                if (model.Password != model.ConfirmPassword)
                {
                    ModelState.AddModelError(nameof(model.ConfirmPassword), "Passwords do not match.");
                    return View(model);
                }

                await _userService.SetPasswordAsync(user.Id, model.Password);
            }

            _logger.LogInformation("User {Username} updated by {AdminUser}", user.Username, User.Identity?.Name);
            NotifyLocalizedSuccess("Notification_UserUpdated", user.Username);
            return RedirectToAction(nameof(Details), new { id = user.Id });
        },
        ex =>
        {
            if (ex is InvalidOperationException)
            {
                ModelState.AddModelError("", ex.Message);
            }
            else
            {
                _logger.LogError(ex, "Error updating user {UserId}", model.Id);
                ModelState.AddModelError("", "An error occurred while updating the user. Please try again.");
            }
            return View(model);
        });
    }

    /// <summary>
    /// Delete user (deactivates the account)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        return await ExecuteAsync(async () =>
        {
            var user = await _userService.GetByIdAsync(id);
            if (!ValidateEntityExists(user, "User"))
                return RedirectToAction(nameof(Index));

            // Prevent deleting yourself
            if (User.Identity?.Name == user.Username)
            {
                NotifyError("You cannot delete your own account.");
                return RedirectToAction(nameof(Index));
            }

            // For now, just deactivate instead of delete
            await _userService.DeactivateAccountAsync(id);

            _logger.LogInformation("User {Username} deactivated (delete requested) by {AdminUser}", user.Username, User.Identity?.Name);
            NotifyLocalizedSuccess("Notification_UserDeleted", user.Username);
            return RedirectToAction(nameof(Index));
        },
        "An error occurred while deleting the user. Please try again.",
        $"Error deleting user {id}");
    }

    /// <summary>
    /// Deactivate user
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        return await ExecuteAsync(async () =>
        {
            var user = await _userService.GetByIdAsync(id);
            if (!ValidateEntityExists(user, "User"))
                return RedirectToAction(nameof(Index));

            // Prevent deactivating yourself
            if (User.Identity?.Name == user.Username)
            {
                NotifyError("You cannot deactivate your own account.");
                return RedirectToAction(nameof(Details), new { id });
            }

            await _userService.DeactivateAccountAsync(id);

            _logger.LogInformation("User {Username} deactivated by {AdminUser}", user.Username, User.Identity?.Name);
            NotifyLocalizedSuccess("Notification_UserDeactivated", user.Username);
            return RedirectToAction(nameof(Details), new { id });
        },
        "An error occurred while deactivating the user.",
        $"Error deactivating user {id}");
    }

    /// <summary>
    /// Activate user
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(Guid id)
    {
        return await ExecuteAsync(async () =>
        {
            var user = await _userService.GetByIdAsync(id);
            if (!ValidateEntityExists(user, "User"))
                return RedirectToAction(nameof(Index));

            await _userService.ActivateAccountAsync(id);

            _logger.LogInformation("User {Username} activated by {AdminUser}", user.Username, User.Identity?.Name);
            NotifyLocalizedSuccess("Notification_UserActivated", user.Username);
            return RedirectToAction(nameof(Details), new { id });
        },
        "An error occurred while activating the user.",
        $"Error activating user {id}");
    }

    /// <summary>
    /// Unlock user account
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(Guid id)
    {
        return await ExecuteAsync(async () =>
        {
            var user = await _userService.GetByIdAsync(id);
            if (!ValidateEntityExists(user, "User"))
                return RedirectToAction(nameof(Index));

            await _userService.UnlockAccountAsync(id);

            _logger.LogInformation("User {Username} unlocked by {AdminUser}", user.Username, User.Identity?.Name);
            NotifyLocalizedSuccess("Notification_UserUnlocked", user.Username);
            return RedirectToAction(nameof(Details), new { id });
        },
        "An error occurred while unlocking the user.",
        $"Error unlocking user {id}");
    }

    /// <summary>
    /// Show reset password form
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ResetPassword(Guid id)
    {
        return await ExecuteAsync(async () =>
        {
            var user = await _userService.GetByIdAsync(id);
            if (!ValidateEntityExists(user, "User"))
                return RedirectToAction(nameof(Index));

            if (user.Provider != AuthenticationProvider.Local)
            {
                NotifyError("Cannot reset password for non-local users.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var model = new ResetPasswordViewModel
            {
                UserId = user.Id,
                Username = user.Username
            };

            return View(model);
        },
        "Error loading reset password form.",
        $"Error loading reset password form for {id}");
    }

    /// <summary>
    /// Reset user password
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ValidateModelState())
            return View(model);

        return await ExecuteAsync(async () =>
        {
            var user = await _userService.GetByIdAsync(model.UserId);
            if (!ValidateEntityExists(user, "User"))
                return RedirectToAction(nameof(Index));

            if (model.NewPassword != model.ConfirmPassword)
            {
                ModelState.AddModelError(nameof(model.ConfirmPassword), "Passwords do not match.");
                return View(model);
            }

            await _userService.SetPasswordAsync(model.UserId, model.NewPassword);

            _logger.LogInformation("Password reset for user {Username} by {AdminUser}", user.Username, User.Identity?.Name);
            NotifyLocalizedSuccess("Notification_PasswordReset", user.Username);
            return RedirectToAction(nameof(Details), new { id = model.UserId });
        },
        ex =>
        {
            if (ex is InvalidOperationException)
            {
                ModelState.AddModelError("", ex.Message);
            }
            else
            {
                _logger.LogError(ex, "Error resetting password for user {UserId}", model.UserId);
                ModelState.AddModelError("", "An error occurred while resetting the password. Please try again.");
            }
            return View(model);
        });
    }
}
