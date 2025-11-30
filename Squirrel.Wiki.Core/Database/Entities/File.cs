namespace Squirrel.Wiki.Core.Database.Entities;

/// <summary>
/// Represents a file in the wiki with metadata
/// </summary>
public class File
{
    public Guid Id { get; set; }
    
    public string FileHash { get; set; } = string.Empty;
    
    public string FileName { get; set; } = string.Empty;
    
    public string FilePath { get; set; } = string.Empty;
    
    public long FileSize { get; set; }
    
    public string ContentType { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    public int? FolderId { get; set; }
    
    public string StorageProvider { get; set; } = string.Empty;
    
    public string UploadedBy { get; set; } = string.Empty;
    
    public DateTime UploadedOn { get; set; }
    
    public FileVisibility Visibility { get; set; } = FileVisibility.Inherit;
    
    public bool IsDeleted { get; set; }
    
    public string? ThumbnailPath { get; set; }
    
    public int CurrentVersion { get; set; } = 1;
    
    // Navigation properties
    public Folder? Folder { get; set; }
    
    public FileContent? Content { get; set; }
    
    public ICollection<FileVersion> Versions { get; set; } = new List<FileVersion>();
}
