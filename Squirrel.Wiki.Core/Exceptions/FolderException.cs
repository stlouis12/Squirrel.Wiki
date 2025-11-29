namespace Squirrel.Wiki.Core.Exceptions;

/// <summary>
/// Exception thrown when folder operations fail
/// </summary>
public class FolderException : SquirrelWikiException
{
    public int? FolderId { get; }
    public string? FolderName { get; }
    
    public FolderException(
        string message,
        int? folderId = null,
        string? folderName = null)
        : base(
            message,
            "FOLDER_ERROR",
            statusCode: 400,
            shouldLog: false)
    {
        FolderId = folderId;
        FolderName = folderName;
        
        if (folderId.HasValue)
            Context["FolderId"] = folderId.Value;
        if (folderName != null)
            Context["FolderName"] = folderName;
    }
}

/// <summary>
/// Exception thrown when folder depth limit is exceeded
/// </summary>
public class FolderDepthExceededException : FolderException
{
    public int CurrentDepth { get; }
    public int MaxDepth { get; }
    
    public FolderDepthExceededException(int currentDepth, int maxDepth)
        : base($"Folder depth ({currentDepth}) exceeds the maximum allowed depth ({maxDepth})")
    {
        CurrentDepth = currentDepth;
        MaxDepth = maxDepth;
        Context["CurrentDepth"] = currentDepth;
        Context["MaxDepth"] = maxDepth;
    }
}

/// <summary>
/// Exception thrown when attempting to create a duplicate folder
/// </summary>
public class DuplicateFolderException : FolderException
{
    public DuplicateFolderException(string folderName, int? parentFolderId = null)
        : base(
            $"A folder named '{folderName}' already exists in this location",
            parentFolderId,
            folderName)
    {
    }
}
