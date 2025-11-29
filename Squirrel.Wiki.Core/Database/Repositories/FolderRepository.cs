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

    public async Task<string?> GetFolderPathAsync(
        int folderId, 
        CancellationToken cancellationToken = default)
    {
        var folder = await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted, cancellationToken);

        if (folder == null)
        {
            return null;
        }

        // Build path by traversing up the hierarchy
        var pathSegments = new List<string> { folder.Name };
        var currentParentId = folder.ParentFolderId;

        // Prevent infinite loops with a max depth
        const int maxDepth = 20;
        int depth = 0;

        while (currentParentId.HasValue && depth < maxDepth)
        {
            var parent = await _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == currentParentId.Value && !f.IsDeleted, cancellationToken);

            if (parent == null)
            {
                break;
            }

            pathSegments.Insert(0, parent.Name);
            currentParentId = parent.ParentFolderId;
            depth++;
        }

        return string.Join("/", pathSegments);
    }
}
