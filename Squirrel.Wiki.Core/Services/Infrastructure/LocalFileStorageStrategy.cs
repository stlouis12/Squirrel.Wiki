using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Contracts.Storage;
using Squirrel.Wiki.Core.Exceptions;

namespace Squirrel.Wiki.Core.Services.Infrastructure;

/// <summary>
/// Local file system storage strategy implementation
/// </summary>
public class LocalFileStorageStrategy : IFileStorageStrategy
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageStrategy> _logger;
    private readonly IConfigurationService _configurationService;
    
    public string ProviderId => "Local";
    public string ProviderName => "Local File System";
    
    public LocalFileStorageStrategy(
        IConfigurationService configurationService,
        ILogger<LocalFileStorageStrategy> logger)
    {
        _logger = logger;
        _configurationService = configurationService;
        
        // Get base path from configuration service (synchronously in constructor)
        var configuredPath = _configurationService.GetValueAsync<string>("SQUIRREL_FILE_STORAGE_PATH").GetAwaiter().GetResult();
        
        // Resolve relative paths to absolute paths based on the application's base directory
        if (Path.IsPathRooted(configuredPath))
        {
            _basePath = configuredPath;
        }
        else
        {
            // For relative paths starting with "App_Data/", resolve relative to the executable directory
            if (configuredPath.StartsWith("App_Data/", StringComparison.OrdinalIgnoreCase) ||
                configuredPath.StartsWith("App_Data\\", StringComparison.OrdinalIgnoreCase))
            {
                // Get the App_Data path from configuration
                var appDataPath = _configurationService.GetValueAsync<string>("SQUIRREL_APP_DATA_PATH").GetAwaiter().GetResult();
                var resolvedAppDataPath = Path.IsPathRooted(appDataPath)
                    ? appDataPath
                    : Path.Combine(AppContext.BaseDirectory, appDataPath);
                
                // Replace App_Data with the resolved app data path
                var relativePath = configuredPath.Substring("App_Data/".Length);
                _basePath = Path.Combine(resolvedAppDataPath, relativePath);
            }
            else
            {
                // For other relative paths, make them relative to the executable directory
                _basePath = Path.Combine(AppContext.BaseDirectory, configuredPath);
            }
        }
        
        _logger.LogInformation("File storage path resolved to: {Path}", _basePath);
        
        // Ensure base directory exists
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
            _logger.LogInformation("Created file storage directory: {Path}", _basePath);
        }
    }
    
    public async Task<string> SaveFileAsync(
        Stream stream,
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            await stream.CopyToAsync(fileStream, cancellationToken);
            
            _logger.LogInformation("Saved file to: {Path}", fullPath);
            return path;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file: {Path}", path);
            throw new FileStorageException(
                $"Failed to save file: {path}",
                ex,
                path,
                ProviderId);
        }
    }
    
    public async Task<Stream> GetFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = GetFullPath(path);
            
            if (!System.IO.File.Exists(fullPath))
            {
                throw new EntityNotFoundException("File", path);
            }
            
            var memoryStream = new MemoryStream();
            using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            await fileStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            
            return memoryStream;
        }
        catch (EntityNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve file: {Path}", path);
            throw new FileStorageException(
                $"Failed to retrieve file: {path}",
                ex,
                path,
                ProviderId);
        }
    }
    
    public Task DeleteFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = GetFullPath(path);
            
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
                _logger.LogInformation("Deleted file: {Path}", fullPath);
            }
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file: {Path}", path);
            throw new FileStorageException(
                $"Failed to delete file: {path}",
                ex,
                path,
                ProviderId);
        }
    }
    
    public Task<bool> FileExistsAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        return Task.FromResult(System.IO.File.Exists(fullPath));
    }
    
    public Task<FileStorageInfo> GetFileInfoAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = GetFullPath(path);
            var fileInfo = new FileInfo(fullPath);
            
            if (!fileInfo.Exists)
            {
                throw new EntityNotFoundException("File", path);
            }
            
            var storageInfo = new FileStorageInfo
            {
                Path = path,
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTimeUtc,
                ContentType = GetContentType(path)
            };
            
            return Task.FromResult(storageInfo);
        }
        catch (EntityNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file info: {Path}", path);
            throw new FileStorageException(
                $"Failed to get file info: {path}",
                ex,
                path,
                ProviderId);
        }
    }
    
    public async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sourceFullPath = GetFullPath(sourcePath);
            var destFullPath = GetFullPath(destinationPath);
            
            if (!System.IO.File.Exists(sourceFullPath))
            {
                throw new EntityNotFoundException("File", sourcePath);
            }
            
            var destDirectory = Path.GetDirectoryName(destFullPath);
            if (destDirectory != null && !Directory.Exists(destDirectory))
            {
                Directory.CreateDirectory(destDirectory);
            }
            
            using var sourceStream = new FileStream(sourceFullPath, FileMode.Open, FileAccess.Read);
            using var destStream = new FileStream(destFullPath, FileMode.Create, FileAccess.Write);
            await sourceStream.CopyToAsync(destStream, cancellationToken);
            
            _logger.LogInformation("Copied file from {Source} to {Destination}", sourcePath, destinationPath);
        }
        catch (EntityNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy file from {Source} to {Destination}", sourcePath, destinationPath);
            throw new FileStorageException(
                $"Failed to copy file from {sourcePath} to {destinationPath}",
                ex,
                sourcePath,
                ProviderId);
        }
    }
    
    public Task MoveFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sourceFullPath = GetFullPath(sourcePath);
            var destFullPath = GetFullPath(destinationPath);
            
            if (!System.IO.File.Exists(sourceFullPath))
            {
                throw new EntityNotFoundException("File", sourcePath);
            }
            
            var destDirectory = Path.GetDirectoryName(destFullPath);
            if (destDirectory != null && !Directory.Exists(destDirectory))
            {
                Directory.CreateDirectory(destDirectory);
            }
            
            System.IO.File.Move(sourceFullPath, destFullPath);
            _logger.LogInformation("Moved file from {Source} to {Destination}", sourcePath, destinationPath);
            
            return Task.CompletedTask;
        }
        catch (EntityNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move file from {Source} to {Destination}", sourcePath, destinationPath);
            throw new FileStorageException(
                $"Failed to move file from {sourcePath} to {destinationPath}",
                ex,
                sourcePath,
                ProviderId);
        }
    }
    
    private string GetFullPath(string relativePath)
    {
        // Normalize path separators
        relativePath = relativePath.Replace('\\', Path.DirectorySeparatorChar)
                                   .Replace('/', Path.DirectorySeparatorChar);
        
        // Remove leading separator if present
        if (relativePath.StartsWith(Path.DirectorySeparatorChar))
        {
            relativePath = relativePath.TrimStart(Path.DirectorySeparatorChar);
        }
        
        var fullPath = Path.Combine(_basePath, relativePath);
        
        // Security check: ensure the path is within the base directory
        var normalizedFullPath = Path.GetFullPath(fullPath);
        var normalizedBasePath = Path.GetFullPath(_basePath);
        
        if (!normalizedFullPath.StartsWith(normalizedBasePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new FileStorageException(
                "Path traversal attempt detected",
                relativePath,
                ProviderId);
        }
        
        return normalizedFullPath;
    }
    
    private static string GetContentType(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }
    
    /// <summary>
    /// Computes SHA256 hash of a stream
    /// </summary>
    public static async Task<string> ComputeHashAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
