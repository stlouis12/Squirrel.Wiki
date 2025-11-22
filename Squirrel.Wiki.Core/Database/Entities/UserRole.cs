namespace Squirrel.Wiki.Core.Database.Entities;

/// <summary>
/// Represents a role assignment for a user
/// </summary>
public class UserRole
{
    public Guid Id { get; set; }
    
    public Guid UserId { get; set; }
    
    public Role Role { get; set; }
    
    public DateTime AssignedOn { get; set; }
    
    public string AssignedBy { get; set; } = string.Empty;
    
    // Navigation
    public User User { get; set; } = null!;
}

/// <summary>
/// User role types
/// </summary>
public enum Role
{
    /// <summary>
    /// Read-only access to pages
    /// </summary>
    Viewer = 0,
    
    /// <summary>
    /// Can create and edit pages
    /// </summary>
    Editor = 1,
    
    /// <summary>
    /// Full system access including user management
    /// </summary>
    Admin = 2
}
