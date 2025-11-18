using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Service implementation for user management operations
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPageRepository _pageRepository;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository userRepository,
        IPageRepository pageRepository,
        ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _pageRepository = pageRepository;
        _logger = logger;
    }

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        return user != null ? MapToDto(user) : null;
    }

    public async Task<UserDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByUsernameAsync(username, cancellationToken);
        return user != null ? MapToDto(user) : null;
    }

    public async Task<UserDto?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
        return user != null ? MapToDto(user) : null;
    }

    public async Task<UserDto?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByExternalIdAsync(externalId, cancellationToken);
        return user != null ? MapToDto(user) : null;
    }

    public async Task<IEnumerable<UserDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);
        return users.Select(MapToDto);
    }

    public async Task<IEnumerable<UserDto>> GetAdminsAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);
        return users.Where(u => u.IsAdmin).Select(MapToDto);
    }

    public async Task<IEnumerable<UserDto>> GetEditorsAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);
        return users.Where(u => u.IsEditor).Select(MapToDto);
    }

    public async Task<UserDto> CreateAsync(UserCreateDto createDto, CancellationToken cancellationToken = default)
    {
        // Validate username availability
        var existingByUsername = await _userRepository.GetByUsernameAsync(createDto.Username, cancellationToken);
        if (existingByUsername != null)
        {
            throw new InvalidOperationException($"Username '{createDto.Username}' is already taken.");
        }

        // Validate email availability
        var existingByEmail = await _userRepository.GetByEmailAsync(createDto.Email, cancellationToken);
        if (existingByEmail != null)
        {
            throw new InvalidOperationException($"Email '{createDto.Email}' is already registered.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = createDto.Username,
            Email = createDto.Email,
            DisplayName = createDto.DisplayName,
            IsAdmin = createDto.IsAdmin,
            IsEditor = createDto.IsEditor,
            CreatedOn = DateTime.UtcNow,
            LastLoginOn = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user, cancellationToken);

        _logger.LogInformation("Created user {Username} with ID {UserId}", user.Username, user.Id);

        return MapToDto(user);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UserUpdateDto updateDto, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {id} not found.");
        }

        // Validate email availability if changed
        if (user.Email != updateDto.Email)
        {
            var existingByEmail = await _userRepository.GetByEmailAsync(updateDto.Email, cancellationToken);
            if (existingByEmail != null && existingByEmail.Id != id)
            {
                throw new InvalidOperationException($"Email '{updateDto.Email}' is already registered.");
            }
        }

        user.Email = updateDto.Email;
        user.DisplayName = updateDto.DisplayName;
        user.IsAdmin = updateDto.IsAdmin;
        user.IsEditor = updateDto.IsEditor;

        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Updated user {Username} (ID: {UserId})", user.Username, user.Id);

        return MapToDto(user);
    }

    public async Task<UserDto> SyncFromOidcAsync(OidcUserDto oidcUser, CancellationToken cancellationToken = default)
    {
        // Try to find existing user by external ID
        var user = await _userRepository.GetByExternalIdAsync(oidcUser.ExternalId, cancellationToken);

        if (user == null)
        {
            // Create new user from OIDC data
            user = new User
            {
                Id = Guid.NewGuid(),
                ExternalId = oidcUser.ExternalId,
                Username = oidcUser.Username,
                Email = oidcUser.Email,
                DisplayName = oidcUser.DisplayName,
                IsAdmin = oidcUser.Groups.Contains("squirrel-admins"),
                IsEditor = oidcUser.Groups.Contains("squirrel-editors"),
                CreatedOn = DateTime.UtcNow,
                LastLoginOn = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user, cancellationToken);
            _logger.LogInformation("Created new user from OIDC: {Username} (External ID: {ExternalId})", 
                user.Username, user.ExternalId);
        }
        else
        {
            // Update existing user with latest OIDC data
            user.Email = oidcUser.Email;
            user.DisplayName = oidcUser.DisplayName;
            user.IsAdmin = oidcUser.Groups.Contains("squirrel-admins");
            user.IsEditor = oidcUser.Groups.Contains("squirrel-editors");
            user.LastLoginOn = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);
            _logger.LogInformation("Updated user from OIDC: {Username} (External ID: {ExternalId})", 
                user.Username, user.ExternalId);
        }

        return MapToDto(user);
    }

    public async Task PromoteToAdminAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {id} not found.");
        }

        user.IsAdmin = true;
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Promoted user {Username} (ID: {UserId}) to admin", user.Username, user.Id);
    }

    public async Task DemoteFromAdminAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {id} not found.");
        }

        user.IsAdmin = false;
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Demoted user {Username} (ID: {UserId}) from admin", user.Username, user.Id);
    }

    public async Task PromoteToEditorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {id} not found.");
        }

        user.IsEditor = true;
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Promoted user {Username} (ID: {UserId}) to editor", user.Username, user.Id);
    }

    public async Task DemoteFromEditorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {id} not found.");
        }

        user.IsEditor = false;
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Demoted user {Username} (ID: {UserId}) from editor", user.Username, user.Id);
    }

    public async Task<bool> IsUsernameAvailableAsync(string username, Guid? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByUsernameAsync(username, cancellationToken);
        
        if (user == null)
        {
            return true;
        }

        if (excludeUserId.HasValue && user.Id == excludeUserId.Value)
        {
            return true;
        }

        return false;
    }

    public async Task<bool> IsEmailAvailableAsync(string email, Guid? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
        
        if (user == null)
        {
            return true;
        }

        if (excludeUserId.HasValue && user.Id == excludeUserId.Value)
        {
            return true;
        }

        return false;
    }

    public async Task UpdateLastLoginAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {id} not found.");
        }

        user.LastLoginOn = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);
    }

    public async Task<UserStatsDto> GetUserStatsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {id} not found.");
        }

        // Get pages created by user
        var createdPages = await _pageRepository.GetByAuthorAsync(user.Username, cancellationToken);
        var pagesCreated = createdPages.Count();

        // Get all page content history by user
        var editHistory = await _pageRepository.GetContentHistoryByAuthorAsync(user.Username, cancellationToken);
        var totalEdits = editHistory.Count();

        // Count unique pages edited
        var pagesEdited = editHistory.Select(pc => pc.PageId).Distinct().Count();

        // Get last edit date
        var lastEditDate = editHistory.Any() 
            ? editHistory.Max(pc => pc.EditedOn) 
            : (DateTime?)null;

        return new UserStatsDto
        {
            UserId = user.Id,
            PagesCreated = pagesCreated,
            PagesEdited = pagesEdited,
            TotalEdits = totalEdits,
            LastEditDate = lastEditDate
        };
    }

    private static UserDto MapToDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            ExternalId = user.ExternalId,
            IsAdmin = user.IsAdmin,
            IsEditor = user.IsEditor,
            CreatedOn = user.CreatedOn,
            LastLoginOn = user.LastLoginOn
        };
    }
}
