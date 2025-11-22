using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Services;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Web.Models;

namespace Squirrel.Wiki.Web.Controllers;

public class AccountController : Controller
{
    private readonly IUserContext _userContext;
    private readonly IUserService _userService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IUserContext userContext,
        IUserService userService,
        ISettingsService settingsService,
        ILogger<AccountController> logger)
    {
        _userContext = userContext;
        _userService = userService;
        _settingsService = settingsService;
        _logger = logger;
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

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            // Authenticate user using UserService
            var user = await _userService.AuthenticateAsync(model.UsernameOrEmail, model.Password);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid username/email or password.");
                return View(model);
            }

            // Check if account is active
            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact an administrator.");
                _logger.LogWarning("Login attempt for inactive account: {UsernameOrEmail}", model.UsernameOrEmail);
                return View(model);
            }

            // Check if account is locked
            if (user.IsLocked)
            {
                var lockMessage = user.LockedUntil.HasValue
                    ? $"Your account is locked until {user.LockedUntil.Value:yyyy-MM-dd HH:mm}."
                    : "Your account has been locked. Please contact an administrator.";
                
                ModelState.AddModelError(string.Empty, lockMessage);
                _logger.LogWarning("Login attempt for locked account: {UsernameOrEmail}", model.UsernameOrEmail);
                return View(model);
            }

            // Create claims for the authenticated user
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
        catch (InvalidOperationException ex)
        {
            // Handle account locked due to failed attempts
            ModelState.AddModelError(string.Empty, ex.Message);
            _logger.LogWarning(ex, "Login failed for {UsernameOrEmail}", model.UsernameOrEmail);
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {UsernameOrEmail}", model.UsernameOrEmail);
            ModelState.AddModelError(string.Empty, "An error occurred during login. Please try again.");
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

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading profile for user {UserId}", userId);
            TempData["ErrorMessage"] = "An error occurred while loading your profile.";
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

        try
        {
            // Get current user
            var user = await _userService.GetByIdAsync(userId);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "User not found.");
                return View(model);
            }

            // Check if user is a local user
            if (user.Provider.ToString() != "Local")
            {
                ModelState.AddModelError(string.Empty, "Password change is only available for local accounts.");
                return View(model);
            }

            // Verify current password
            var authenticatedUser = await _userService.AuthenticateAsync(user.Email, model.CurrentPassword);
            if (authenticatedUser == null)
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect.");
                return View(model);
            }

            // Change password
            await _userService.SetPasswordAsync(userId, model.NewPassword);

            TempData["SuccessMessage"] = "Your password has been changed successfully.";
            _logger.LogInformation("Password changed for user {Username} (ID: {UserId})", user.Username, userId);

            return RedirectToAction("Profile");
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}", userId);
            ModelState.AddModelError(string.Empty, "An error occurred while changing your password. Please try again.");
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
}
