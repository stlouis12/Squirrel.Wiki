using Microsoft.EntityFrameworkCore;
using Squirrel.Wiki.Core.Database.Entities;
using FileEntity = Squirrel.Wiki.Core.Database.Entities.File;

namespace Squirrel.Wiki.Core.Database.Repositories;

/// <summary>
/// Repository implementation for file operations
/// </summary>
public class FileRepository : Repository<FileEntity, Guid>, IFileRepository
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
        return await _dbSet
            .Where(f => !f.IsDeleted && 
                (EF.Functions.Like(f.FileName, $"%{searchTerm}%") ||
                 (f.Description != null && EF.Functions.Like(f.Description, $"%{searchTerm}%"))))
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
        IEnumerable<Guid> ids, 
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(f => ids.Contains(f.Id))
            .Include(f => f.Folder)
            .Include(f => f.Content)
            .ToListAsync(cancellationToken);
    }

    public override async Task<FileEntity?> GetByIdAsync(
        Guid id, 
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(f => f.Folder)
            .Include(f => f.Content)
            .Include(f => f.Versions)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public async Task AddFileContentAsync(
        FileContent fileContent, 
        CancellationToken cancellationToken = default)
    {
        await _context.Set<FileContent>().AddAsync(fileContent, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<FileContent?> GetFileContentAsync(
        string fileHash, 
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<FileContent>()
            .FirstOrDefaultAsync(fc => fc.FileHash == fileHash, cancellationToken);
    }

    public async Task IncrementReferenceCountAsync(
        string fileHash, 
        CancellationToken cancellationToken = default)
    {
        var fileContent = await _context.Set<FileContent>()
            .FirstOrDefaultAsync(fc => fc.FileHash == fileHash, cancellationToken);
        
        if (fileContent != null)
        {
            fileContent.ReferenceCount++;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
