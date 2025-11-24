using AutoMapper;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Authentication;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Exceptions;
using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Service implementation for user management operations
/// </summary>
public class UserService : BaseService, IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPageRepository _pageRepository;
    private readonly ISettingsService _settingsService;

    public UserService(
        IUserRepository userRepository,
        IPageRepository pageRepository,
        ISettingsService settingsService,
        ILogger<UserService> logger,
        ICacheService cache,
        ICacheInvalidationService cacheInvalidation,
        IMapper mapper)
        : base(logger, cache, cacheInvalidation, mapper)
    {
        _userRepository = userRepository;
        _pageRepository = pageRepository;
        _settingsService = settingsService;
    }

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        return user != null ? Mapper.Map<UserDto>(user) : null;
    }

    public async Task<UserDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByUsernameAsync(username, cancellationToken);
        return user != null ? Mapper.Map<UserDto>(user) : null;
    }

    public async Task<UserDto?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
        return user != null ? Mapper.Map<UserDto>(user) : null;
    }

    public async Task<UserDto?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByExternalIdAsync(externalId, cancellationToken);
        return user != null ? Mapper.Map<UserDto>(user) : null;
    }

    public async Task<IEnumerable<UserDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);
        return Mapper.Map<IEnumerable<UserDto>>(users);
    }

    public async Task<IEnumerable<UserDto>> GetAdminsAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);
        return Mapper.Map<IEnumerable<UserDto>>(users.Where(u => u.IsAdmin));
    }

    public async Task<IEnumerable<UserDto>> GetEditorsAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);
        return Mapper.Map<IEnumerable<UserDto>>(users.Where(u => u.IsEditor));
    }

    public async Task<UserDto> CreateAsync(UserCreateDto createDto, CancellationToken cancellationToken = default)
    {
        // Validate username availability
        var existingByUsername = await _userRepository.GetByUsernameAsync(createDto.Username, cancellationToken);
        if (existingByUsername != null)
        {
            throw BusinessRuleException.UsernameAlreadyExists(createDto.Username);
        }

        // Validate email availability
        var existingByEmail = await _userRepository.GetByEmailAsync(createDto.Email, cancellationToken);
        if (existingByEmail != null)
        {
            throw BusinessRuleException.EmailAlreadyExists(createDto.Email);
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

        LogInfo("Created user {Username} with ID {UserId}", user.Username, user.Id);

        return Mapper.Map<UserDto>(user);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UserUpdateDto updateDto, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            throw new EntityNotFoundException("User", id);
        }

        // Validate email availability if changed
        if (user.Email != updateDto.Email)
        {
            var existingByEmail = await _userRepository.GetByEmailAsync(updateDto.Email, cancellationToken);
            if (existingByEmail != null && existingByEmail.Id != id)
            {
                throw BusinessRuleException.EmailAlreadyExists(updateDto.Email);
            }
        }

        user.Email = updateDto.Email;
        user.DisplayName = updateDto.DisplayName;
        user.FirstName = updateDto.FirstName;
        user.LastName = updateDto.LastName;
        user.IsAdmin = updateDto.IsAdmin;
        user.IsEditor = updateDto.IsEditor;

        await _userRepository.UpdateAsync(user, cancellationToken);

        LogInfo("Updated user {Username} (ID: {UserId})", user.Username, user.Id);

        return Mapper.Map<UserDto>(user);
    }

    public async Task PromoteToAdminAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            throw new EntityNotFoundException("User", id);
        }

        user.IsAdmin = true;
        await _userRepository.UpdateAsync(user, cancellationToken);

        LogInfo("Promoted user {Username} (ID: {UserId}) to admin", user.Username, user.Id);
    }

    public async Task DemoteFromAdminAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            throw new EntityNotFoundException("User", id);
        }

        user.IsAdmin = false;
        await _userRepository.UpdateAsync(user, cancellationToken);

        LogInfo("Demoted user {Username} (ID: {UserId}) from admin", user.Username, user.Id);
    }

    public async Task PromoteToEditorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            throw new EntityNotFoundException("User", id);
        }

        user.IsEditor = true;
        await _userRepository.UpdateAsync(user, cancellationToken);

        LogInfo("Promoted user {Username} (ID: {UserId}) to editor", user.Username, user.Id);
    }

    public async Task DemoteFromEditorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            throw new EntityNotFoundException("User", id);
        }

        user.IsEditor = false;
        await _userRepository.UpdateAsync(user, cancellationToken);

        LogInfo("Demoted user {Username} (ID: {UserId}) from editor", user.Username, user.Id);
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
            throw new EntityNotFoundException("User", id);
        }

        user.LastLoginOn = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);
    }

    public async Task<UserStatsDto> GetUserStatsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            throw new EntityNotFoundException("User", id);
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
        var editDates = editHistory.Select(pc => pc.EditedOn).ToList();
        DateTime? lastEditDate = editDates.Any() ? (DateTime?)editDates.Max() : null;

        return new UserStatsDto
        {
            UserId = user.Id,
            PagesCreated = pagesCreated,
            PagesEdited = pagesEdited,
            TotalEdits = totalEdits,
            LastEditDate = lastEditDate
        };
    }

    // ============================================================================
    // Local Authentication Methods
    // ============================================================================

    public async Task<UserDto?> AuthenticateAsync(string usernameOrEmail, string password, CancellationToken cancellationToken = default)
    {
        // Try to find user by email or username
        var users = await _userRepository.GetAllAsync(cancellationToken);
        var user = users.FirstOrDefault(u => 
            u.Email.Equals(usernameOrEmail, StringComparison.OrdinalIgnoreCase) || 
            u.Username.Equals(usernameOrEmail, StringComparison.OrdinalIgnoreCase));

        if (user == null)
        {
            LogWarning("Authentication failed: User not found for {UsernameOrEmail}", usernameOrEmail);
            return null;
        }

        // Only local users can authenticate with password
        if (user.Provider != AuthenticationProvider.Local)
        {
            LogWarning("Authentication failed: User {Username} is not a local user", user.Username);
            return null;
        }

        // Check if password hash exists
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            LogWarning("Authentication failed: No password set for user {Username}", user.Username);
            return null;
        }

        // Verify password
        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

        if (!isPasswordValid)
        {
            // Get max login attempts from settings (default to 5 if not set)
            var maxLoginAttempts = await _settingsService.GetSettingAsync<int>("MaxLoginAttempts", cancellationToken);
            if (maxLoginAttempts <= 0)
            {
                maxLoginAttempts = 5; // Default fallback
            }

            // Increment failed login attempts
            user.FailedLoginAttempts++;
            
            // Lock account after max failed attempts
            if (user.FailedLoginAttempts >= maxLoginAttempts)
            {
                // Get account lock duration from settings (default to 30 minutes if not set)
                var lockDurationMinutes = await _settingsService.GetSettingAsync<int>("AccountLockDurationMinutes", cancellationToken);
                if (lockDurationMinutes <= 0)
                {
                    lockDurationMinutes = 30; // Default fallback
                }

                user.IsLocked = true;
                user.LockedUntil = DateTime.UtcNow.AddMinutes(lockDurationMinutes);
                await _userRepository.UpdateAsync(user, cancellationToken);
                
                LogWarning("Account locked for user {Username} due to {Attempts} failed login attempts. Locked until {LockedUntil}", 
                    user.Username, user.FailedLoginAttempts, user.LockedUntil);
                throw new BusinessRuleException(
                    $"Account has been locked due to multiple failed login attempts. Please try again after {user.LockedUntil:yyyy-MM-dd HH:mm}.",
                    "ACCOUNT_LOCKED"
                ).WithContext("Username", user.Username)
                 .WithContext("LockedUntil", user.LockedUntil);
            }
            
            await _userRepository.UpdateAsync(user, cancellationToken);
            LogWarning("Authentication failed: Invalid password for user {Username} (Attempt {Attempts}/{MaxAttempts})", 
                user.Username, user.FailedLoginAttempts, maxLoginAttempts);
            return null;
        }

        // Reset failed login attempts on successful authentication
        user.FailedLoginAttempts = 0;
        user.LastLoginOn = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);

        LogInfo("User {Username} authenticated successfully", user.Username);
        return Mapper.Map<UserDto>(user);
    }

    public async Task<UserDto> CreateLocalUserAsync(string username, string email, string password, string displayName, bool isAdmin = false, bool isEditor = false, CancellationToken cancellationToken = default)
    {
        // Validate password
        var passwordValidation = await ValidatePasswordAsync(password);
        if (!passwordValidation.IsValid)
        {
            var errors = passwordValidation.Errors.Select(e => new ValidationError("Password", e)).ToList();
            throw new ValidationException(errors);
        }

        // Validate username availability
        var existingByUsername = await _userRepository.GetByUsernameAsync(username, cancellationToken);
        if (existingByUsername != null)
        {
            throw BusinessRuleException.UsernameAlreadyExists(username);
        }

        // Validate email availability
        var existingByEmail = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (existingByEmail != null)
        {
            throw BusinessRuleException.EmailAlreadyExists(email);
        }

        // Create user with hashed password
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            DisplayName = displayName,
            Provider = AuthenticationProvider.Local,
            PasswordHash = HashPassword(password),
            IsAdmin = isAdmin,
            IsEditor = isEditor,
            IsActive = true,
            IsLocked = false,
            FailedLoginAttempts = 0,
            CreatedOn = DateTime.UtcNow,
            LastPasswordChangeOn = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user, cancellationToken);

        LogInfo("Created local user {Username} with ID {UserId}", user.Username, user.Id);

        return Mapper.Map<UserDto>(user);
    }

    public async Task SetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new EntityNotFoundException("User", userId);
        }

        if (user.Provider != AuthenticationProvider.Local)
        {
            throw new BusinessRuleException(
                "Cannot set password for non-local authentication users.",
                "INVALID_PROVIDER"
            ).WithContext("Provider", user.Provider.ToString());
        }

        // Validate password
        var passwordValidation = await ValidatePasswordAsync(newPassword);
        if (!passwordValidation.IsValid)
        {
            var errors = passwordValidation.Errors.Select(e => new ValidationError("Password", e)).ToList();
            throw new ValidationException(errors);
        }

        user.PasswordHash = HashPassword(newPassword);
        user.LastPasswordChangeOn = DateTime.UtcNow;
        user.PasswordResetToken = null;
        user.PasswordResetExpiry = null;

        await _userRepository.UpdateAsync(user, cancellationToken);

        LogInfo("Password changed for user {Username} (ID: {UserId})", user.Username, user.Id);
    }

    public Task<PasswordValidationResult> ValidatePasswordAsync(string password)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("Password is required.");
        }
        else
        {
            if (password.Length < 8)
            {
                errors.Add("Password must be at least 8 characters long.");
            }

            if (password.Length > 128)
            {
                errors.Add("Password must not exceed 128 characters.");
            }

            if (!password.Any(char.IsUpper))
            {
                errors.Add("Password must contain at least one uppercase letter.");
            }

            if (!password.Any(char.IsLower))
            {
                errors.Add("Password must contain at least one lowercase letter.");
            }

            if (!password.Any(char.IsDigit))
            {
                errors.Add("Password must contain at least one digit.");
            }

            if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
            {
                errors.Add("Password must contain at least one special character.");
            }
        }

        return Task.FromResult(errors.Any() 
            ? PasswordValidationResult.Failed(errors.ToArray())
            : PasswordValidationResult.Success());
    }

    public async Task<string> InitiatePasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (user == null)
        {
            // Don't reveal that the email doesn't exist
            LogWarning("Password reset requested for non-existent email: {Email}", email);
            return string.Empty;
        }

        if (user.Provider != AuthenticationProvider.Local)
        {
            LogWarning("Password reset requested for non-local user: {Username}", user.Username);
            return string.Empty;
        }

        // Generate reset token
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        token = token.Replace("+", "").Replace("/", "").Replace("=", "");

        user.PasswordResetToken = token;
        user.PasswordResetExpiry = DateTime.UtcNow.AddHours(24);

        await _userRepository.UpdateAsync(user, cancellationToken);

        LogInfo("Password reset initiated for user {Username} (ID: {UserId})", user.Username, user.Id);

        return token;
    }

    public async Task<bool> CompletePasswordResetAsync(string token, string newPassword, CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);
        var user = users.FirstOrDefault(u => u.PasswordResetToken == token);

        if (user == null)
        {
            LogWarning("Invalid password reset token attempted");
            return false;
        }

        if (user.PasswordResetExpiry == null || user.PasswordResetExpiry < DateTime.UtcNow)
        {
            LogWarning("Expired password reset token attempted for user {Username}", user.Username);
            return false;
        }

        // Validate password
        var passwordValidation = await ValidatePasswordAsync(newPassword);
        if (!passwordValidation.IsValid)
        {
            LogWarning("Password reset failed validation for user {Username}", user.Username);
            return false;
        }

        user.PasswordHash = HashPassword(newPassword);
        user.LastPasswordChangeOn = DateTime.UtcNow;
        user.PasswordResetToken = null;
        user.PasswordResetExpiry = null;

        await _userRepository.UpdateAsync(user, cancellationToken);

        LogInfo("Password reset completed for user {Username} (ID: {UserId})", user.Username, user.Id);

        return true;
    }

    public async Task LockAccountAsync(Guid userId, DateTime? lockUntil = null, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new EntityNotFoundException("User", userId);
        }

        user.IsLocked = true;
        user.LockedUntil = lockUntil ?? DateTime.UtcNow.AddDays(30);

        await _userRepository.UpdateAsync(user, cancellationToken);

        LogInfo("Locked account for user {Username} (ID: {UserId}) until {LockedUntil}", 
            user.Username, user.Id, user.LockedUntil);
    }

    public async Task UnlockAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new EntityNotFoundException("User", userId);
        }

        user.IsLocked = false;
        user.LockedUntil = null;
        user.FailedLoginAttempts = 0;

        await _userRepository.UpdateAsync(user, cancellationToken);

        LogInfo("Unlocked account for user {Username} (ID: {UserId})", user.Username, user.Id);
    }

    public async Task ActivateAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new EntityNotFoundException("User", userId);
        }

        user.IsActive = true;

        await _userRepository.UpdateAsync(user, cancellationToken);

        LogInfo("Activated account for user {Username} (ID: {UserId})", user.Username, user.Id);
    }

    public async Task DeactivateAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new EntityNotFoundException("User", userId);
        }

        user.IsActive = false;

        await _userRepository.UpdateAsync(user, cancellationToken);

        LogInfo("Deactivated account for user {Username} (ID: {UserId})", user.Username, user.Id);
    }

    // ============================================================================
    // Private Helper Methods
    // ============================================================================

    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, 12);
    }
}
