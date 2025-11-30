using Squirrel.Wiki.Core.Database.Entities;
using FileEntity = Squirrel.Wiki.Core.Database.Entities.File;

namespace Squirrel.Wiki.Core.Database.Repositories;

/// <summary>
/// Repository interface for file operations
/// </summary>
public interface IFileRepository : IRepository<FileEntity, Guid>
{
    Task<IEnumerable<FileEntity>> GetByFolderAsync(int? folderId, CancellationToken cancellationToken = default);
    
    Task<FileEntity?> GetByPathAsync(string path, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<FileEntity>> GetByHashAsync(string fileHash, CancellationToken cancellationToken = default);
    
    Task<int> GetCountByFolderAsync(int folderId, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<FileEntity>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<FileEntity>> GetByStorageProviderAsync(string provider, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<FileEntity>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    
    Task AddFileContentAsync(FileContent fileContent, CancellationToken cancellationToken = default);
    
    Task<FileContent?> GetFileContentAsync(string fileHash, CancellationToken cancellationToken = default);
    
    Task IncrementReferenceCountAsync(string fileHash, CancellationToken cancellationToken = default);
}
