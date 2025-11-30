using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Services.Files;

/// <summary>
/// Service interface for folder management operations
/// </summary>
public interface IFolderService
{
    /// <summary>
    /// Creates a new folder
    /// </summary>
    Task<Result<FolderDto>> CreateFolderAsync(FolderCreateDto createDto, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a folder by ID
    /// </summary>
    Task<Result<FolderDto>> GetFolderByIdAsync(int folderId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a folder by slug
    /// </summary>
    Task<Result<FolderDto>> GetFolderBySlugAsync(string slug, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets root folders (folders with no parent)
    /// </summary>
    Task<Result<List<FolderDto>>> GetRootFoldersAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets child folders of a parent folder
    /// </summary>
    Task<Result<List<FolderDto>>> GetChildFoldersAsync(int? parentFolderId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the complete folder tree structure
    /// </summary>
    Task<Result<List<FolderTreeDto>>> GetFolderTreeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates folder metadata
    /// </summary>
    Task<Result<FolderDto>> UpdateFolderAsync(int folderId, FolderUpdateDto updateDto, string updatedBy, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Moves a folder to a new parent
    /// </summary>
    Task<Result<FolderDto>> MoveFolderAsync(int folderId, int? newParentFolderId, string movedBy, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a folder (soft delete)
    /// </summary>
    Task<Result> DeleteFolderAsync(int folderId, string deletedBy, bool recursive = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the breadcrumb path for a folder
    /// </summary>
    Task<Result<List<FolderDto>>> GetFolderBreadcrumbAsync(int folderId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates folder depth to prevent excessive nesting
    /// </summary>
    Task<Result<int>> GetFolderDepthAsync(int folderId, CancellationToken cancellationToken = default);
}
