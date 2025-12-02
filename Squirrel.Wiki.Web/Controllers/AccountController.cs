using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Contracts.Plugins;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Services.Configuration;
using Squirrel.Wiki.Core.Services.Plugins;
using Squirrel.Wiki.Core.Services.Users;
using Squirrel.Wiki.Web.Extensions;
using Squirrel.Wiki.Web.Models;
using Squirrel.Wiki.Web.Services;
using Squirrel.Wiki.Contracts.Configuration;

namespace Squirrel.Wiki.Web.Controllers;

public class AccountController : BaseController
{
    private readonly IUserContext _userContext;
    private readonly IUserService _userService;
    private readonly IConfigurationService _configurationService;
    private readonly IPluginService _pluginService;

    public AccountController(
        IUserContext userContext,
        IUserService userService,
        IConfigurationService configurationService,
        IPluginService pluginService,
        ITimezoneService timezoneService,
        ILogger<AccountController> logger,
        INotificationService notifications)
        : base(logger, notifications, timezoneService, null)
    {
        _userContext = userContext;
        _userService = userService;
        _configurationService = configurationService;
        _pluginService = pluginService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        if (_userContext.IsAuthenticated)
        {
            return RedirectToAction("Index", "Home");
        }

        // Get enabled authentication plugins
        var enabledPlugins = await _pluginService.GetEnabledPluginsAsync();
        var authPlugins = new List<IAuthenticationPlugin>();
        
        foreach (var dbPlugin in enabledPlugins)
        {
            var loadedPlugin = _pluginService.GetLoadedPlugin<IAuthenticationPlugin>(dbPlugin.PluginId);
            if (loadedPlugin != null && dbPlugin.IsConfigured)
            {
                authPlugins.Add(loadedPlugin);
            }
        }

        var model = new LoginViewModel
        {
            ReturnUrl = returnUrl,
            AuthenticationPlugins = authPlugins
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

            // Get session timeout from configuration (default to 480 minutes / 8 hours if not set)
            var sessionTimeoutMinutes = await _configurationService.GetValueAsync<int>("SQUIRREL_SESSION_TIMEOUT_MINUTES");
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

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> OidcLogin(string? returnUrl = null)
    {
        // Get the OIDC plugin
        var oidcPlugin = _pluginService.GetLoadedPlugin<IAuthenticationPlugin>("squirrel.auth.oidc");
        if (oidcPlugin == null)
        {
            _logger.LogWarning("OIDC plugin not found or not loaded");
            NotifyError("OIDC authentication is not available.");
            return RedirectToAction("Login", new { returnUrl });
        }

        // Get plugin configuration
        var dbPlugin = await _pluginService.GetPluginByPluginIdAsync("squirrel.auth.oidc");
        if (dbPlugin == null || !dbPlugin.IsEnabled || !dbPlugin.IsConfigured)
        {
            _logger.LogWarning("OIDC plugin not enabled or configured");
            NotifyError("OIDC authentication is not properly configured.");
            return RedirectToAction("Login", new { returnUrl });
        }

        var config = await _pluginService.GetPluginConfigurationWithDefaultsAsync(dbPlugin.Id);

        // Build the authorization URL
        var authority = config["Authority"];
        var clientId = config["ClientId"];
        var scope = config.GetValueOrDefault("Scope", "openid profile email");
        
        var redirectUri = $"{Request.Scheme}://{Request.Host}/Account/OidcCallback";
        var state = Guid.NewGuid().ToString("N");
        var nonce = Guid.NewGuid().ToString("N");

        // Store state and nonce in session for validation
        HttpContext.Session.SetString("oidc_state", state);
        HttpContext.Session.SetString("oidc_nonce", nonce);
        HttpContext.Session.SetString("oidc_return_url", returnUrl ?? "/");

        // Build authorization URL
        var authUrl = $"{authority}/protocol/openid-connect/auth?" +
            $"client_id={Uri.EscapeDataString(clientId)}&" +
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
            $"response_type=code&" +
            $"scope={Uri.EscapeDataString(scope)}&" +
            $"state={state}&" +
            $"nonce={nonce}";

        _logger.LogInformation("Redirecting to OIDC provider: {Authority}", authority);
        return Redirect(authUrl);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> OidcCallback(string? code, string? state, string? error, string? error_description)
    {
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("OIDC authentication error: {Error} - {ErrorDescription}", error, error_description);
            NotifyError($"Authentication failed: {error_description ?? error}");
            return RedirectToAction("Login");
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            _logger.LogWarning("OIDC callback missing code or state");
            NotifyError("Invalid authentication response.");
            return RedirectToAction("Login");
        }

        // Validate state
        var savedState = HttpContext.Session.GetString("oidc_state");
        if (savedState != state)
        {
            _logger.LogWarning("OIDC state mismatch");
            NotifyError("Invalid authentication state.");
            return RedirectToAction("Login");
        }

        var returnUrl = HttpContext.Session.GetString("oidc_return_url") ?? "/";

        // Get the OIDC plugin and configuration
        var dbPlugin = await _pluginService.GetPluginByPluginIdAsync("squirrel.auth.oidc");
        if (dbPlugin == null)
        {
            NotifyError("OIDC authentication is not available.");
            return RedirectToAction("Login");
        }

        var config = await _pluginService.GetPluginConfigurationWithDefaultsAsync(dbPlugin.Id);

        try
        {
            // Exchange code for tokens
            var authority = config["Authority"];
            var clientId = config["ClientId"];
            var clientSecret = config["ClientSecret"];
            var redirectUri = $"{Request.Scheme}://{Request.Host}/Account/OidcCallback";

            using var httpClient = new HttpClient();
            var tokenRequest = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret
            };

            var tokenResponse = await httpClient.PostAsync(
                $"{authority}/protocol/openid-connect/token",
                new FormUrlEncodedContent(tokenRequest));

            if (!tokenResponse.IsSuccessStatusCode)
            {
                var errorContent = await tokenResponse.Content.ReadAsStringAsync();
                _logger.LogError("Token exchange failed: {StatusCode} - {Error}", tokenResponse.StatusCode, errorContent);
                NotifyError("Failed to complete authentication.");
                return RedirectToAction("Login");
            }

            var tokenData = await tokenResponse.Content.ReadAsStringAsync();
            var tokenJson = System.Text.Json.JsonDocument.Parse(tokenData);
            var accessToken = tokenJson.RootElement.GetProperty("access_token").GetString();
            var idToken = tokenJson.RootElement.GetProperty("id_token").GetString();

            // Get user info
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            
            var userInfoResponse = await httpClient.GetAsync($"{authority}/protocol/openid-connect/userinfo");
            if (!userInfoResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get user info");
                NotifyError("Failed to retrieve user information.");
                return RedirectToAction("Login");
            }

            var userInfoData = await userInfoResponse.Content.ReadAsStringAsync();
            var userInfo = System.Text.Json.JsonDocument.Parse(userInfoData);

            // Extract user information from claims
            var sub = userInfo.RootElement.GetProperty("sub").GetString();
            var usernameClaim = config.GetValueOrDefault("UsernameClaim", "preferred_username");
            var emailClaim = config.GetValueOrDefault("EmailClaim", "email");
            var displayNameClaim = config.GetValueOrDefault("DisplayNameClaim", "name");
            var groupsClaim = config.GetValueOrDefault("GroupsClaim", "groups");

            var username = userInfo.RootElement.TryGetProperty(usernameClaim, out var usernameProp) 
                ? usernameProp.GetString() : sub;
            var email = userInfo.RootElement.TryGetProperty(emailClaim, out var emailProp) 
                ? emailProp.GetString() : "";
            var displayName = userInfo.RootElement.TryGetProperty(displayNameClaim, out var displayNameProp) 
                ? displayNameProp.GetString() : username;

            var groups = new List<string>();
            if (userInfo.RootElement.TryGetProperty(groupsClaim, out var groupsProp) && groupsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var group in groupsProp.EnumerateArray())
                {
                    groups.Add(group.GetString() ?? "");
                }
            }

            // Authenticate using the OIDC plugin strategy
            var oidcPlugin = _pluginService.GetLoadedPlugin<IAuthenticationPlugin>("squirrel.auth.oidc");
            
            // Use reflection to call the overloaded CreateStrategy method that accepts IServiceProvider
            var createStrategyMethod = oidcPlugin!.GetType().GetMethod(
                "CreateStrategy",
                new[] { typeof(IServiceProvider), typeof(Dictionary<string, string>) });
            
            if (createStrategyMethod == null)
            {
                _logger.LogError("Could not find CreateStrategy method with IServiceProvider parameter");
                NotifyError("Internal authentication error.");
                return RedirectToAction("Login");
            }
            
            // Call the overloaded CreateStrategy method with the current request's service provider
            var strategy = (Squirrel.Wiki.Contracts.Authentication.IAuthenticationStrategy?)createStrategyMethod.Invoke(
                oidcPlugin, 
                new object[] { HttpContext.RequestServices, config });
            
            if (strategy == null)
            {
                _logger.LogError("CreateStrategy returned null");
                NotifyError("Internal authentication error.");
                return RedirectToAction("Login");
            }

            var authRequest = new Squirrel.Wiki.Contracts.Authentication.AuthenticationRequest
            {
                Provider = Squirrel.Wiki.Contracts.Authentication.AuthenticationProvider.External,
                ExternalId = sub,
                Username = username,
                Email = email,
                DisplayName = displayName,
                Groups = groups
            };

            var authResult = await strategy.AuthenticateAsync(authRequest);

            if (!authResult.Success)
            {
                _logger.LogWarning("OIDC authentication failed: {Reason}", authResult.FailureReason);
                NotifyError($"Authentication failed: {authResult.ErrorMessage}");
                return RedirectToAction("Login");
            }

            // Create claims and sign in
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, authResult.UserId.ToString()),
                new Claim(ClaimTypes.Name, authResult.Username!),
                new Claim(ClaimTypes.Email, authResult.Email ?? ""),
                new Claim("DisplayName", authResult.DisplayName ?? authResult.Username!)
            };

            if (authResult.IsAdmin)
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            if (authResult.IsEditor)
                claims.Add(new Claim(ClaimTypes.Role, "Editor"));

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            var sessionTimeoutMinutes = await _configurationService.GetValueAsync<int>("SQUIRREL_SESSION_TIMEOUT_MINUTES");
            if (sessionTimeoutMinutes <= 0)
                sessionTimeoutMinutes = 480;

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(sessionTimeoutMinutes)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                claimsPrincipal,
                authProperties);

            _logger.LogInformation("User {Username} logged in via OIDC", authResult.Username);

            // Clear session data
            HttpContext.Session.Remove("oidc_state");
            HttpContext.Session.Remove("oidc_nonce");
            HttpContext.Session.Remove("oidc_return_url");

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OIDC callback");
            NotifyError("An error occurred during authentication.");
            return RedirectToAction("Login");
        }
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
