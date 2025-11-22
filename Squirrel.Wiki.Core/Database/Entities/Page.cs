namespace Squirrel.Wiki.Core.Database.Entities;

/// <summary>
/// Represents a wiki page with metadata
/// </summary>
public class Page
{
    public int Id { get; set; }
    
    public string Title { get; set; } = string.Empty;
    
    public string Slug { get; set; } = string.Empty;
    
    public int? CategoryId { get; set; }
    
    public string CreatedBy { get; set; } = string.Empty;
    
    public DateTime CreatedOn { get; set; }
    
    public string ModifiedBy { get; set; } = string.Empty;
    
    public DateTime ModifiedOn { get; set; }
    
    public bool IsLocked { get; set; }
    
    public bool IsDeleted { get; set; }
    
    /// <summary>
    /// Visibility setting for this page. 
    /// Public = visible to anonymous users even if global anonymous reading is disabled
    /// Private = requires authentication even if global anonymous reading is enabled
    /// Inherit = follows the global AllowAnonymousReading setting (default)
    /// </summary>
    public PageVisibility Visibility { get; set; } = PageVisibility.Inherit;
    
    // Navigation properties
    public Category? Category { get; set; }
    
    public ICollection<PageContent> Contents { get; set; } = new List<PageContent>();
    
    public ICollection<PageTag> PageTags { get; set; } = new List<PageTag>();
}
