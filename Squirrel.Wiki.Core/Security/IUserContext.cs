namespace Squirrel.Wiki.Core.Security;

/// <summary>
/// Provides access to the current user's information
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// Gets the current user's unique identifier
    /// </summary>
    string? UserId { get; }
    
    /// <summary>
    /// Gets the current user's username
    /// </summary>
    string? Username { get; }
    
    /// <summary>
    /// Gets the current user's email address
    /// </summary>
    string? Email { get; }
    
    /// <summary>
    /// Gets whether the current user is authenticated
    /// </summary>
    bool IsAuthenticated { get; }
    
    /// <summary>
    /// Gets whether the current user has admin privileges
    /// </summary>
    bool IsAdmin { get; }
    
    /// <summary>
    /// Gets whether the current user has editor privileges (includes admins)
    /// </summary>
    bool IsEditor { get; }
    
    /// <summary>
    /// Gets all roles assigned to the current user
    /// </summary>
    IEnumerable<string> GetRoles();
}
