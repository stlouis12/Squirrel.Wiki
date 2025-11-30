namespace Squirrel.Wiki.Core.Exceptions;

/// <summary>
/// Exception thrown when file storage operations fail
/// </summary>
public class FileStorageException : SquirrelWikiException
{
    public string? FilePath { get; }
    public string? StorageProvider { get; }
    
    public FileStorageException(
        string message,
        string? filePath = null,
        string? storageProvider = null)
        : base(
            message,
            "FILE_STORAGE_ERROR",
            statusCode: 500,
            shouldLog: true)
    {
        FilePath = filePath;
        StorageProvider = storageProvider;
        
        if (filePath != null)
            Context["FilePath"] = filePath;
        if (storageProvider != null)
            Context["StorageProvider"] = storageProvider;
    }
    
    public FileStorageException(
        string message,
        Exception innerException,
        string? filePath = null,
        string? storageProvider = null)
        : base(
            message,
            "FILE_STORAGE_ERROR",
            innerException,
            statusCode: 500,
            shouldLog: true)
    {
        FilePath = filePath;
        StorageProvider = storageProvider;
        
        if (filePath != null)
            Context["FilePath"] = filePath;
        if (storageProvider != null)
            Context["StorageProvider"] = storageProvider;
    }
    
    public override string GetUserMessage()
    {
        return "An error occurred while accessing file storage. Please try again later.";
    }
}
