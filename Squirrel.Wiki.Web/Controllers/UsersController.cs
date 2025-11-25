using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Squirrel.Wiki.Contracts.Authentication;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Services.Configuration;
using Squirrel.Wiki.Core.Services.Users;
using Squirrel.Wiki.Web.Models.Admin;
using Squirrel.Wiki.Web.Resources;
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
        ITimezoneService timezoneService,
        IStringLocalizer<SharedResources> localizer,
        ILogger<UsersController> logger,
        INotificationService notifications)
        : base(logger, notifications, timezoneService, localizer)
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

            // Populate timezone service for view
            PopulateBaseViewModel(model);

            return View(model);
        });
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

            PopulateBaseViewModel(model);
            return View(model);
        });
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
    /// Create new user - Refactored with Result Pattern
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserEditViewModel model)
    {
        if (!ValidateModelState())
            return View(model);

        // Validate password confirmation early
        if (model.Password != model.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Passwords do not match.");
            return View(model);
        }

        // Execute user creation with Result Pattern
        var result = await CreateUserWithResult(model);

        // Handle success/failure
        if (result.IsSuccess)
        {
            _logger.LogInformation("User {Username} created by {AdminUser}", model.Username, User.Identity?.Name);
            NotifyLocalizedSuccess("Notification_UserCreated", model.Username);
            return RedirectToAction(nameof(Details), new { id = result.Value!.Id });
        }
        else
        {
            ModelState.AddModelError("", result.Error!);
            return View(model);
        }
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
        });
    }

    /// <summary>
    /// Update user - Refactored with Result Pattern
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserEditViewModel model)
    {
        if (!ValidateModelState())
            return View(model);

        // Validate password confirmation early if password is being changed
        if (!string.IsNullOrWhiteSpace(model.Password) && model.Password != model.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Passwords do not match.");
            return View(model);
        }

        // Execute user update with Result Pattern
        var result = await UpdateUserWithResult(model);

        // Handle success/failure
        if (result.IsSuccess)
        {
            _logger.LogInformation("User {Username} updated by {AdminUser}", result.Value!.Username, User.Identity?.Name);
            NotifyLocalizedSuccess("Notification_UserUpdated", result.Value.Username);
            return RedirectToAction(nameof(Details), new { id = result.Value.Id });
        }
        else
        {
            ModelState.AddModelError("", result.Error!);
            return View(model);
        }
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
        });
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
        });
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
        });
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
        });
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
        });
    }

    /// <summary>
    /// Reset user password - Refactored with Result Pattern
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ValidateModelState())
            return View(model);

        // Validate password confirmation early
        if (model.NewPassword != model.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Passwords do not match.");
            return View(model);
        }

        // Execute password reset with Result Pattern
        var result = await ResetPasswordWithResult(model);

        // Handle success/failure
        if (result.IsSuccess)
        {
            _logger.LogInformation("Password reset for user {Username} by {AdminUser}", result.Value!, User.Identity?.Name);
            NotifyLocalizedSuccess("Notification_PasswordReset", result.Value);
            return RedirectToAction(nameof(Details), new { id = model.UserId });
        }
        else
        {
            ModelState.AddModelError("", result.Error!);
            return View(model);
        }
    }

    #region Helper Methods - Result Pattern

    /// <summary>
    /// Creates a new user with Result Pattern
    /// Encapsulates user creation logic including role assignment and activation
    /// </summary>
    private async Task<Result<UserDto>> CreateUserWithResult(UserEditViewModel model)
    {
        try
        {
            // Create user with roles
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

            return Result<UserDto>.Success(user);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validation error creating user {Username}", model.Username);
            return Result<UserDto>.Failure(ex.Message, "USER_VALIDATION_ERROR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Username}", model.Username);
            return Result<UserDto>.Failure("An error occurred while creating the user. Please try again.", "USER_CREATE_ERROR");
        }
    }

    /// <summary>
    /// Updates an existing user with Result Pattern
    /// Encapsulates user update logic including role changes, activation status, and password updates
    /// </summary>
    private async Task<Result<UserDto>> UpdateUserWithResult(UserEditViewModel model)
    {
        try
        {
            var user = await _userService.GetByIdAsync(model.Id);
            if (user == null)
            {
                return Result<UserDto>.Failure("User not found.", "USER_NOT_FOUND");
            }

            // Update user via DTO
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
                await _userService.SetPasswordAsync(user.Id, model.Password);
            }

            // Get updated user
            var updatedUser = await _userService.GetByIdAsync(user.Id);
            return Result<UserDto>.Success(updatedUser!);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validation error updating user {UserId}", model.Id);
            return Result<UserDto>.Failure(ex.Message, "USER_VALIDATION_ERROR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", model.Id);
            return Result<UserDto>.Failure("An error occurred while updating the user. Please try again.", "USER_UPDATE_ERROR");
        }
    }

    /// <summary>
    /// Resets a user's password with Result Pattern
    /// Encapsulates password reset logic with validation
    /// </summary>
    private async Task<Result<string>> ResetPasswordWithResult(ResetPasswordViewModel model)
    {
        try
        {
            var user = await _userService.GetByIdAsync(model.UserId);
            if (user == null)
            {
                return Result<string>.Failure("User not found.", "USER_NOT_FOUND");
            }

            await _userService.SetPasswordAsync(model.UserId, model.NewPassword);

            return Result<string>.Success(user.Username);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validation error resetting password for user {UserId}", model.UserId);
            return Result<string>.Failure(ex.Message, "PASSWORD_VALIDATION_ERROR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for user {UserId}", model.UserId);
            return Result<string>.Failure("An error occurred while resetting the password. Please try again.", "PASSWORD_RESET_ERROR");
        }
    }

    #endregion
}
