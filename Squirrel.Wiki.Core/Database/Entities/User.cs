using Squirrel.Wiki.Plugins;

using Squirrel.Wiki.Contracts.Authentication;

namespace Squirrel.Wiki.Core.Database.Entities;

/// <summary>
/// Represents a user in the system (local or OpenID Connect)
/// </summary>
public class User
{
    public Guid Id { get; set; }
    
    // Authentication
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Password hash for local authentication. Null for OIDC users.
    /// </summary>
    public string? PasswordHash { get; set; }
    
    /// <summary>
    /// External ID from OpenID Connect provider (sub claim). Null for local users.
    /// </summary>
    public string? ExternalId { get; set; }
    
    /// <summary>
    /// Authentication provider type
    /// </summary>
    public AuthenticationProvider Provider { get; set; }
    
    // Profile
    public string DisplayName { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    
    // Status
    public bool IsActive { get; set; } = true;
    public bool IsLocked { get; set; } = false;
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockedUntil { get; set; }
    
    // Password Reset
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpiry { get; set; }
    
    // Timestamps
    public DateTime CreatedOn { get; set; }
    public DateTime? LastLoginOn { get; set; }
    public DateTime? LastPasswordChangeOn { get; set; }
    
    // Legacy role flags (will be migrated to UserRoles)
    public bool IsAdmin { get; set; }
    public bool IsEditor { get; set; }
    
    // Navigation
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
