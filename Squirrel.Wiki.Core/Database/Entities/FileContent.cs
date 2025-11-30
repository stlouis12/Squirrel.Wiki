namespace Squirrel.Wiki.Core.Database.Entities;

/// <summary>
/// Represents the actual file content stored in the storage provider.
/// Multiple File entities can reference the same FileContent (deduplication).
/// </summary>
public class FileContent
{
    public string FileHash { get; set; } = string.Empty;
    
    public string StoragePath { get; set; } = string.Empty;
    
    public long FileSize { get; set; }
    
    public string StorageProvider { get; set; } = string.Empty;
    
    public int ReferenceCount { get; set; }
    
    public DateTime CreatedOn { get; set; }
    
    // Navigation properties
    public ICollection<File> Files { get; set; } = new List<File>();
}
