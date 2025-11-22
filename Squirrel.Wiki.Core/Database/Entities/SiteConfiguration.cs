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
    
    /// <summary>
    /// Indicates if this setting value comes from an environment variable
    /// </summary>
    public bool IsFromEnvironment { get; set; }
    
    /// <summary>
    /// The environment variable name if IsFromEnvironment is true
    /// </summary>
    public string? EnvironmentVariableName { get; set; }
}
