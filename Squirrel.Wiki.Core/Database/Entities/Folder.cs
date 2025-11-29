namespace Squirrel.Wiki.Core.Database.Entities;

/// <summary>
/// Represents a folder for organizing files
/// </summary>
public class Folder
{
    public int Id { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public string Slug { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    public int? ParentFolderId { get; set; }
    
    public int DisplayOrder { get; set; }
    
    public string CreatedBy { get; set; } = string.Empty;
    
    public DateTime CreatedOn { get; set; }
    
    public bool IsDeleted { get; set; }
    
    // Navigation properties
    public Folder? ParentFolder { get; set; }
    
    public ICollection<Folder> SubFolders { get; set; } = new List<Folder>();
    
    public ICollection<File> Files { get; set; } = new List<File>();
}
