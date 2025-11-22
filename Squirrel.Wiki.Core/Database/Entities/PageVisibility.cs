namespace Squirrel.Wiki.Core.Database.Entities;

/// <summary>
/// Defines the visibility level for a wiki page
/// </summary>
public enum PageVisibility
{
    /// <summary>
    /// Inherits the global AllowAnonymousReading setting (default)
    /// </summary>
    Inherit = 0,
    
    /// <summary>
    /// Page is publicly visible to anonymous users, regardless of global setting
    /// </summary>
    Public = 1,
    
    /// <summary>
    /// Page requires authentication to view, regardless of global setting
    /// </summary>
    Private = 2
}
