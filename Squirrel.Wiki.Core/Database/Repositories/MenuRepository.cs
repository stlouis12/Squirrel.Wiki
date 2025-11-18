using Microsoft.EntityFrameworkCore;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Repositories;

/// <summary>
/// Repository implementation for Menu operations
/// </summary>
public class MenuRepository : Repository<Menu, int>, IMenuRepository
{
    public MenuRepository(SquirrelDbContext context) : base(context)
    {
    }

    public async Task<Menu?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(m => m.Name.ToLower() == name.ToLower(), cancellationToken);
    }

    public async Task<Menu?> GetActiveByTypeAsync(MenuType menuType, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(m => m.MenuType == menuType && m.IsEnabled)
            .OrderBy(m => m.DisplayOrder)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<Menu>> GetByTypeAsync(MenuType menuType, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(m => m.MenuType == menuType)
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasActiveMenuOfTypeAsync(MenuType menuType, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(m => m.MenuType == menuType && m.IsEnabled);
        
        if (excludeId.HasValue)
        {
            query = query.Where(m => m.Id != excludeId.Value);
        }
        
        return await query.AnyAsync(cancellationToken);
    }

    public async Task<IEnumerable<Menu>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(m => m.IsEnabled)
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Menu>> GetAllOrderedAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task ReorderAsync(int menuId, int newDisplayOrder, CancellationToken cancellationToken = default)
    {
        var menu = await GetByIdAsync(menuId, cancellationToken);
        if (menu != null)
        {
            menu.DisplayOrder = newDisplayOrder;
            await UpdateAsync(menu, cancellationToken);
        }
    }
}
