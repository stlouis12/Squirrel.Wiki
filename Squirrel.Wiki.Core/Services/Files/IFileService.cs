using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Services.Files;

/// <summary>
/// Service interface for file management operations
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Uploads a new file
    /// </summary>
    Task<Result<FileDto>> UploadFileAsync(FileUploadDto uploadDto, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Uploads multiple files
    /// </summary>
    Task<Result<List<FileDto>>> UploadMultipleFilesAsync(List<FileUploadDto> uploadDtos, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a file by ID
    /// </summary>
    Task<Result<FileDto>> GetFileByIdAsync(Guid fileId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets detailed file information including version history
    /// </summary>
    Task<Result<FileDetailsDto>> GetFileDetailsAsync(Guid fileId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a file by path
    /// </summary>
    Task<Result<FileDto>> GetFileByPathAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets files in a folder
    /// </summary>
    Task<Result<List<FileDto>>> GetFilesByFolderAsync(int? folderId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Downloads a file
    /// </summary>
    Task<Result<(Stream Stream, string FileName, string ContentType)>> DownloadFileAsync(Guid fileId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates file metadata
    /// </summary>
    Task<Result<FileDto>> UpdateFileAsync(Guid fileId, FileUpdateDto updateDto, string updatedBy, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Moves a file to a different folder
    /// </summary>
    Task<Result<FileDto>> MoveFileAsync(Guid fileId, int? newFolderId, string movedBy, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a file (soft delete)
    /// </summary>
    Task<Result> DeleteFileAsync(Guid fileId, string deletedBy, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Permanently deletes a file and its physical storage
    /// </summary>
    Task<Result> PermanentlyDeleteFileAsync(Guid fileId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Searches for files
    /// </summary>
    Task<Result<List<FileDto>>> SearchFilesAsync(FileSearchDto searchDto, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets file usage statistics (how many files reference the same content)
    /// </summary>
    Task<Result<int>> GetFileUsageCountAsync(string fileHash, CancellationToken cancellationToken = default);
}
