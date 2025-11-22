namespace Squirrel.Wiki.Contracts.Authentication;

/// <summary>
/// Interface for authentication strategies (Local, OIDC, etc.)
/// </summary>
public interface IAuthenticationStrategy
{
    /// <summary>
    /// Gets the authentication provider type this strategy handles
    /// </summary>
    AuthenticationProvider Provider { get; }

    /// <summary>
    /// Authenticates a user with the provided credentials
    /// </summary>
    Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if the strategy can handle the given request
    /// </summary>
    bool CanHandle(AuthenticationRequest request);
}

/// <summary>
/// Authentication provider types
/// </summary>
public enum AuthenticationProvider
{
    Local = 0,
    OpenIdConnect = 1
}

/// <summary>
/// Request for authentication
/// </summary>
public class AuthenticationRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ExternalId { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public List<string> Groups { get; set; } = new();
    public AuthenticationProvider Provider { get; set; }
}

/// <summary>
/// Result of authentication attempt
/// </summary>
public class AuthenticationResult
{
    public bool Success { get; set; }
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsEditor { get; set; }
    public string? ErrorMessage { get; set; }
    public AuthenticationFailureReason? FailureReason { get; set; }
    public bool RequiresPasswordChange { get; set; }
    public bool IsLocked { get; set; }
    public DateTime? LockedUntil { get; set; }

    public static AuthenticationResult Failed(string errorMessage, AuthenticationFailureReason reason)
    {
        return new AuthenticationResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            FailureReason = reason
        };
    }

    public static AuthenticationResult Succeeded(Guid userId, string username, string email, string displayName, bool isAdmin, bool isEditor)
    {
        return new AuthenticationResult
        {
            Success = true,
            UserId = userId,
            Username = username,
            Email = email,
            DisplayName = displayName,
            IsAdmin = isAdmin,
            IsEditor = isEditor
        };
    }
}

/// <summary>
/// Reasons for authentication failure
/// </summary>
public enum AuthenticationFailureReason
{
    InvalidCredentials,
    AccountLocked,
    AccountInactive,
    AccountNotFound,
    PasswordExpired,
    ExternalProviderError,
    TooManyAttempts
}
