using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Web.Models;

namespace Squirrel.Wiki.Web.Controllers;

public class AccountController : Controller
{
    private readonly IUserContext _userContext;
    private readonly ILogger<AccountController> _logger;

    // Development test users (hardcoded for development only)
    private static readonly Dictionary<string, (string Password, string Username, string Role)> TestUsers = new()
    {
        { "admin@squirrel.wiki", ("Admin123!", "admin", "Admin") },
        { "editor@squirrel.wiki", ("Editor123!", "editor", "Editor") },
        { "viewer@squirrel.wiki", ("Viewer123!", "viewer", "Viewer") }
    };

    public AccountController(IUserContext userContext, ILogger<AccountController> logger)
    {
        _userContext = userContext;
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

        // Validate credentials against test users (development only)
        if (!TestUsers.TryGetValue(model.Email.ToLowerInvariant(), out var userInfo))
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        if (userInfo.Password != model.Password)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        // Create claims for the authenticated user
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, userInfo.Username),
            new Claim(ClaimTypes.Email, model.Email),
            new Claim(ClaimTypes.Role, userInfo.Role)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe 
                ? DateTimeOffset.UtcNow.AddDays(30) 
                : DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            claimsPrincipal,
            authProperties);

        _logger.LogInformation("User {Email} logged in successfully", model.Email);

        // Redirect to return URL or home
        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
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
    public IActionResult Profile()
    {
        var model = new ProfileViewModel
        {
            UserId = _userContext.UserId ?? string.Empty,
            Username = _userContext.Username ?? string.Empty,
            Email = _userContext.Email ?? string.Empty,
            DisplayName = _userContext.Username ?? string.Empty,
            IsAdmin = _userContext.IsAdmin,
            IsEditor = _userContext.IsEditor,
            Roles = _userContext.Roles,
            LastLoginOn = DateTime.UtcNow,
            CreatedOn = DateTime.UtcNow
        };

        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }
}
