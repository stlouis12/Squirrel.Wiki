using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Services;
using Squirrel.Wiki.Web.Extensions;
using Squirrel.Wiki.Web.Models;
using Squirrel.Wiki.Web.Services;

namespace Squirrel.Wiki.Web.Controllers;

public class AccountController : BaseController
{
    private readonly IUserContext _userContext;
    private readonly IUserService _userService;
    private readonly ISettingsService _settingsService;

    public AccountController(
        IUserContext userContext,
        IUserService userService,
        ISettingsService settingsService,
        ITimezoneService timezoneService,
        ILogger<AccountController> logger,
        INotificationService notifications)
        : base(logger, notifications, timezoneService, null)
    {
        _userContext = userContext;
        _userService = userService;
        _settingsService = settingsService;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (_userContext.IsAuthenticated)
        {
            return RedirectToAction("Index", "Home");
        }

        var model = new LoginViewModel
        {
            ReturnUrl = returnUrl
        };

        return View(model);
    }

    /// <summary>
    /// Login POST - Refactored with Result Pattern
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await AuthenticateUserWithResult(model);

        if (result.IsSuccess)
        {
            var user = result.Value!;
            
            // Create claims and sign in
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("DisplayName", user.DisplayName)
            };

            // Add role claims
            foreach (var role in user.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            // Get session timeout from settings (default to 480 minutes / 8 hours if not set)
            var sessionTimeoutMinutes = await _settingsService.GetSettingAsync<int>("SessionTimeoutMinutes");
            if (sessionTimeoutMinutes <= 0)
            {
                sessionTimeoutMinutes = 480; // Default to 8 hours
            }

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe 
                    ? DateTimeOffset.UtcNow.AddDays(30) 
                    : DateTimeOffset.UtcNow.AddMinutes(sessionTimeoutMinutes)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                claimsPrincipal,
                authProperties);

            _logger.LogInformation("User {Username} ({Email}) logged in successfully", user.Username, user.Email);

            // Redirect to return URL or home
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
        else
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var email = _userContext.Email;
        
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        
        _logger.LogInformation("User {Email} logged out", email);

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        if (string.IsNullOrEmpty(_userContext.UserId))
        {
            return RedirectToAction("Login");
        }

        if (!Guid.TryParse(_userContext.UserId, out var userId))
        {
            return RedirectToAction("Login");
        }

        try
        {
            var user = await _userService.GetByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User not found for ID {UserId}", userId);
                return RedirectToAction("Login");
            }

            var model = new ProfileViewModel
            {
                UserId = user.Id.ToString(),
                Username = user.Username,
                Email = user.Email,
                DisplayName = user.DisplayName,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                IsAdmin = user.IsAdmin,
                IsEditor = user.IsEditor,
                Roles = user.Roles,
                Provider = user.Provider.ToString(),
                LastLoginOn = user.LastLoginOn,
                CreatedOn = user.CreatedOn,
                LastPasswordChangeOn = user.LastPasswordChangeOn
            };

            PopulateBaseViewModel(model);
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading profile for user {UserId}", userId);
            NotifyError("An error occurred while loading your profile.");
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword()
    {
        // Only allow local users to change password
        if (_userContext.UserId == null)
        {
            return RedirectToAction("Login");
        }

        return View();
    }

    /// <summary>
    /// Change Password POST - Refactored with Result Pattern
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (string.IsNullOrEmpty(_userContext.UserId))
        {
            return RedirectToAction("Login");
        }

        if (!Guid.TryParse(_userContext.UserId, out var userId))
        {
            return RedirectToAction("Login");
        }

        var result = await ChangePasswordWithResult(userId, model);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Password changed for user {Username} (ID: {UserId})", 
                result.Value!, userId);
            
            NotifyLocalizedSuccess("Notification_PasswordChanged");
            return RedirectToAction("Profile");
        }
        else
        {
            // Check if it's a current password error
            if (result.ErrorCode == "PASSWORD_INCORRECT")
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), result.Error!);
            }
            else
            {
                ModelState.AddModelError(string.Empty, result.Error!);
            }
            return View(model);
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    #region Helper Methods - Result Pattern

    /// <summary>
    /// Authenticates a user with Result Pattern
    /// Encapsulates authentication logic with validation
    /// </summary>
    private async Task<Result<UserDto>> AuthenticateUserWithResult(LoginViewModel model)
    {
        try
        {
            // Authenticate user using UserService
            var user = await _userService.AuthenticateAsync(model.UsernameOrEmail, model.Password);

            if (user == null)
            {
                _logger.LogWarning("Failed login attempt for {UsernameOrEmail}", model.UsernameOrEmail);
                return Result<UserDto>.Failure(
                    "Invalid username/email or password.",
                    "LOGIN_INVALID_CREDENTIALS"
                ).WithContext("UsernameOrEmail", model.UsernameOrEmail);
            }

            // Check if account is active
            if (!user.IsActive)
            {
                _logger.LogWarning("Login attempt for inactive account: {UsernameOrEmail}", model.UsernameOrEmail);
                return Result<UserDto>.Failure(
                    "Your account has been deactivated. Please contact an administrator.",
                    "LOGIN_ACCOUNT_INACTIVE"
                ).WithContext("UserId", user.Id)
                 .WithContext("Username", user.Username);
            }

            // Check if account is locked
            if (user.IsLocked)
            {
                var lockMessage = user.LockedUntil.HasValue
                    ? $"Your account is locked until {user.LockedUntil.Value:yyyy-MM-dd HH:mm}."
                    : "Your account has been locked. Please contact an administrator.";
                
                _logger.LogWarning("Login attempt for locked account: {UsernameOrEmail}", model.UsernameOrEmail);
                return Result<UserDto>.Failure(
                    lockMessage,
                    "LOGIN_ACCOUNT_LOCKED"
                ).WithContext("UserId", user.Id)
                 .WithContext("Username", user.Username)
                 .WithContext("LockedUntil", user.LockedUntil);
            }

            return Result<UserDto>.Success(user);
        }
        catch (InvalidOperationException ex)
        {
            // Handle account locked due to failed attempts
            _logger.LogWarning(ex, "Login failed for {UsernameOrEmail}", model.UsernameOrEmail);
            return Result<UserDto>.Failure(ex.Message, "LOGIN_FAILED_ATTEMPTS");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {UsernameOrEmail}", model.UsernameOrEmail);
            return Result<UserDto>.Failure(
                "An error occurred during login. Please try again.",
                "LOGIN_ERROR"
            );
        }
    }

    /// <summary>
    /// Changes user password with Result Pattern
    /// Encapsulates password change logic with validation
    /// </summary>
    private async Task<Result<string>> ChangePasswordWithResult(Guid userId, ChangePasswordViewModel model)
    {
        try
        {
            // Get current user
            var user = await _userService.GetByIdAsync(userId);
            if (user == null)
            {
                return Result<string>.Failure(
                    "User not found.",
                    "USER_NOT_FOUND"
                ).WithContext("UserId", userId);
            }

            // Check if user is a local user
            if (user.Provider.ToString() != "Local")
            {
                return Result<string>.Failure(
                    "Password change is only available for local accounts.",
                    "PASSWORD_CHANGE_NOT_ALLOWED"
                ).WithContext("UserId", userId)
                 .WithContext("Provider", user.Provider.ToString());
            }

            // Verify current password
            var authenticatedUser = await _userService.AuthenticateAsync(user.Email, model.CurrentPassword);
            if (authenticatedUser == null)
            {
                _logger.LogWarning("Incorrect current password for user {UserId}", userId);
                return Result<string>.Failure(
                    "Current password is incorrect.",
                    "PASSWORD_INCORRECT"
                ).WithContext("UserId", userId);
            }

            // Change password
            await _userService.SetPasswordAsync(userId, model.NewPassword);

            return Result<string>.Success(user.Username);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validation error changing password for user {UserId}", userId);
            return Result<string>.Failure(ex.Message, "PASSWORD_CHANGE_VALIDATION_ERROR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}", userId);
            return Result<string>.Failure(
                "An error occurred while changing your password. Please try again.",
                "PASSWORD_CHANGE_ERROR"
            );
        }
    }

    #endregion
}
