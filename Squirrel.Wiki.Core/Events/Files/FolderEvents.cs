using Squirrel.Wiki.Core.Events;

namespace Squirrel.Wiki.Core.Events.Files;

/// <summary>
/// Event raised when a folder is created
/// </summary>
public class FolderCreatedEvent : DomainEvent
{
    public int FolderId { get; }
    public string FolderName { get; }
    public string Slug { get; }
    public int? ParentFolderId { get; }
    public string CreatedBy { get; }
    
    public FolderCreatedEvent(int folderId, string folderName, string slug, int? parentFolderId, string createdBy)
    {
        FolderId = folderId;
        FolderName = folderName;
        Slug = slug;
        ParentFolderId = parentFolderId;
        CreatedBy = createdBy;
    }
}

/// <summary>
/// Event raised when a folder is updated
/// </summary>
public class FolderUpdatedEvent : DomainEvent
{
    public int FolderId { get; }
    public string FolderName { get; }
    public string UpdatedBy { get; }
    public Dictionary<string, object> Changes { get; }
    
    public FolderUpdatedEvent(int folderId, string folderName, string updatedBy, Dictionary<string, object> changes)
    {
        FolderId = folderId;
        FolderName = folderName;
        UpdatedBy = updatedBy;
        Changes = changes ?? new Dictionary<string, object>();
    }
}

/// <summary>
/// Event raised when a folder is deleted
/// </summary>
public class FolderDeletedEvent : DomainEvent
{
    public int FolderId { get; }
    public string FolderName { get; }
    public int? ParentFolderId { get; }
    public string DeletedBy { get; }
    
    public FolderDeletedEvent(int folderId, string folderName, int? parentFolderId, string deletedBy)
    {
        FolderId = folderId;
        FolderName = folderName;
        ParentFolderId = parentFolderId;
        DeletedBy = deletedBy;
    }
}
