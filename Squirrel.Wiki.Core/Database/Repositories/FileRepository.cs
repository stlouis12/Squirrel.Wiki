using Microsoft.EntityFrameworkCore;
using FileEntity = Squirrel.Wiki.Core.Database.Entities.File;

namespace Squirrel.Wiki.Core.Database.Repositories;

/// <summary>
/// Repository implementation for file operations
/// </summary>
public class FileRepository : Repository<FileEntity, int>, IFileRepository
{
    public FileRepository(SquirrelDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<FileEntity>> GetByFolderAsync(
        int? folderId, 
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(f => f.FolderId == folderId && !f.IsDeleted)
            .Include(f => f.Folder)
            .Include(f => f.Content)
            .OrderBy(f => f.FileName)
            .ToListAsync(cancellationToken);
    }

    public async Task<FileEntity?> GetByPathAsync(
        string path, 
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(f => f.Folder)
            .Include(f => f.Content)
            .FirstOrDefaultAsync(f => f.FilePath == path && !f.IsDeleted, cancellationToken);
    }

    public async Task<IEnumerable<FileEntity>> GetByHashAsync(
        string fileHash, 
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(f => f.FileHash == fileHash && !f.IsDeleted)
            .Include(f => f.Folder)
            .Include(f => f.Content)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountByFolderAsync(
        int folderId, 
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .CountAsync(f => f.FolderId == folderId && !f.IsDeleted, cancellationToken);
    }

    public async Task<IEnumerable<FileEntity>> SearchAsync(
        string searchTerm, 
        CancellationToken cancellationToken = default)
    {
        var lowerSearchTerm = searchTerm.ToLower();
        
        return await _dbSet
            .Where(f => !f.IsDeleted && 
                (f.FileName.ToLower().Contains(lowerSearchTerm) ||
                 (f.Description != null && f.Description.ToLower().Contains(lowerSearchTerm))))
            .Include(f => f.Folder)
            .Include(f => f.Content)
            .OrderBy(f => f.FileName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<FileEntity>> GetByStorageProviderAsync(
        string provider, 
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(f => f.StorageProvider == provider && !f.IsDeleted)
            .Include(f => f.Folder)
            .Include(f => f.Content)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<FileEntity>> GetByIdsAsync(
        IEnumerable<int> ids, 
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(f => ids.Contains(f.Id))
            .Include(f => f.Folder)
            .Include(f => f.Content)
            .ToListAsync(cancellationToken);
    }

    public override async Task<FileEntity?> GetByIdAsync(
        int id, 
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(f => f.Folder)
            .Include(f => f.Content)
            .Include(f => f.Versions)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }
}
