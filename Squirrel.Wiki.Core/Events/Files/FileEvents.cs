using Squirrel.Wiki.Core.Events;

namespace Squirrel.Wiki.Core.Events.Files;

/// <summary>
/// Event raised when a file is uploaded
/// </summary>
public class FileUploadedEvent : DomainEvent
{
    public Guid FileId { get; }
    public string FileName { get; }
    public string FileHash { get; }
    public long FileSize { get; }
    public int? FolderId { get; }
    public string UploadedBy { get; }
    
    public FileUploadedEvent(Guid fileId, string fileName, string fileHash, long fileSize, int? folderId, string uploadedBy)
    {
        FileId = fileId;
        FileName = fileName;
        FileHash = fileHash;
        FileSize = fileSize;
        FolderId = folderId;
        UploadedBy = uploadedBy;
    }
}

/// <summary>
/// Event raised when a file is updated
/// </summary>
public class FileUpdatedEvent : DomainEvent
{
    public Guid FileId { get; }
    public string FileName { get; }
    public string UpdatedBy { get; }
    public Dictionary<string, object> Changes { get; }
    
    public FileUpdatedEvent(Guid fileId, string fileName, string updatedBy, Dictionary<string, object> changes)
    {
        FileId = fileId;
        FileName = fileName;
        UpdatedBy = updatedBy;
        Changes = changes ?? new Dictionary<string, object>();
    }
}

/// <summary>
/// Event raised when a file is deleted
/// </summary>
public class FileDeletedEvent : DomainEvent
{
    public Guid FileId { get; }
    public string FileName { get; }
    public string FileHash { get; }
    public int? FolderId { get; }
    public string DeletedBy { get; }
    
    public FileDeletedEvent(Guid fileId, string fileName, string fileHash, int? folderId, string deletedBy)
    {
        FileId = fileId;
        FileName = fileName;
        FileHash = fileHash;
        FolderId = folderId;
        DeletedBy = deletedBy;
    }
}

/// <summary>
/// Event raised when a file is moved to a different folder
/// </summary>
public class FileMovedEvent : DomainEvent
{
    public Guid FileId { get; }
    public string FileName { get; }
    public int? OldFolderId { get; }
    public int? NewFolderId { get; }
    public string MovedBy { get; }
    
    public FileMovedEvent(Guid fileId, string fileName, int? oldFolderId, int? newFolderId, string movedBy)
    {
        FileId = fileId;
        FileName = fileName;
        OldFolderId = oldFolderId;
        NewFolderId = newFolderId;
        MovedBy = movedBy;
    }
}
