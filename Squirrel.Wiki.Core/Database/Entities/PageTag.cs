namespace Squirrel.Wiki.Core.Database.Entities;

/// <summary>
/// Represents the many-to-many relationship between pages and tags
/// </summary>
public class PageTag
{
    public int PageId { get; set; }
    
    public int TagId { get; set; }
    
    // Navigation properties
    public Page Page { get; set; } = null!;
    
    public Tag Tag { get; set; } = null!;
}
