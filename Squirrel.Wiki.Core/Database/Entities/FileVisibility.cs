namespace Squirrel.Wiki.Core.Database.Entities;

/// <summary>
/// Defines the visibility level for files
/// </summary>
public enum FileVisibility
{
    /// <summary>
    /// Inherits the global AllowAnonymousReading setting (default)
    /// </summary>
    Inherit = 0,
    
    /// <summary>
    /// File is publicly visible to anonymous users, regardless of global setting
    /// </summary>
    Public = 1,
    
    /// <summary>
    /// File requires authentication to view, regardless of global setting
    /// </summary>
    Private = 2
}
