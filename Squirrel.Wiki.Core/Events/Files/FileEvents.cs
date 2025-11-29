namespace Squirrel.Wiki.Core.Events.Files;

/// <summary>
/// Event raised when a file is uploaded
/// </summary>
public class FileUploadedEvent : DomainEvent
{
    public int FileId { get; }
    public string FileName { get; }
    public string FileHash { get; }
    public long FileSize { get; }
    public int? FolderId { get; }
    public string UploadedBy { get; }
    
    public FileUploadedEvent(
        int fileId,
        string fileName,
        string fileHash,
        long fileSize,
        int? folderId,
        string uploadedBy)
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
/// Event raised when a file is deleted
/// </summary>
public class FileDeletedEvent : DomainEvent
{
    public int FileId { get; }
    public string FileName { get; }
    public string FileHash { get; }
    public int? FolderId { get; }
    public string DeletedBy { get; }
    
    public FileDeletedEvent(
        int fileId,
        string fileName,
        string fileHash,
        int? folderId,
        string deletedBy)
    {
        FileId = fileId;
        FileName = fileName;
        FileHash = fileHash;
        FolderId = folderId;
        DeletedBy = deletedBy;
    }
}

/// <summary>
/// Event raised when file metadata is updated
/// </summary>
public class FileUpdatedEvent : DomainEvent
{
    public int FileId { get; }
    public string FileName { get; }
    public string UpdatedBy { get; }
    public Dictionary<string, object> Changes { get; }
    
    public FileUpdatedEvent(
        int fileId,
        string fileName,
        string updatedBy,
        Dictionary<string, object> changes)
    {
        FileId = fileId;
        FileName = fileName;
        UpdatedBy = updatedBy;
        Changes = changes;
    }
}

/// <summary>
/// Event raised when a file is moved to a different folder
/// </summary>
public class FileMovedEvent : DomainEvent
{
    public int FileId { get; }
    public string FileName { get; }
    public int? OldFolderId { get; }
    public int? NewFolderId { get; }
    public string MovedBy { get; }
    
    public FileMovedEvent(
        int fileId,
        string fileName,
        int? oldFolderId,
        int? newFolderId,
        string movedBy)
    {
        FileId = fileId;
        FileName = fileName;
        OldFolderId = oldFolderId;
        NewFolderId = newFolderId;
        MovedBy = movedBy;
    }
}

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
    
    public FolderCreatedEvent(
        int folderId,
        string folderName,
        string slug,
        int? parentFolderId,
        string createdBy)
    {
        FolderId = folderId;
        FolderName = folderName;
        Slug = slug;
        ParentFolderId = parentFolderId;
        CreatedBy = createdBy;
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
    
    public FolderDeletedEvent(
        int folderId,
        string folderName,
        int? parentFolderId,
        string deletedBy)
    {
        FolderId = folderId;
        FolderName = folderName;
        ParentFolderId = parentFolderId;
        DeletedBy = deletedBy;
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
    
    public FolderUpdatedEvent(
        int folderId,
        string folderName,
        string updatedBy,
        Dictionary<string, object> changes)
    {
        FolderId = folderId;
        FolderName = folderName;
        UpdatedBy = updatedBy;
        Changes = changes;
    }
}
