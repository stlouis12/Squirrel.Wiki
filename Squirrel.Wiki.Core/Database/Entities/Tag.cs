namespace Squirrel.Wiki.Core.Database.Entities;

/// <summary>
/// Represents a normalized tag for categorizing pages
/// </summary>
public class Tag
{
    public int Id { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public string NormalizedName { get; set; } = string.Empty;
    
    // Navigation properties
    public ICollection<PageTag> PageTags { get; set; } = new List<PageTag>();
}
