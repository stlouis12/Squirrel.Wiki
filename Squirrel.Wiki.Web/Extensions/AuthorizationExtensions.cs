using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Database.Entities;
using System.Security.Claims;

namespace Squirrel.Wiki.Web.Extensions;

/// <summary>
/// Extension methods to simplify authorization in controllers
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Authorizes access to a page resource and returns an appropriate action result if authorization fails
    /// </summary>
    /// <param name="authorizationService">The authorization service</param>
    /// <param name="controller">The controller performing the authorization</param>
    /// <param name="user">The current user</param>
    /// <param name="page">The page to authorize access to</param>
    /// <param name="policyName">The policy name (e.g., "CanViewPage", "CanEditPage", "CanDeletePage")</param>
    /// <param name="returnUrl">Optional return URL for login redirect</param>
    /// <returns>Null if authorized, or an ActionResult (Forbid/Redirect) if not authorized</returns>
    public static async Task<IActionResult?> AuthorizePageAccessAsync(
        this IAuthorizationService authorizationService,
        ControllerBase controller,
        ClaimsPrincipal user,
        Page page,
        string policyName,
        string? returnUrl = null)
    {
        var authResult = await authorizationService.AuthorizeAsync(user, page, policyName);
        
        if (authResult.Succeeded)
        {
            return null; // Authorization succeeded
        }

        // Authorization failed - return appropriate result
        if (user.Identity?.IsAuthenticated == true)
        {
            // User is authenticated but not authorized
            return controller.Forbid();
        }
        else
        {
            // User is not authenticated - redirect to login
            var loginUrl = returnUrl != null
                ? controller.Url.Action("Login", "Account", new { returnUrl })
                : controller.Url.Action("Login", "Account");

            return controller.Redirect(loginUrl ?? "/Account/Login");
        }
    }

    /// <summary>
    /// Checks if the user is authorized to access a page resource
    /// </summary>
    /// <param name="authorizationService">The authorization service</param>
    /// <param name="user">The current user</param>
    /// <param name="page">The page to check authorization for</param>
    /// <param name="policyName">The policy name</param>
    /// <returns>True if authorized, false otherwise</returns>
    public static async Task<bool> IsAuthorizedAsync(
        this IAuthorizationService authorizationService,
        ClaimsPrincipal user,
        Page page,
        string policyName)
    {
        var authResult = await authorizationService.AuthorizeAsync(user, page, policyName);
        return authResult.Succeeded;
    }
}
