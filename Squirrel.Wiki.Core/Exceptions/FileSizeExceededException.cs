namespace Squirrel.Wiki.Core.Exceptions;

/// <summary>
/// Exception thrown when a file size exceeds the allowed limit
/// </summary>
public class FileSizeExceededException : ValidationException
{
    public long FileSize { get; }
    public long MaxFileSize { get; }
    
    public FileSizeExceededException(long fileSize, long maxFileSize)
        : base($"File size ({FormatBytes(fileSize)}) exceeds the maximum allowed size ({FormatBytes(maxFileSize)})")
    {
        FileSize = fileSize;
        MaxFileSize = maxFileSize;
        Context["FileSize"] = fileSize;
        Context["MaxFileSize"] = maxFileSize;
    }
    
    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
