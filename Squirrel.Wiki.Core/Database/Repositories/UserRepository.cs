using Microsoft.EntityFrameworkCore;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Repositories;

/// <summary>
/// Repository implementation for User operations
/// </summary>
public class UserRepository : Repository<User, Guid>, IUserRepository
{
    public UserRepository(SquirrelDbContext context) : base(context)
    {
    }

    public async Task<User?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(u => u.ExternalId == externalId, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower(), cancellationToken);
    }

    public async Task<IEnumerable<User>> GetAllAdminsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(u => u.IsAdmin)
            .OrderBy(u => u.Username)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<User>> GetAllEditorsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(u => u.IsEditor)
            .OrderBy(u => u.Username)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateLastLoginAsync(Guid userId, DateTime lastLogin, CancellationToken cancellationToken = default)
    {
        var user = await GetByIdAsync(userId, cancellationToken);
        if (user != null)
        {
            user.LastLoginOn = lastLogin;
            await UpdateAsync(user, cancellationToken);
        }
    }
}
