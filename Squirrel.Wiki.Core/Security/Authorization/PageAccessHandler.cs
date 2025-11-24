using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Security.Authorization;

/// <summary>
/// Authorization handler for page access operations.
/// Wraps the existing IAuthorizationService to integrate with ASP.NET Core's policy-based authorization.
/// </summary>
public class PageAccessHandler : AuthorizationHandler<PageAccessRequirement, Page>
{
    private readonly IAuthorizationService _authService;
    private readonly IUserContext _userContext;
    private readonly ILogger<PageAccessHandler> _logger;

    public PageAccessHandler(
        IAuthorizationService authService,
        IUserContext userContext,
        ILogger<PageAccessHandler> logger)
    {
        _authService = authService;
        _userContext = userContext;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PageAccessRequirement requirement,
        Page resource)
    {
        try
        {
            var canAccess = requirement.AccessType switch
            {
                PageAccessType.View => await HandleViewAccessAsync(resource),
                PageAccessType.Edit => await HandleEditAccessAsync(resource),
                PageAccessType.Delete => await HandleDeleteAccessAsync(resource),
                _ => false
            };

            if (canAccess)
            {
                _logger.LogDebug(
                    "Authorization succeeded for page {PageId}, access type: {AccessType}, user: {Username}",
                    resource.Id,
                    requirement.AccessType,
                    _userContext.Username ?? "anonymous");

                context.Succeed(requirement);
            }
            else
            {
                _logger.LogDebug(
                    "Authorization failed for page {PageId}, access type: {AccessType}, user: {Username}",
                    resource.Id,
                    requirement.AccessType,
                    _userContext.Username ?? "anonymous");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error during authorization for page {PageId}, access type: {AccessType}",
                resource.Id,
                requirement.AccessType);
            
            // On error, deny access for security
        }
    }

    private async Task<bool> HandleViewAccessAsync(Page page)
    {
        // Use existing authorization service for view access
        return await _authService.CanViewPageAsync(page);
    }

    private async Task<bool> HandleEditAccessAsync(Page page)
    {
        // Convert Page entity to PageDto for existing authorization service
        var pageDto = MapToPageDto(page);
        var userRole = GetUserRole();
        return await _authService.CanEditPageAsync(
            pageDto,
            _userContext.Username,
            userRole);
    }

    private async Task<bool> HandleDeleteAccessAsync(Page page)
    {
        // Convert Page entity to PageDto for existing authorization service
        var pageDto = MapToPageDto(page);
        var userRole = GetUserRole();
        return await _authService.CanDeletePageAsync(pageDto, userRole);
    }

    /// <summary>
    /// Gets the user's role for authorization checks.
    /// Returns the highest privilege role (Admin > Editor > null)
    /// </summary>
    private string? GetUserRole()
    {
        if (!_userContext.IsAuthenticated)
            return null;

        if (_userContext.IsAdmin)
            return "Admin";

        if (_userContext.IsEditor)
            return "Editor";

        return null;
    }

    /// <summary>
    /// Maps a Page entity to a PageDto for use with existing authorization methods.
    /// Note: Content is not included as it's not needed for authorization checks.
    /// </summary>
    private static PageDto MapToPageDto(Page page)
    {
        return new PageDto
        {
            Id = page.Id,
            Title = page.Title,
            Slug = page.Slug,
            IsLocked = page.IsLocked,
            IsDeleted = page.IsDeleted,
            Visibility = page.Visibility,
            CreatedBy = page.CreatedBy,
            CreatedOn = page.CreatedOn,
            ModifiedBy = page.ModifiedBy,
            ModifiedOn = page.ModifiedOn
        };
    }
}
