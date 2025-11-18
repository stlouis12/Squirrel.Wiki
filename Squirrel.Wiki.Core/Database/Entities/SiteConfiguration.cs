namespace Squirrel.Wiki.Core.Database.Entities;

/// <summary>
/// Represents application settings stored in the database
/// </summary>
public class SiteConfiguration
{
    public Guid Id { get; set; }
    
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// JSON-serialized value
    /// </summary>
    public string Value { get; set; } = string.Empty;
    
    public DateTime ModifiedOn { get; set; }
    
    public string ModifiedBy { get; set; } = string.Empty;
}
