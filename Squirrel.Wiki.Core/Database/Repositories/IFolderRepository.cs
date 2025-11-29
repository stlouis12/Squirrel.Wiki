using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Repositories;

/// <summary>
/// Repository interface for folder operations
/// </summary>
public interface IFolderRepository : IRepository<Folder, int>
{
    Task<IEnumerable<Folder>> GetRootFoldersAsync(CancellationToken cancellationToken = default);
    
    Task<IEnumerable<Folder>> GetChildrenAsync(int? parentId, CancellationToken cancellationToken = default);
    
    Task<Folder?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<Folder>> GetByParentIdAsync(int parentId, CancellationToken cancellationToken = default);
    
    Task<bool> HasChildrenAsync(int folderId, CancellationToken cancellationToken = default);
    
    Task<bool> HasFilesAsync(int folderId, CancellationToken cancellationToken = default);
}
