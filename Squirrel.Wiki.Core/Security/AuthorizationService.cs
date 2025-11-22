using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Services;

namespace Squirrel.Wiki.Core.Security;

/// <summary>
/// Implementation of authorization service
/// </summary>
public class AuthorizationService : IAuthorizationService
{
    private readonly IUserContext _userContext;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<AuthorizationService> _logger;

    public AuthorizationService(
        IUserContext userContext,
        ISettingsService settingsService,
        ILogger<AuthorizationService> logger)
    {
        _userContext = userContext;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<bool> CanViewPageAsync(Page page, CancellationToken cancellationToken = default)
    {
        // Deleted pages cannot be viewed
        if (page.IsDeleted)
        {
            _logger.LogDebug("Page {PageId} is deleted, denying access", page.Id);
            return false;
        }

        // Check page-specific visibility first
        switch (page.Visibility)
        {
            case PageVisibility.Public:
                // Public pages are always viewable
                _logger.LogDebug("Page {PageId} is public, allowing access", page.Id);
                return true;

            case PageVisibility.Private:
                // Private pages require authentication
                var isAuthenticatedForPrivate = IsAuthenticated();
                _logger.LogDebug("Page {PageId} is private, authenticated: {IsAuthenticated}", 
                    page.Id, isAuthenticatedForPrivate);
                return isAuthenticatedForPrivate;

            case PageVisibility.Inherit:
            default:
                // Inherit from global setting
                var allowAnonymousReading = await _settingsService.GetSettingAsync<bool>(
                    "AllowAnonymousReading", cancellationToken);
                
                if (allowAnonymousReading)
                {
                    // Global setting allows anonymous reading
                    _logger.LogDebug("Page {PageId} inherits global setting (anonymous allowed), allowing access", 
                        page.Id);
                    return true;
                }
                else
                {
                    // Global setting requires authentication
                    var isAuthenticatedForInherit = IsAuthenticated();
                    _logger.LogDebug("Page {PageId} inherits global setting (authentication required), authenticated: {IsAuthenticated}", 
                        page.Id, isAuthenticatedForInherit);
                    return isAuthenticatedForInherit;
                }
        }
    }

    public bool IsAdmin()
    {
        return IsAuthenticated() && _userContext.IsAdmin;
    }

    public bool IsAuthenticated()
    {
        return _userContext.IsAuthenticated;
    }

    public Task<bool> CanEditPageAsync(Models.PageDto page, string? username, string? userRole)
    {
        // If page is locked, only admins can edit
        if (page.IsLocked)
        {
            return Task.FromResult(userRole == "Admin");
        }

        // Otherwise, admins and editors can edit
        return Task.FromResult(userRole == "Admin" || userRole == "Editor");
    }

    public Task<bool> CanDeletePageAsync(Models.PageDto page, string? userRole)
    {
        // If page is locked, only admins can delete
        if (page.IsLocked)
        {
            return Task.FromResult(userRole == "Admin");
        }

        // Otherwise, admins and editors can delete
        return Task.FromResult(userRole == "Admin" || userRole == "Editor");
    }
}
