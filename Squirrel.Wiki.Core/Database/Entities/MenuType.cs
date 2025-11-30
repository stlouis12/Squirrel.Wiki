namespace Squirrel.Wiki.Core.Database.Entities;

/// <summary>
/// Defines the type/location of a menu in the application
/// </summary>
public enum MenuType
{
    /// <summary>
    /// Main navigation menu (typically in header)
    /// </summary>
    MainNavigation = 1,
    
    /// <summary>
    /// Footer menu (typically in page footer)
    /// </summary>
    Footer = 2,
    
    /// <summary>
    /// Sidebar menu (typically in left or right sidebar)
    /// </summary>
    Sidebar = 3
}
