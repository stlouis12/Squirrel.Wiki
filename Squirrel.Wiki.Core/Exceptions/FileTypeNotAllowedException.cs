namespace Squirrel.Wiki.Core.Exceptions;

/// <summary>
/// Exception thrown when a file type is not allowed
/// </summary>
public class FileTypeNotAllowedException : ValidationException
{
    public string FileName { get; }
    public string FileExtension { get; }
    public string AllowedExtensions { get; }
    private readonly string _userMessage;
    
    public FileTypeNotAllowedException(string fileName, string fileExtension, string allowedExtensions)
        : base($"The file type '{fileExtension}' is not allowed for upload. Allowed file types: {allowedExtensions}")
    {
        FileName = fileName;
        FileExtension = fileExtension;
        AllowedExtensions = allowedExtensions;
        _userMessage = $"The file type '{fileExtension}' is not allowed for upload. Allowed file types: {allowedExtensions}";
        Context["FileName"] = fileName;
        Context["FileExtension"] = fileExtension;
        Context["AllowedExtensions"] = allowedExtensions;
    }
    
    public override string GetUserMessage()
    {
        return _userMessage;
    }
}
