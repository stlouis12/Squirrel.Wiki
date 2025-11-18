using Microsoft.EntityFrameworkCore;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Repositories;

/// <summary>
/// Repository implementation for Category operations
/// </summary>
public class CategoryRepository : Repository<Category, int>, ICategoryRepository
{
    public CategoryRepository(SquirrelDbContext context) : base(context)
    {
    }

    public async Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(c => c.ParentCategory)
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Slug == slug, cancellationToken);
    }

    public async Task<IEnumerable<Category>> GetRootCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(c => c.SubCategories)
            .Where(c => c.ParentCategoryId == null)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Category>> GetChildCategoriesAsync(int parentCategoryId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(c => c.SubCategories)
            .Where(c => c.ParentCategoryId == parentCategoryId)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Category>> GetCategoryTreeAsync(CancellationToken cancellationToken = default)
    {
        // Get all categories with their relationships
        var allCategories = await _dbSet
            .Include(c => c.ParentCategory)
            .Include(c => c.SubCategories)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

        // Return only root categories - EF Core will populate the tree
        return allCategories.Where(c => c.ParentCategoryId == null);
    }

    public async Task<IEnumerable<Category>> GetCategoryPathAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        var path = new List<Category>();
        var category = await _dbSet
            .Include(c => c.ParentCategory)
            .FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);

        while (category != null)
        {
            path.Insert(0, category);
            if (category.ParentCategoryId.HasValue)
            {
                category = await _dbSet
                    .Include(c => c.ParentCategory)
                    .FirstOrDefaultAsync(c => c.Id == category.ParentCategoryId.Value, cancellationToken);
            }
            else
            {
                category = null;
            }
        }

        return path;
    }

    public async Task<IEnumerable<Category>> GetAllOrderedAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(c => c.ParentCategory)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasChildrenAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AnyAsync(c => c.ParentCategoryId == categoryId, cancellationToken);
    }

    public async Task<bool> HasPagesAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        return await _context.Pages
            .AnyAsync(p => p.CategoryId == categoryId && !p.IsDeleted, cancellationToken);
    }

    public async Task ReorderAsync(int categoryId, int newDisplayOrder, CancellationToken cancellationToken = default)
    {
        var category = await GetByIdAsync(categoryId, cancellationToken);
        if (category != null)
        {
            category.DisplayOrder = newDisplayOrder;
            await UpdateAsync(category, cancellationToken);
        }
    }

    public async Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(c => c.ParentCategory)
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Name == name, cancellationToken);
    }

    public async Task<IEnumerable<Category>> GetChildrenAsync(int parentId, CancellationToken cancellationToken = default)
    {
        return await GetChildCategoriesAsync(parentId, cancellationToken);
    }

    public async Task<IEnumerable<Category>> GetCategoriesByDepthAsync(int depth, CancellationToken cancellationToken = default)
    {
        // Calculate depth by counting parent relationships
        var allCategories = await _dbSet
            .Include(c => c.ParentCategory)
            .ToListAsync(cancellationToken);

        var categoriesAtDepth = new List<Category>();
        
        foreach (var category in allCategories)
        {
            var currentDepth = 0;
            var current = category;
            
            while (current.ParentCategoryId.HasValue)
            {
                currentDepth++;
                current = allCategories.FirstOrDefault(c => c.Id == current.ParentCategoryId.Value);
                if (current == null) break;
            }
            
            if (currentDepth == depth)
            {
                categoriesAtDepth.Add(category);
            }
        }

        return categoriesAtDepth.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name);
    }

    public async Task<int> GetPageCountAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        return await _context.Pages
            .CountAsync(p => p.CategoryId == categoryId && !p.IsDeleted, cancellationToken);
    }

    public override async Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(c => c.ParentCategory)
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }
}
