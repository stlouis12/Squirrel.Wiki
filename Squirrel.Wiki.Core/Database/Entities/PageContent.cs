namespace Squirrel.Wiki.Core.Database.Entities;

/// <summary>
/// Represents the content and version history of a wiki page
/// </summary>
public class PageContent
{
    public Guid Id { get; set; }
    
    public int PageId { get; set; }
    
    public string Text { get; set; } = string.Empty;
    
    public string EditedBy { get; set; } = string.Empty;
    
    public DateTime EditedOn { get; set; }
    
    public int VersionNumber { get; set; }
    
    public string? ChangeComment { get; set; }
    
    // Navigation properties
    public Page Page { get; set; } = null!;
}
