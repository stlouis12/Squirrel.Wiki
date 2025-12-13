using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Authentication;
using Squirrel.Wiki.Core.Database.Repositories;

namespace Squirrel.Wiki.Core.Security;

/// <summary>
/// Authentication strategy for local username/password authentication
/// </summary>
public class LocalAuthenticationStrategy : IAuthenticationStrategy
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<LocalAuthenticationStrategy> _logger;
    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 30;

    public AuthenticationProvider Provider => AuthenticationProvider.Local;

    public LocalAuthenticationStrategy(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ILogger<LocalAuthenticationStrategy> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public bool CanHandle(AuthenticationRequest request)
    {
        return request.Provider == AuthenticationProvider.Local &&
               !string.IsNullOrWhiteSpace(request.Username) &&
               !string.IsNullOrWhiteSpace(request.Password);
    }

    public async Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request, CancellationToken cancellationToken = default)
    {
        if (!CanHandle(request))
        {
            return AuthenticationResult.Failed("Invalid authentication request", AuthenticationFailureReason.InvalidCredentials);
        }

        var user = await _userRepository.GetByUsernameAsync(request.Username!, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("Authentication failed: User not found - {Username}", request.Username);
            return AuthenticationResult.Failed("Invalid username or password", AuthenticationFailureReason.AccountNotFound);
        }

        // Validate account eligibility
        var validationResult = ValidateAccountEligibility(user);
        if (validationResult != null)
        {
            return validationResult;
        }

        // Handle account lock status
        var lockCheckResult = await CheckAndHandleAccountLock(user, cancellationToken);
        if (lockCheckResult != null)
        {
            return lockCheckResult;
        }

        // Verify password
        var passwordValid = !string.IsNullOrWhiteSpace(user.PasswordHash) && 
                           _passwordHasher.VerifyPassword(request.Password!, user.PasswordHash);

        if (!passwordValid)
        {
            return await HandleFailedPasswordAttempt(user, request.Username!, cancellationToken);
        }

        // Handle successful authentication
        return await HandleSuccessfulAuthentication(user, request.Password!, cancellationToken);
    }

    /// <summary>
    /// Validates that the account is eligible for local authentication
    /// </summary>
    private AuthenticationResult? ValidateAccountEligibility(Database.Entities.User user)
    {
        if (user.Provider != AuthenticationProvider.Local)
        {
            _logger.LogWarning("Authentication failed: User {Username} is not a local account", user.Username);
            return AuthenticationResult.Failed("This account uses external authentication", AuthenticationFailureReason.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Authentication failed: Account inactive - {Username}", user.Username);
            return AuthenticationResult.Failed("Account is inactive", AuthenticationFailureReason.AccountInactive);
        }

        return null;
    }

    /// <summary>
    /// Checks if account is locked and handles lock expiration
    /// </summary>
    private async Task<AuthenticationResult?> CheckAndHandleAccountLock(
        Database.Entities.User user, 
        CancellationToken cancellationToken)
    {
        if (!user.IsLocked)
        {
            return null;
        }

        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
        {
            return CreateLockedAccountResult(user);
        }

        // Lock has expired, unlock the account
        await UnlockAccount(user, cancellationToken);
        return null;
    }

    /// <summary>
    /// Creates a result for a locked account
    /// </summary>
    private AuthenticationResult CreateLockedAccountResult(Database.Entities.User user)
    {
        _logger.LogWarning("Authentication failed: Account locked - {Username}", user.Username);
        var result = AuthenticationResult.Failed(
            $"Account is locked until {user.LockedUntil!.Value:yyyy-MM-dd HH:mm} UTC",
            AuthenticationFailureReason.AccountLocked);
        result.IsLocked = true;
        result.LockedUntil = user.LockedUntil.Value;
        return result;
    }

    /// <summary>
    /// Unlocks an account after lock expiration
    /// </summary>
    private async Task UnlockAccount(Database.Entities.User user, CancellationToken cancellationToken)
    {
        user.IsLocked = false;
        user.LockedUntil = null;
        user.FailedLoginAttempts = 0;
        await _userRepository.UpdateAsync(user, cancellationToken);
    }

    /// <summary>
    /// Handles a failed password attempt, including account locking
    /// </summary>
    private async Task<AuthenticationResult> HandleFailedPasswordAttempt(
        Database.Entities.User user,
        string username,
        CancellationToken cancellationToken)
    {
        user.FailedLoginAttempts++;

        if (user.FailedLoginAttempts >= MaxFailedAttempts)
        {
            user.IsLocked = true;
            user.LockedUntil = DateTime.UtcNow.AddMinutes(LockoutMinutes);
            _logger.LogWarning("Account locked due to too many failed attempts - {Username}", username);
        }

        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogWarning("Authentication failed: Invalid password - {Username} (Attempt {Attempts})", 
            username, user.FailedLoginAttempts);

        if (user.IsLocked)
        {
            var lockResult = AuthenticationResult.Failed(
                $"Too many failed attempts. Account locked until {user.LockedUntil:yyyy-MM-dd HH:mm} UTC",
                AuthenticationFailureReason.TooManyAttempts);
            lockResult.IsLocked = true;
            lockResult.LockedUntil = user.LockedUntil;
            return lockResult;
        }

        return AuthenticationResult.Failed("Invalid username or password", AuthenticationFailureReason.InvalidCredentials);
    }

    /// <summary>
    /// Handles successful authentication, including password rehashing if needed
    /// </summary>
    private async Task<AuthenticationResult> HandleSuccessfulAuthentication(
        Database.Entities.User user,
        string password,
        CancellationToken cancellationToken)
    {
        if (_passwordHasher.NeedsRehash(user.PasswordHash!))
        {
            user.PasswordHash = _passwordHasher.HashPassword(password);
            _logger.LogInformation("Password rehashed for user {Username}", user.Username);
        }

        user.FailedLoginAttempts = 0;
        user.LastLoginOn = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("User authenticated successfully - {Username}", user.Username);

        return AuthenticationResult.Succeeded(
            user.Id,
            user.Username,
            user.Email,
            user.DisplayName,
            user.IsAdmin,
            user.IsEditor);
    }
}
