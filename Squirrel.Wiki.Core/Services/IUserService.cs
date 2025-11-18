using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Service interface for user management operations
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Gets a user by their ID
    /// </summary>
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by their username
    /// </summary>
    Task<UserDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by their email address
    /// </summary>
    Task<UserDto?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by their external ID (OIDC subject identifier)
    /// </summary>
    Task<UserDto?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all users
    /// </summary>
    Task<IEnumerable<UserDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all admin users
    /// </summary>
    Task<IEnumerable<UserDto>> GetAdminsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all editor users
    /// </summary>
    Task<IEnumerable<UserDto>> GetEditorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new user
    /// </summary>
    Task<UserDto> CreateAsync(UserCreateDto createDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user
    /// </summary>
    Task<UserDto> UpdateAsync(Guid id, UserUpdateDto updateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes a user from OIDC provider
    /// </summary>
    Task<UserDto> SyncFromOidcAsync(OidcUserDto oidcUser, CancellationToken cancellationToken = default);

    /// <summary>
    /// Promotes a user to admin
    /// </summary>
    Task PromoteToAdminAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Demotes a user from admin
    /// </summary>
    Task DemoteFromAdminAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Promotes a user to editor
    /// </summary>
    Task PromoteToEditorAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Demotes a user from editor
    /// </summary>
    Task DemoteFromEditorAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a username is available
    /// </summary>
    Task<bool> IsUsernameAvailableAsync(string username, Guid? excludeUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an email is available
    /// </summary>
    Task<bool> IsEmailAvailableAsync(string email, Guid? excludeUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates user's last login time
    /// </summary>
    Task UpdateLastLoginAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets user statistics (page count, edit count, etc.)
    /// </summary>
    Task<UserStatsDto> GetUserStatsAsync(Guid id, CancellationToken cancellationToken = default);
}
