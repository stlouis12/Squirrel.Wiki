namespace Squirrel.Wiki.Core.Database.Entities;

/// <summary>
/// Represents a historical version of a file (only created when versioning is enabled)
/// </summary>
public class FileVersion
{
    public int Id { get; set; }
    
    public int FileId { get; set; }
    
    public int VersionNumber { get; set; }
    
    public string FileHash { get; set; } = string.Empty;
    
    public long FileSize { get; set; }
    
    public string CreatedBy { get; set; } = string.Empty;
    
    public DateTime CreatedOn { get; set; }
    
    public string? ChangeDescription { get; set; }
    
    // Navigation properties
    public File? File { get; set; }
    
    public FileContent? Content { get; set; }
}
