using AutoMapper;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Contracts.Storage;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Events;
using Squirrel.Wiki.Core.Events.Files;
using Squirrel.Wiki.Core.Exceptions;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Services.Caching;
using Squirrel.Wiki.Core.Services.Infrastructure;
using FileEntity = Squirrel.Wiki.Core.Database.Entities.File;

namespace Squirrel.Wiki.Core.Services.Files;

/// <summary>
/// Service implementation for file management operations
/// </summary>
public class FileService : BaseService, IFileService
{
    private readonly IFileRepository _fileRepository;
    private readonly IFolderRepository _folderRepository;
    private readonly IFileStorageStrategy _storageStrategy;
    private readonly IConfigurationService _configurationService;
    private const string CacheKeyPrefix = "file:";

    public FileService(
        IFileRepository fileRepository,
        IFolderRepository folderRepository,
        IFileStorageStrategy storageStrategy,
        ICacheService cacheService,
        ILogger<FileService> logger,
        IEventPublisher eventPublisher,
        IMapper mapper,
        IConfigurationService configurationService)
        : base(logger, cacheService, eventPublisher, mapper, configurationService)
    {
        _fileRepository = fileRepository;
        _folderRepository = folderRepository;
        _storageStrategy = storageStrategy;
        _configurationService = configurationService;
    }

    public async Task<Result<FileDto>> UploadFileAsync(FileUploadDto uploadDto, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Validate file size
            var maxFileSize = await _configurationService.GetValueAsync<long>("SQUIRREL_FILE_MAX_SIZE", cancellationToken);
            if (uploadDto.FileSize > maxFileSize)
            {
                throw new FileSizeExceededException(uploadDto.FileSize, maxFileSize);
            }

            // 2. Validate file type
            var extension = Path.GetExtension(uploadDto.FileName).ToLowerInvariant();
            var allowedExtensions = await _configurationService.GetValueAsync<string>("SQUIRREL_FILE_ALLOWED_EXTENSIONS", cancellationToken);
            if (!await IsAllowedExtensionAsync(extension, allowedExtensions))
            {
                throw new FileTypeNotAllowedException(extension, allowedExtensions);
            }

            // 3. Validate folder exists if specified
            if (uploadDto.FolderId.HasValue)
            {
                var folder = await _folderRepository.GetByIdAsync(uploadDto.FolderId.Value, cancellationToken);
                if (folder == null)
                {
                    return Result<FileDto>.Failure($"Folder with ID {uploadDto.FolderId.Value} not found", "FOLDER_NOT_FOUND");
                }
            }

            // 4. Compute SHA256 hash
            uploadDto.FileStream.Position = 0;
            var fileHash = await LocalFileStorageStrategy.ComputeHashAsync(uploadDto.FileStream, cancellationToken);

            // 5. Check if file with same hash already exists (deduplication)
            var existingFiles = await _fileRepository.GetByHashAsync(fileHash, cancellationToken);
            var existingFile = existingFiles.FirstOrDefault(f => !f.IsDeleted);

            string storagePath;
            if (existingFile == null)
            {
                // 6. Save physical file (new content)
                storagePath = GenerateStoragePath(fileHash, extension);
                uploadDto.FileStream.Position = 0;
                await _storageStrategy.SaveFileAsync(uploadDto.FileStream, storagePath, cancellationToken);
                LogInfo("Created new physical file with hash {FileHash} at {StoragePath}", fileHash, storagePath);
            }
            else
            {
                // Reuse existing storage path
                storagePath = existingFile.FilePath;
                LogInfo("Reusing existing physical file with hash {FileHash}", fileHash);
            }

            // 7. Create File record
            var file = new FileEntity
            {
                FileName = uploadDto.FileName,
                FileHash = fileHash,
                FilePath = storagePath,
                FileSize = uploadDto.FileSize,
                ContentType = uploadDto.ContentType,
                Description = uploadDto.Description,
                FolderId = uploadDto.FolderId,
                StorageProvider = _storageStrategy.ProviderId,
                Visibility = uploadDto.Visibility,
                UploadedBy = "system", // TODO: Get from user context
                UploadedOn = DateTime.UtcNow,
                CurrentVersion = 1,
                IsDeleted = false
            };
            await _fileRepository.AddAsync(file, cancellationToken);

            LogInfo("Uploaded file {FileName} with ID {FileId} (hash: {FileHash})", 
                file.FileName, file.Id, fileHash);

            // 8. Publish event
            await EventPublisher.PublishAsync(
                new FileUploadedEvent(file.Id, file.FileName, fileHash, uploadDto.FileSize, 
                    uploadDto.FolderId, file.UploadedBy),
                cancellationToken);

            // 9. Invalidate cache
            await InvalidateCacheAsync(cancellationToken);

            // 10. Map to DTO and return
            var dto = await MapToDtoAsync(file, cancellationToken);
            return Result<FileDto>.Success(dto);
        }
        catch (FileSizeExceededException ex)
        {
            LogError(ex, "File size exceeded for {FileName}", uploadDto.FileName);
            return Result<FileDto>.Failure(ex.Message, "FILE_SIZE_EXCEEDED");
        }
        catch (FileTypeNotAllowedException ex)
        {
            LogError(ex, "File type not allowed for {FileName}", uploadDto.FileName);
            return Result<FileDto>.Failure(ex.Message, "FILE_TYPE_NOT_ALLOWED");
        }
        catch (Exception ex)
        {
            LogError(ex, "Error uploading file {FileName}", uploadDto.FileName);
            return Result<FileDto>.Failure($"Error uploading file: {ex.Message}", "UPLOAD_ERROR");
        }
    }

    public async Task<Result<List<FileDto>>> UploadMultipleFilesAsync(List<FileUploadDto> uploadDtos, CancellationToken cancellationToken = default)
    {
        try
        {
            var results = new List<FileDto>();

            foreach (var uploadDto in uploadDtos)
            {
                var result = await UploadFileAsync(uploadDto, cancellationToken);
                if (result.IsSuccess)
                {
                    results.Add(result.Value!);
                }
                else
                {
                    LogWarning("Failed to upload file {FileName}: {Error}", uploadDto.FileName, result.Error);
                }
            }

            return Result<List<FileDto>>.Success(results);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error uploading multiple files");
            return Result<List<FileDto>>.Failure($"Error uploading files: {ex.Message}", "UPLOAD_MULTIPLE_ERROR");
        }
    }

    public async Task<Result<FileDto>> GetFileByIdAsync(int fileId, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"{CacheKeyPrefix}{fileId}";
            var cached = await Cache.GetAsync<FileDto>(cacheKey, cancellationToken);
            if (cached != null)
            {
                return Result<FileDto>.Success(cached);
            }

            var file = await _fileRepository.GetByIdAsync(fileId, cancellationToken);
            if (file == null)
            {
                return Result<FileDto>.Failure($"File with ID {fileId} not found", "NOT_FOUND");
            }

            var dto = await MapToDtoAsync(file, cancellationToken);
            await Cache.SetAsync(cacheKey, dto, null, cancellationToken);

            return Result<FileDto>.Success(dto);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error getting file by ID {FileId}", fileId);
            return Result<FileDto>.Failure($"Error retrieving file: {ex.Message}", "GET_ERROR");
        }
    }

    public async Task<Result<FileDto>> GetFileByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var file = await _fileRepository.GetByPathAsync(path, cancellationToken);
            if (file == null)
            {
                return Result<FileDto>.Failure($"File at path '{path}' not found", "NOT_FOUND");
            }

            var dto = await MapToDtoAsync(file, cancellationToken);
            return Result<FileDto>.Success(dto);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error getting file by path {Path}", path);
            return Result<FileDto>.Failure($"Error retrieving file: {ex.Message}", "GET_ERROR");
        }
    }

    public async Task<Result<List<FileDto>>> GetFilesByFolderAsync(int? folderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var files = await _fileRepository.GetByFolderAsync(folderId, cancellationToken);
            var dtos = new List<FileDto>();

            foreach (var file in files)
            {
                dtos.Add(await MapToDtoAsync(file, cancellationToken));
            }

            return Result<List<FileDto>>.Success(dtos);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error getting files by folder {FolderId}", folderId);
            return Result<List<FileDto>>.Failure($"Error retrieving files: {ex.Message}", "GET_ERROR");
        }
    }

    public async Task<Result<(Stream Stream, string FileName, string ContentType)>> DownloadFileAsync(int fileId, CancellationToken cancellationToken = default)
    {
        try
        {
            var file = await _fileRepository.GetByIdAsync(fileId, cancellationToken);
            if (file == null)
            {
                return Result<(Stream, string, string)>.Failure($"File with ID {fileId} not found", "NOT_FOUND");
            }

            // Get file stream from storage
            var stream = await _storageStrategy.GetFileAsync(file.FilePath, cancellationToken);

            LogInfo("Downloaded file {FileName} (ID: {FileId})", file.FileName, file.Id);

            return Result<(Stream, string, string)>.Success((
                stream,
                file.FileName,
                file.ContentType));
        }
        catch (Exception ex)
        {
            LogError(ex, "Error downloading file {FileId}", fileId);
            return Result<(Stream, string, string)>.Failure($"Error downloading file: {ex.Message}", "DOWNLOAD_ERROR");
        }
    }

    public async Task<Result<FileDto>> UpdateFileAsync(int fileId, FileUpdateDto updateDto, string updatedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var file = await _fileRepository.GetByIdAsync(fileId, cancellationToken);
            if (file == null)
            {
                return Result<FileDto>.Failure($"File with ID {fileId} not found", "NOT_FOUND");
            }

            var changes = new Dictionary<string, object>();

            // Update file name if provided
            if (!string.IsNullOrWhiteSpace(updateDto.FileName) && updateDto.FileName != file.FileName)
            {
                changes["FileName"] = $"{file.FileName} -> {updateDto.FileName}";
                file.FileName = updateDto.FileName;
            }

            // Update description if provided
            if (updateDto.Description != null && updateDto.Description != file.Description)
            {
                changes["Description"] = $"{file.Description} -> {updateDto.Description}";
                file.Description = updateDto.Description;
            }

            // Update visibility if provided
            if (updateDto.Visibility.HasValue && updateDto.Visibility.Value != file.Visibility)
            {
                changes["Visibility"] = $"{file.Visibility} -> {updateDto.Visibility.Value}";
                file.Visibility = updateDto.Visibility.Value;
            }

            await _fileRepository.UpdateAsync(file, cancellationToken);

            LogInfo("Updated file {FileName} (ID: {FileId})", file.FileName, file.Id);

            // Publish event
            await EventPublisher.PublishAsync(
                new FileUpdatedEvent(file.Id, file.FileName, updatedBy, changes),
                cancellationToken);

            // Invalidate cache
            await InvalidateCacheAsync(cancellationToken);

            var dto = await MapToDtoAsync(file, cancellationToken);
            return Result<FileDto>.Success(dto);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error updating file {FileId}", fileId);
            return Result<FileDto>.Failure($"Error updating file: {ex.Message}", "UPDATE_ERROR");
        }
    }

    public async Task<Result<FileDto>> MoveFileAsync(int fileId, int? newFolderId, string movedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var file = await _fileRepository.GetByIdAsync(fileId, cancellationToken);
            if (file == null)
            {
                return Result<FileDto>.Failure($"File with ID {fileId} not found", "NOT_FOUND");
            }

            // Validate new folder exists if specified
            if (newFolderId.HasValue)
            {
                var folder = await _folderRepository.GetByIdAsync(newFolderId.Value, cancellationToken);
                if (folder == null)
                {
                    return Result<FileDto>.Failure($"Target folder with ID {newFolderId.Value} not found", "FOLDER_NOT_FOUND");
                }
            }

            var oldFolderId = file.FolderId;
            file.FolderId = newFolderId;
            await _fileRepository.UpdateAsync(file, cancellationToken);

            LogInfo("Moved file {FileName} (ID: {FileId}) from folder {OldFolder} to {NewFolder}",
                file.FileName, file.Id, oldFolderId, newFolderId);

            // Publish event
            await EventPublisher.PublishAsync(
                new FileMovedEvent(file.Id, file.FileName, oldFolderId, newFolderId, movedBy),
                cancellationToken);

            // Invalidate cache
            await InvalidateCacheAsync(cancellationToken);

            var dto = await MapToDtoAsync(file, cancellationToken);
            return Result<FileDto>.Success(dto);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error moving file {FileId}", fileId);
            return Result<FileDto>.Failure($"Error moving file: {ex.Message}", "MOVE_ERROR");
        }
    }

    public async Task<Result> DeleteFileAsync(int fileId, string deletedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var file = await _fileRepository.GetByIdAsync(fileId, cancellationToken);
            if (file == null)
            {
                return Result.Failure($"File with ID {fileId} not found", "NOT_FOUND");
            }

            var fileHash = file.FileHash;
            var storagePath = file.FilePath;

            // Soft delete file record
            await _fileRepository.DeleteAsync(file, cancellationToken);

            LogInfo("Deleted file {FileName} (ID: {FileId})", file.FileName, file.Id);

            // Check if FileContent is still referenced by other active files
            var otherFiles = await _fileRepository.GetByHashAsync(fileHash, cancellationToken);
            var activeFiles = otherFiles.Where(f => !f.IsDeleted && f.Id != fileId).ToList();

            // If no other active files reference this content, delete physical file
            if (!activeFiles.Any())
            {
                try
                {
                    await _storageStrategy.DeleteFileAsync(storagePath, cancellationToken);
                    LogInfo("Deleted physical file at {StoragePath} (no longer referenced)", storagePath);
                }
                catch (Exception ex)
                {
                    LogWarning("Failed to delete physical file at {StoragePath}: {Error}", storagePath, ex.Message);
                    // Don't fail the operation if physical file deletion fails
                }
            }
            else
            {
                LogInfo("Physical file at {StoragePath} retained ({Count} other files reference it)", 
                    storagePath, activeFiles.Count);
            }

            // Publish event
            await EventPublisher.PublishAsync(
                new FileDeletedEvent(file.Id, file.FileName, fileHash, file.FolderId, deletedBy),
                cancellationToken);

            // Invalidate cache
            await InvalidateCacheAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            LogError(ex, "Error deleting file {FileId}", fileId);
            return Result.Failure($"Error deleting file: {ex.Message}", "DELETE_ERROR");
        }
    }

    public async Task<Result> PermanentlyDeleteFileAsync(int fileId, CancellationToken cancellationToken = default)
    {
        try
        {
            var file = await _fileRepository.GetByIdAsync(fileId, cancellationToken);
            if (file == null)
            {
                return Result.Failure($"File with ID {fileId} not found", "NOT_FOUND");
            }

            var storagePath = file.FilePath;

            // Hard delete file record from database
            await _fileRepository.DeleteAsync(file, cancellationToken);

            // Always delete physical file for permanent deletion
            try
            {
                await _storageStrategy.DeleteFileAsync(storagePath, cancellationToken);
                LogInfo("Permanently deleted file {FileName} (ID: {FileId}) and physical file at {StoragePath}",
                    file.FileName, file.Id, storagePath);
            }
            catch (Exception ex)
            {
                LogWarning("Failed to delete physical file at {StoragePath}: {Error}", storagePath, ex.Message);
            }

            // Invalidate cache
            await InvalidateCacheAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            LogError(ex, "Error permanently deleting file {FileId}", fileId);
            return Result.Failure($"Error permanently deleting file: {ex.Message}", "PERMANENT_DELETE_ERROR");
        }
    }

    public async Task<Result<List<FileDto>>> SearchFilesAsync(FileSearchDto searchDto, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use the repository search method - it doesn't support pagination yet
            var allFiles = await _fileRepository.GetByFolderAsync(searchDto.FolderId, cancellationToken);
            
            // Apply filters
            var files = allFiles.AsEnumerable();
            
            if (!string.IsNullOrWhiteSpace(searchDto.SearchTerm))
            {
                files = files.Where(f => f.FileName.Contains(searchDto.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                                        (f.Description != null && f.Description.Contains(searchDto.SearchTerm, StringComparison.OrdinalIgnoreCase)));
            }
            
            if (!string.IsNullOrWhiteSpace(searchDto.ContentType))
            {
                files = files.Where(f => f.ContentType.Equals(searchDto.ContentType, StringComparison.OrdinalIgnoreCase));
            }
            
            if (searchDto.UploadedAfter.HasValue)
            {
                files = files.Where(f => f.UploadedOn >= searchDto.UploadedAfter.Value);
            }
            
            if (searchDto.UploadedBefore.HasValue)
            {
                files = files.Where(f => f.UploadedOn <= searchDto.UploadedBefore.Value);
            }
            
            if (!string.IsNullOrWhiteSpace(searchDto.UploadedBy))
            {
                files = files.Where(f => f.UploadedBy.Equals(searchDto.UploadedBy, StringComparison.OrdinalIgnoreCase));
            }

            var dtos = new List<FileDto>();
            foreach (var file in files)
            {
                dtos.Add(await MapToDtoAsync(file, cancellationToken));
            }

            return Result<List<FileDto>>.Success(dtos);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error searching files");
            return Result<List<FileDto>>.Failure($"Error searching files: {ex.Message}", "SEARCH_ERROR");
        }
    }

    public async Task<Result<int>> GetFileUsageCountAsync(string fileHash, CancellationToken cancellationToken = default)
    {
        try
        {
            var files = await _fileRepository.GetByHashAsync(fileHash, cancellationToken);
            var activeCount = files.Count(f => !f.IsDeleted);
            return Result<int>.Success(activeCount);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error getting file usage count for hash {FileHash}", fileHash);
            return Result<int>.Failure($"Error getting file usage count: {ex.Message}", "USAGE_COUNT_ERROR");
        }
    }

    private string GenerateStoragePath(string fileHash, string extension)
    {
        // Use hash-based directory structure for better distribution
        // e.g., "ab/cd/abcdef123456.jpg"
        var dir1 = fileHash.Substring(0, 2);
        var dir2 = fileHash.Substring(2, 2);
        return $"{dir1}/{dir2}/{fileHash}{extension}";
    }

    private async Task<bool> IsAllowedExtensionAsync(string extension, string allowedExtensionsString)
    {
        var allowedExtensions = allowedExtensionsString
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim().ToLowerInvariant())
            .ToArray();
        
        return allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<FileDto> MapToDtoAsync(FileEntity file, CancellationToken cancellationToken)
    {
        var dto = Mapper.Map<FileDto>(file);
        dto.DownloadUrl = $"/files/download/{file.Id}";
        dto.ThumbnailUrl = null; // TODO: Implement thumbnail generation
        return dto;
    }

    private async Task InvalidateCacheAsync(CancellationToken cancellationToken)
    {
        await Cache.RemoveByPatternAsync($"{CacheKeyPrefix}*", cancellationToken);
    }
}
