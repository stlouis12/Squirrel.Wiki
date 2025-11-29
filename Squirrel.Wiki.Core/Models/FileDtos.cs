using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Models;

/// <summary>
/// Data transfer object for file information
/// </summary>
public class FileDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? FolderId { get; set; }
    public string? FolderName { get; set; }
    public string StorageProvider { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedOn { get; set; }
    public FileVisibility Visibility { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public int CurrentVersion { get; set; }
    
    /// <summary>
    /// Gets formatted file size (e.g., "1.5 MB")
    /// </summary>
    public string FileSizeFormatted
    {
        get
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            if (FileSize >= GB)
                return $"{FileSize / (double)GB:F2} GB";
            if (FileSize >= MB)
                return $"{FileSize / (double)MB:F2} MB";
            if (FileSize >= KB)
                return $"{FileSize / (double)KB:F2} KB";
            return $"{FileSize} bytes";
        }
    }
}

/// <summary>
/// DTO for uploading a file
/// </summary>
public class FileUploadDto
{
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? FolderId { get; set; }
    public Stream FileStream { get; set; } = Stream.Null;
    public FileVisibility Visibility { get; set; } = FileVisibility.Inherit;
}

/// <summary>
/// DTO for updating file metadata
/// </summary>
public class FileUpdateDto
{
    public string? FileName { get; set; }
    public string? Description { get; set; }
    public FileVisibility? Visibility { get; set; }
}

/// <summary>
/// DTO for searching files
/// </summary>
public class FileSearchDto
{
    public string? SearchTerm { get; set; }
    public int? FolderId { get; set; }
    public string? ContentType { get; set; }
    public DateTime? UploadedAfter { get; set; }
    public DateTime? UploadedBefore { get; set; }
    public string? UploadedBy { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>
/// DTO for folder information
/// </summary>
public class FolderDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentFolderId { get; set; }
    public string? ParentFolderName { get; set; }
    public string? Path { get; set; }
    public int FileCount { get; set; }
    public int SubFolderCount { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
}

/// <summary>
/// DTO for creating a folder
/// </summary>
public class FolderCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentFolderId { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// DTO for updating a folder
/// </summary>
public class FolderUpdateDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
}

/// <summary>
/// DTO for folder tree structure
/// </summary>
public class FolderTreeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int? ParentFolderId { get; set; }
    public int FileCount { get; set; }
    public List<FolderTreeDto> Children { get; set; } = new();
}

/// <summary>
/// DTO for detailed file information
/// </summary>
public class FileDetailsDto : FileDto
{
    public string? FolderPath { get; set; }
}

/// <summary>
/// Data transfer object for file version information
/// </summary>
public class FileVersionDto
{
    public int Id { get; set; }
    public Guid FileId { get; set; }
    public int VersionNumber { get; set; }
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedOn { get; set; }
}
