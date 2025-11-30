namespace Squirrel.Wiki.Core.Database.Entities;

/// <summary>
/// Represents a customizable navigation menu using MenuMarkup
/// </summary>
public class Menu
{
    public int Id { get; set; }
    
    /// <summary>
    /// User-friendly name for the menu
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Type/location of the menu (MainNavigation, Footer, Sidebar)
    /// </summary>
    public MenuType MenuType { get; set; }
    
    /// <summary>
    /// Optional description of the menu's purpose
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// MenuMarkup content with special tokens (for MainNavigation and Sidebar)
    /// </summary>
    public string? Markup { get; set; }
    
    /// <summary>
    /// Footer left zone content (for Footer type only)
    /// </summary>
    public string? FooterLeftZone { get; set; }
    
    /// <summary>
    /// Footer right zone content (for Footer type only)
    /// </summary>
    public string? FooterRightZone { get; set; }
    
    public int DisplayOrder { get; set; }
    
    public bool IsEnabled { get; set; } = true;
    
    public DateTime ModifiedOn { get; set; }
    
    public string ModifiedBy { get; set; } = string.Empty;
}
