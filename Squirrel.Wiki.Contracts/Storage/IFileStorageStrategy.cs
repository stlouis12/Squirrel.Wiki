namespace Squirrel.Wiki.Contracts.Storage;

/// <summary>
/// Strategy interface for file storage providers.
/// Implementations can store files locally, in cloud storage, or other locations.
/// </summary>
public interface IFileStorageStrategy
{
    /// <summary>
    /// Gets the unique identifier for this storage provider
    /// </summary>
    string ProviderId { get; }
    
    /// <summary>
    /// Gets the display name for this storage provider
    /// </summary>
    string ProviderName { get; }
    
    /// <summary>
    /// Saves a file to storage
    /// </summary>
    /// <param name="stream">File content stream</param>
    /// <param name="path">Relative path where file should be stored</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Full storage path of saved file</returns>
    Task<string> SaveFileAsync(Stream stream, string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves a file from storage
    /// </summary>
    /// <param name="path">Path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File content stream</returns>
    Task<Stream> GetFileAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a file from storage
    /// </summary>
    /// <param name="path">Path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteFileAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a file exists in storage
    /// </summary>
    /// <param name="path">Path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if file exists, false otherwise</returns>
    Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets file information without downloading the file
    /// </summary>
    /// <param name="path">Path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File information</returns>
    Task<FileStorageInfo> GetFileInfoAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Copies a file within storage
    /// </summary>
    /// <param name="sourcePath">Source file path</param>
    /// <param name="destinationPath">Destination file path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Moves a file within storage
    /// </summary>
    /// <param name="sourcePath">Source file path</param>
    /// <param name="destinationPath">Destination file path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MoveFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// File information returned by storage providers
/// </summary>
public class FileStorageInfo
{
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}
