using Microsoft.EntityFrameworkCore;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Repositories;

/// <summary>
/// Repository implementation for folder operations
/// </summary>
public class FolderRepository : Repository<Folder, int>, IFolderRepository
{
    public FolderRepository(SquirrelDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Folder>> GetRootFoldersAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(f => f.ParentFolderId == null && !f.IsDeleted)
            .OrderBy(f => f.DisplayOrder)
            .ThenBy(f => f.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Folder>> GetChildrenAsync(
        int? parentId, 
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(f => f.ParentFolderId == parentId && !f.IsDeleted)
            .OrderBy(f => f.DisplayOrder)
            .ThenBy(f => f.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Folder?> GetBySlugAsync(
        string slug, 
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(f => f.ParentFolder)
            .Include(f => f.SubFolders)
            .FirstOrDefaultAsync(f => f.Slug == slug && !f.IsDeleted, cancellationToken);
    }

    public async Task<IEnumerable<Folder>> GetByParentIdAsync(
        int parentId, 
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(f => f.ParentFolderId == parentId && !f.IsDeleted)
            .OrderBy(f => f.DisplayOrder)
            .ThenBy(f => f.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasChildrenAsync(
        int folderId, 
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AnyAsync(f => f.ParentFolderId == folderId && !f.IsDeleted, cancellationToken);
    }

    public async Task<bool> HasFilesAsync(
        int folderId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.Files
            .AnyAsync(f => f.FolderId == folderId && !f.IsDeleted, cancellationToken);
    }

    public override async Task<Folder?> GetByIdAsync(
        int id, 
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(f => f.ParentFolder)
            .Include(f => f.SubFolders.Where(sf => !sf.IsDeleted))
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }
}
