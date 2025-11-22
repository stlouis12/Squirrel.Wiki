using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Security;

/// <summary>
/// Service for handling authorization checks
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Checks if the current user can view a page based on its visibility and global settings
    /// </summary>
    Task<bool> CanViewPageAsync(Page page, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if the current user can edit a specific page (considers page locking)
    /// </summary>
    Task<bool> CanEditPageAsync(Core.Models.PageDto page, string? username, string? userRole);
    
    /// <summary>
    /// Checks if a user with the given role can delete a specific page (considers page locking)
    /// </summary>
    Task<bool> CanDeletePageAsync(Core.Models.PageDto page, string? userRole);
    
    /// <summary>
    /// Checks if the current user is an administrator
    /// </summary>
    bool IsAdmin();
    
    /// <summary>
    /// Checks if the current user is authenticated
    /// </summary>
    bool IsAuthenticated();
}
