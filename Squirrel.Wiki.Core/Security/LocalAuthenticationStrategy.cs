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

        // Find user by username
        var user = await _userRepository.GetByUsernameAsync(request.Username!, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("Authentication failed: User not found - {Username}", request.Username);
            return AuthenticationResult.Failed("Invalid username or password", AuthenticationFailureReason.AccountNotFound);
        }

        // Check if account is for local authentication
        if (user.Provider != AuthenticationProvider.Local)
        {
            _logger.LogWarning("Authentication failed: User {Username} is not a local account", request.Username);
            return AuthenticationResult.Failed("This account uses external authentication", AuthenticationFailureReason.InvalidCredentials);
        }

        // Check if account is active
        if (!user.IsActive)
        {
            _logger.LogWarning("Authentication failed: Account inactive - {Username}", request.Username);
            return AuthenticationResult.Failed("Account is inactive", AuthenticationFailureReason.AccountInactive);
        }

        // Check if account is locked
        if (user.IsLocked)
        {
            if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
            {
                _logger.LogWarning("Authentication failed: Account locked - {Username}", request.Username);
                var result = AuthenticationResult.Failed(
                    $"Account is locked until {user.LockedUntil.Value:yyyy-MM-dd HH:mm} UTC",
                    AuthenticationFailureReason.AccountLocked);
                result.IsLocked = true;
                result.LockedUntil = user.LockedUntil.Value;
                return result;
            }
            else
            {
                // Lock has expired, unlock the account
                user.IsLocked = false;
                user.LockedUntil = null;
                user.FailedLoginAttempts = 0;
                await _userRepository.UpdateAsync(user, cancellationToken);
            }
        }

        // Verify password
        if (string.IsNullOrWhiteSpace(user.PasswordHash) || !_passwordHasher.VerifyPassword(request.Password!, user.PasswordHash))
        {
            // Increment failed login attempts
            user.FailedLoginAttempts++;

            // Lock account if too many failed attempts
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.IsLocked = true;
                user.LockedUntil = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                _logger.LogWarning("Account locked due to too many failed attempts - {Username}", request.Username);
            }

            await _userRepository.UpdateAsync(user, cancellationToken);

            _logger.LogWarning("Authentication failed: Invalid password - {Username} (Attempt {Attempts})", 
                request.Username, user.FailedLoginAttempts);

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

        // Check if password needs rehashing
        if (_passwordHasher.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = _passwordHasher.HashPassword(request.Password!);
            _logger.LogInformation("Password rehashed for user {Username}", request.Username);
        }

        // Reset failed login attempts on successful login
        user.FailedLoginAttempts = 0;
        user.LastLoginOn = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("User authenticated successfully - {Username}", request.Username);

        return AuthenticationResult.Succeeded(
            user.Id,
            user.Username,
            user.Email,
            user.DisplayName,
            user.IsAdmin,
            user.IsEditor);
    }
}
