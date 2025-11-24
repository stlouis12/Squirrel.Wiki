using Microsoft.EntityFrameworkCore;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Exceptions;

namespace Squirrel.Wiki.Core.Database.Repositories;

/// <summary>
/// Repository implementation for Page operations
/// </summary>
public class PageRepository : Repository<Page, int>, IPageRepository
{
    public PageRepository(SquirrelDbContext context) : base(context)
    {
    }

    public async Task<Page?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.Category)
            .Include(p => p.PageTags)
                .ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(p => p.Slug == slug, cancellationToken);
    }

    public async Task<Page?> GetByTitleAsync(string title, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.Category)
            .Include(p => p.PageTags)
                .ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(p => EF.Functions.Like(p.Title, title), cancellationToken);
    }

    public async Task<IEnumerable<Page>> GetByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.Category)
            .Include(p => p.PageTags)
                .ThenInclude(pt => pt.Tag)
            .Where(p => p.CategoryId == categoryId && !p.IsDeleted)
            .OrderBy(p => p.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Page>> GetByCreatedByAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.Category)
            .Where(p => p.CreatedBy == username && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Page>> GetByModifiedByAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.Category)
            .Where(p => p.ModifiedBy == username && !p.IsDeleted)
            .OrderByDescending(p => p.ModifiedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Page>> GetByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.Category)
            .Include(p => p.PageTags)
                .ThenInclude(pt => pt.Tag)
            .Where(p => p.PageTags.Any(pt => pt.Tag.Name == tag) && !p.IsDeleted)
            .OrderBy(p => p.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Page>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.Category)
            .Include(p => p.PageTags)
                .ThenInclude(pt => pt.Tag)
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Page>> GetAllDeletedAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.Category)
            .Where(p => p.IsDeleted)
            .OrderByDescending(p => p.ModifiedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Page>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var lowerSearchTerm = searchTerm.ToLower();
        
        return await _dbSet
            .Include(p => p.Category)
            .Include(p => p.PageTags)
                .ThenInclude(pt => pt.Tag)
            .Where(p => !p.IsDeleted && 
                (EF.Functions.Like(p.Title.ToLower(), $"%{lowerSearchTerm}%") ||
                 EF.Functions.Like(p.Slug.ToLower(), $"%{lowerSearchTerm}%")))
            .OrderBy(p => p.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<PageContent?> GetLatestContentAsync(int pageId, CancellationToken cancellationToken = default)
    {
        return await _context.PageContents
            .Where(pc => pc.PageId == pageId)
            .OrderByDescending(pc => pc.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<PageContent>> GetAllContentVersionsAsync(int pageId, CancellationToken cancellationToken = default)
    {
        return await _context.PageContents
            .Where(pc => pc.PageId == pageId)
            .OrderByDescending(pc => pc.VersionNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<PageContent?> GetContentByVersionAsync(int pageId, int versionNumber, CancellationToken cancellationToken = default)
    {
        return await _context.PageContents
            .FirstOrDefaultAsync(pc => pc.PageId == pageId && pc.VersionNumber == versionNumber, cancellationToken);
    }

    public async Task<PageContent> AddContentVersionAsync(PageContent content, CancellationToken cancellationToken = default)
    {
        await _context.PageContents.AddAsync(content, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return content;
    }

    public async Task UpdateContentVersionAsync(PageContent content, CancellationToken cancellationToken = default)
    {
        _context.PageContents.Update(content);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDeleteAsync(int pageId, CancellationToken cancellationToken = default)
    {
        var page = await GetByIdAsync(pageId, cancellationToken);
        if (page != null)
        {
            page.IsDeleted = true;
            page.ModifiedOn = DateTime.UtcNow;
            await UpdateAsync(page, cancellationToken);
        }
    }

    public async Task RestoreAsync(int pageId, CancellationToken cancellationToken = default)
    {
        var page = await _dbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == pageId, cancellationToken);
            
        if (page != null)
        {
            page.IsDeleted = false;
            page.ModifiedOn = DateTime.UtcNow;
            await UpdateAsync(page, cancellationToken);
        }
    }

    public async Task<IEnumerable<Page>> GetByAuthorAsync(string authorUsername, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.Category)
            .Where(p => p.CreatedBy == authorUsername && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<PageContent>> GetContentHistoryByAuthorAsync(string authorUsername, CancellationToken cancellationToken = default)
    {
        return await _context.PageContents
            .Where(pc => pc.EditedBy == authorUsername)
            .OrderByDescending(pc => pc.EditedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Page>> GetByCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        return await GetByCategoryIdAsync(categoryId, cancellationToken);
    }

    public async Task UpdateTagsAsync(int pageId, IEnumerable<string> tagNames, CancellationToken cancellationToken = default)
    {
        var page = await _dbSet
            .Include(p => p.PageTags)
            .FirstOrDefaultAsync(p => p.Id == pageId, cancellationToken);

        if (page == null)
        {
            throw new EntityNotFoundException("Page", pageId);
        }

        // Remove existing tags
        _context.Set<PageTag>().RemoveRange(page.PageTags);

        // Add new tags
        foreach (var tagName in tagNames)
        {
            var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == tagName, cancellationToken);
            if (tag == null)
            {
                tag = new Tag { Name = tagName };
                await _context.Tags.AddAsync(tag, cancellationToken);
            }

            page.PageTags.Add(new PageTag { Page = page, Tag = tag });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public override async Task<Page?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.Category)
            .Include(p => p.PageTags)
                .ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<List<Page>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        if (!idList.Any())
        {
            return new List<Page>();
        }
        
        return await _dbSet
            .Include(p => p.Category)
            .Include(p => p.PageTags)
                .ThenInclude(pt => pt.Tag)
            .Where(p => idList.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }
}
