using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Web.Models;

/// <summary>
/// View model for sidebar navigation with enhanced support for special tokens
/// </summary>
public class SidebarViewModel
{
    public List<SidebarItemViewModel> Items { get; set; } = new();
}

/// <summary>
/// Represents a single item in the sidebar navigation
/// </summary>
public class SidebarItemViewModel
{
    public string Text { get; set; } = string.Empty;
    public string? Url { get; set; }
    public SidebarItemType Type { get; set; } = SidebarItemType.Link;
    public List<SidebarItemViewModel> Children { get; set; } = new();
    public bool IsCollapsible => Children.Any() || Type == SidebarItemType.AllTags || Type == SidebarItemType.AllCategories;
    public string CollapseId => $"sidebar-{Guid.NewGuid():N}";
}

/// <summary>
/// Type of sidebar item
/// </summary>
public enum SidebarItemType
{
    /// <summary>
    /// Regular link item
    /// </summary>
    Link,
    
    /// <summary>
    /// Header with nested children
    /// </summary>
    Header,
    
    /// <summary>
    /// Special token: %ALLTAGS% - expands to show all tags
    /// </summary>
    AllTags,
    
    /// <summary>
    /// Special token: %ALLCATEGORIES% - expands to show category tree
    /// </summary>
    AllCategories,
    
    /// <summary>
    /// Special token: %EMBEDDED_SEARCH% - renders an embedded search box
    /// </summary>
    EmbeddedSearch
}
