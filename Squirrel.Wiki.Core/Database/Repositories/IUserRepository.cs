using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Database.Repositories;

/// <summary>
/// Repository interface for User operations
/// </summary>
public interface IUserRepository : IRepository<User, Guid>
{
    /// <summary>
    /// Gets a user by their external ID (from external authentication provider)
    /// </summary>
    Task<User?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a user by their email address
    /// </summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a user by their username
    /// </summary>
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all admin users
    /// </summary>
    Task<IEnumerable<User>> GetAllAdminsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all editor users
    /// </summary>
    Task<IEnumerable<User>> GetAllEditorsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates the last login timestamp for a user
    /// </summary>
    Task UpdateLastLoginAsync(Guid userId, DateTime lastLogin, CancellationToken cancellationToken = default);
}
