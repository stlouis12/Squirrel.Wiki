using FileEntity = Squirrel.Wiki.Core.Database.Entities.File;

namespace Squirrel.Wiki.Core.Database.Repositories;

/// <summary>
/// Repository interface for file operations
/// </summary>
public interface IFileRepository : IRepository<FileEntity, int>
{
    Task<IEnumerable<FileEntity>> GetByFolderAsync(int? folderId, CancellationToken cancellationToken = default);
    
    Task<FileEntity?> GetByPathAsync(string path, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<FileEntity>> GetByHashAsync(string fileHash, CancellationToken cancellationToken = default);
    
    Task<int> GetCountByFolderAsync(int folderId, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<FileEntity>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<FileEntity>> GetByStorageProviderAsync(string provider, CancellationToken cancellationToken = default);
}
