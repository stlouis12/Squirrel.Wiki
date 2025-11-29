namespace Squirrel.Wiki.Core.Exceptions;

/// <summary>
/// Exception thrown when a file type is not allowed
/// </summary>
public class FileTypeNotAllowedException : ValidationException
{
    public string FileName { get; }
    public string FileExtension { get; }
    
    public FileTypeNotAllowedException(string fileName, string fileExtension)
        : base($"File type '{fileExtension}' is not allowed. File: {fileName}")
    {
        FileName = fileName;
        FileExtension = fileExtension;
        Context["FileName"] = fileName;
        Context["FileExtension"] = fileExtension;
    }
}
