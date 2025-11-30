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
    /// Checks if the current user can view multiple pages in a single batch operation.
    /// Returns a dictionary mapping page ID to whether the user can view that page.
    /// This is more efficient than calling CanViewPageAsync multiple times.
    /// </summary>
    Task<Dictionary<int, bool>> CanViewPagesAsync(IEnumerable<Page> pages, CancellationToken cancellationToken = default);
    
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
    
    /// <summary>
    /// Checks if the current user can view a file based on its visibility and global settings
    /// </summary>
    Task<bool> CanViewFileAsync(Database.Entities.File file, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if the current user can upload files
    /// </summary>
    Task<bool> CanUploadFileAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if the current user can edit a file
    /// </summary>
    Task<bool> CanEditFileAsync(Database.Entities.File file, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if the current user can delete a file
    /// </summary>
    Task<bool> CanDeleteFileAsync(Database.Entities.File file, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if the current user can manage folders (create, edit, delete, move)
    /// </summary>
    Task<bool> CanManageFoldersAsync();

    /// <summary>
    /// Batch authorization check for viewing multiple files
    /// Returns a dictionary mapping file IDs to whether the user can view them
    /// </summary>
    Task<Dictionary<Guid, bool>> CanViewFilesAsync(IEnumerable<Database.Entities.File> files);

    /// <summary>
    /// Batch authorization check for editing multiple files
    /// Returns a dictionary mapping file IDs to whether the user can edit them
    /// </summary>
    Task<Dictionary<Guid, bool>> CanEditFilesAsync(IEnumerable<Database.Entities.File> files);

    /// <summary>
    /// Batch authorization check for deleting multiple files
    /// Returns a dictionary mapping file IDs to whether the user can delete them
    /// </summary>
    Task<Dictionary<Guid, bool>> CanDeleteFilesAsync(IEnumerable<Database.Entities.File> files);
}
