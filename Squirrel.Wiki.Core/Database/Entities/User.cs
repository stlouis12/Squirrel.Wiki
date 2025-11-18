namespace Squirrel.Wiki.Core.Database.Entities;

/// <summary>
/// Represents a user profile (cached from OpenID Connect)
/// </summary>
public class User
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// External ID from OpenID Connect provider (sub claim)
    /// </summary>
    public string ExternalId { get; set; } = string.Empty;
    
    public string Email { get; set; } = string.Empty;
    
    public string Username { get; set; } = string.Empty;
    
    public string DisplayName { get; set; } = string.Empty;
    
    public bool IsAdmin { get; set; }
    
    public bool IsEditor { get; set; }
    
    public DateTime LastLoginOn { get; set; }
    
    public DateTime CreatedOn { get; set; }
}
