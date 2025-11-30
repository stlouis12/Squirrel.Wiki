using Microsoft.EntityFrameworkCore;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Repositories;

/// <summary>
/// Repository implementation for Tag operations
/// </summary>
public class TagRepository : Repository<Tag, int>, ITagRepository
{
    public TagRepository(SquirrelDbContext context) : base(context)
    {
    }

    public async Task<Tag?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(t => t.Name.ToLower() == name.ToLower(), cancellationToken);
    }

    public async Task<IEnumerable<Tag>> GetAllOrderedAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<(Tag Tag, int PageCount)>> GetTagsWithCountsAsync(CancellationToken cancellationToken = default)
    {
        var tagsWithCounts = await _dbSet
            .Select(t => new
            {
                Tag = t,
                PageCount = t.PageTags.Count(pt => !pt.Page.IsDeleted)
            })
            .OrderBy(x => x.Tag.Name)
            .ToListAsync(cancellationToken);

        return tagsWithCounts.Select(x => (x.Tag, x.PageCount));
    }

    public async Task<IEnumerable<Tag>> GetPopularTagsAsync(int count, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .OrderByDescending(t => t.PageTags.Count(pt => !pt.Page.IsDeleted))
            .ThenBy(t => t.Name)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Tag>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var lowerSearchTerm = searchTerm.ToLower();
        
        return await _dbSet
            .Where(t => EF.Functions.Like(t.Name.ToLower(), $"%{lowerSearchTerm}%"))
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Tag> GetOrCreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.ToLowerInvariant();
        
        // First, try to find existing tag by normalized name
        var tag = await _dbSet
            .FirstOrDefaultAsync(t => t.NormalizedName == normalizedName, cancellationToken);
        
        if (tag == null)
        {
            // Create new tag
            tag = new Tag
            {
                Name = name,
                NormalizedName = normalizedName
            };
            
            try
            {
                tag = await AddAsync(tag, cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Handle race condition - another thread may have created the tag
                // Try to fetch it again
                tag = await _dbSet
                    .FirstOrDefaultAsync(t => t.NormalizedName == normalizedName, cancellationToken);
                
                if (tag == null)
                {
                    // If still null, rethrow the original exception
                    throw;
                }
            }
        }
        
        return tag;
    }

    public async Task DeleteUnusedTagsAsync(CancellationToken cancellationToken = default)
    {
        var unusedTags = await _dbSet
            .Where(t => !t.PageTags.Any())
            .ToListAsync(cancellationToken);

        foreach (var tag in unusedTags)
        {
            await DeleteAsync(tag, cancellationToken);
        }
    }

    public async Task<int> GetPageCountAsync(int tagId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<PageTag>()
            .CountAsync(pt => pt.TagId == tagId && !pt.Page.IsDeleted, cancellationToken);
    }

    public async Task<IEnumerable<Tag>> GetTagsForPageAsync(int pageId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<PageTag>()
            .Where(pt => pt.PageId == pageId)
            .Select(pt => pt.Tag)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Tag>> GetUnusedTagsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(t => !t.PageTags.Any())
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }
}
