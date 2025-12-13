using AutoMapper;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Events;
using Squirrel.Wiki.Core.Events.Files;
using Squirrel.Wiki.Core.Exceptions;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Services.Caching;
using Squirrel.Wiki.Core.Services.Content;
using static Squirrel.Wiki.Core.Services.Files.FileServiceErrorCodes;

namespace Squirrel.Wiki.Core.Services.Files;

/// <summary>
/// Service implementation for folder management operations
/// </summary>
public class FolderService : BaseService, IFolderService
{
    private readonly IFolderRepository _folderRepository;
    private readonly IFileRepository _fileRepository;
    private readonly ISlugGenerator _slugGenerator;
    private const string CacheKeyPrefix = "folder:";
    private const string CacheKeyTree = "folder:tree";
    private const int MaxFolderDepth = 10;

    public FolderService(
        IFolderRepository folderRepository,
        IFileRepository fileRepository,
        ICacheService cacheService,
        ISlugGenerator slugGenerator,
        ILogger<FolderService> logger,
        IEventPublisher eventPublisher,
        IMapper mapper,
        IConfigurationService? configuration = null)
        : base(logger, cacheService, eventPublisher, mapper, configuration)
    {
        _folderRepository = folderRepository;
        _fileRepository = fileRepository;
        _slugGenerator = slugGenerator;
    }

    public async Task<Result<FolderDto>> CreateFolderAsync(FolderCreateDto createDto, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate parent exists if specified
            if (createDto.ParentFolderId.HasValue)
            {
                var parent = await _folderRepository.GetByIdAsync(createDto.ParentFolderId.Value, cancellationToken);
                if (parent == null)
                {
                    return Result<FolderDto>.Failure($"Parent folder with ID {createDto.ParentFolderId.Value} not found", "PARENT_NOT_FOUND");
                }

                // Check depth limit
                var depth = await GetFolderDepthInternalAsync(createDto.ParentFolderId.Value, cancellationToken);
                if (depth >= MaxFolderDepth - 1)
                {
                    return Result<FolderDto>.Failure($"Maximum folder depth of {MaxFolderDepth} would be exceeded", "MAX_DEPTH_EXCEEDED");
                }
            }

            // Check for duplicate name in same parent
            var siblings = await _folderRepository.GetByParentIdAsync(createDto.ParentFolderId ?? 0, cancellationToken);
            if (siblings.Any(s => s.Name.Equals(createDto.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return Result<FolderDto>.Failure($"A folder named '{createDto.Name}' already exists in this location", "DUPLICATE_NAME");
            }

            // Generate slug
            var slug = _slugGenerator.GenerateSlug(createDto.Name);

            // Create folder
            var folder = new Folder
            {
                Name = createDto.Name,
                Slug = slug,
                Description = createDto.Description,
                ParentFolderId = createDto.ParentFolderId,
                CreatedBy = createDto.CreatedBy,
                CreatedOn = DateTime.UtcNow
            };

            await _folderRepository.AddAsync(folder, cancellationToken);

            LogInfo("Created folder {FolderName} with ID {FolderId}", folder.Name, folder.Id);

            // Publish event
            await EventPublisher.PublishAsync(
                new FolderCreatedEvent(folder.Id, folder.Name, folder.Slug, folder.ParentFolderId, folder.CreatedBy),
                cancellationToken);

            // Invalidate cache
            await InvalidateCacheAsync(cancellationToken);

            // Map to DTO
            var dto = await MapToDtoAsync(folder, cancellationToken);
            return Result<FolderDto>.Success(dto);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error creating folder {FolderName}", createDto.Name);
            return Result<FolderDto>.Failure($"Error creating folder: {ex.Message}", "CREATE_ERROR");
        }
    }

    public async Task<Result<FolderDto>> GetFolderByIdAsync(int folderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"{CacheKeyPrefix}{folderId}";
            var cached = await Cache.GetAsync<FolderDto>(cacheKey, cancellationToken);
            if (cached != null)
            {
                return Result<FolderDto>.Success(cached);
            }

            var folder = await _folderRepository.GetByIdAsync(folderId, cancellationToken);
            if (folder == null)
            {
                return Result<FolderDto>.Failure($"Folder with ID {folderId} not found", NOT_FOUND);
            }

            var dto = await MapToDtoAsync(folder, cancellationToken);
            await Cache.SetAsync(cacheKey, dto, null, cancellationToken);

            return Result<FolderDto>.Success(dto);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error getting folder by ID {FolderId}", folderId);
            return Result<FolderDto>.Failure($"Error retrieving folder: {ex.Message}", GET_ERROR);
        }
    }

    public async Task<Result<FolderDto>> GetFolderBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            var folder = await _folderRepository.GetBySlugAsync(slug, cancellationToken);
            if (folder == null)
            {
                return Result<FolderDto>.Failure($"Folder with slug '{slug}' not found", NOT_FOUND);
            }

            var dto = await MapToDtoAsync(folder, cancellationToken);
            return Result<FolderDto>.Success(dto);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error getting folder by slug {Slug}", slug);
            return Result<FolderDto>.Failure($"Error retrieving folder: {ex.Message}", GET_ERROR);
        }
    }

    public async Task<Result<List<FolderDto>>> GetRootFoldersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var folders = await _folderRepository.GetRootFoldersAsync(cancellationToken);
            var dtos = new List<FolderDto>();

            foreach (var folder in folders)
            {
                dtos.Add(await MapToDtoAsync(folder, cancellationToken));
            }

            return Result<List<FolderDto>>.Success(dtos);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error getting root folders");
            return Result<List<FolderDto>>.Failure($"Error retrieving root folders: {ex.Message}", GET_ERROR);
        }
    }

    public async Task<Result<List<FolderDto>>> GetChildFoldersAsync(int? parentFolderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var folders = await _folderRepository.GetByParentIdAsync(parentFolderId ?? 0, cancellationToken);
            var dtos = new List<FolderDto>();

            foreach (var folder in folders)
            {
                dtos.Add(await MapToDtoAsync(folder, cancellationToken));
            }

            return Result<List<FolderDto>>.Success(dtos);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error getting child folders for parent {ParentId}", parentFolderId);
            return Result<List<FolderDto>>.Failure($"Error retrieving child folders: {ex.Message}", GET_ERROR);
        }
    }

    public async Task<Result<List<FolderTreeDto>>> GetFolderTreeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cached = await Cache.GetAsync<List<FolderTreeDto>>(CacheKeyTree, cancellationToken);
            if (cached != null)
            {
                return Result<List<FolderTreeDto>>.Success(cached);
            }

            var rootFolders = await _folderRepository.GetRootFoldersAsync(cancellationToken);
            var tree = new List<FolderTreeDto>();

            foreach (var root in rootFolders)
            {
                tree.Add(await BuildTreeNodeAsync(root, cancellationToken));
            }

            await Cache.SetAsync(CacheKeyTree, tree, null, cancellationToken);
            return Result<List<FolderTreeDto>>.Success(tree);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error building folder tree");
            return Result<List<FolderTreeDto>>.Failure($"Error building folder tree: {ex.Message}", "TREE_ERROR");
        }
    }

    public async Task<Result<FolderDto>> UpdateFolderAsync(int folderId, FolderUpdateDto updateDto, string updatedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var folder = await _folderRepository.GetByIdAsync(folderId, cancellationToken);
            if (folder == null)
            {
                return Result<FolderDto>.Failure($"Folder with ID {folderId} not found", NOT_FOUND);
            }

            var changes = new Dictionary<string, object>();

            // Update name if provided
            if (!string.IsNullOrWhiteSpace(updateDto.Name) && updateDto.Name != folder.Name)
            {
                // Check for duplicate name in same parent
                var siblings = await _folderRepository.GetByParentIdAsync(folder.ParentFolderId ?? 0, cancellationToken);
                if (siblings.Any(s => s.Id != folderId && s.Name.Equals(updateDto.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return Result<FolderDto>.Failure($"A folder named '{updateDto.Name}' already exists in this location", "DUPLICATE_NAME");
                }

                changes["Name"] = $"{folder.Name} -> {updateDto.Name}";
                folder.Name = updateDto.Name;
                folder.Slug = _slugGenerator.GenerateSlug(updateDto.Name);
            }

            // Update description if provided
            if (updateDto.Description != null && updateDto.Description != folder.Description)
            {
                changes["Description"] = $"{folder.Description} -> {updateDto.Description}";
                folder.Description = updateDto.Description;
            }

            // Update display order if provided
            if (updateDto.DisplayOrder.HasValue && updateDto.DisplayOrder.Value != folder.DisplayOrder)
            {
                changes["DisplayOrder"] = $"{folder.DisplayOrder} -> {updateDto.DisplayOrder.Value}";
                folder.DisplayOrder = updateDto.DisplayOrder.Value;
            }

            await _folderRepository.UpdateAsync(folder, cancellationToken);

            LogInfo("Updated folder {FolderName} (ID: {FolderId})", folder.Name, folder.Id);

            // Publish event
            await EventPublisher.PublishAsync(
                new FolderUpdatedEvent(folder.Id, folder.Name, updatedBy, changes),
                cancellationToken);

            // Invalidate cache
            await InvalidateCacheAsync(cancellationToken);

            var dto = await MapToDtoAsync(folder, cancellationToken);
            return Result<FolderDto>.Success(dto);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error updating folder {FolderId}", folderId);
            return Result<FolderDto>.Failure($"Error updating folder: {ex.Message}", "UPDATE_ERROR");
        }
    }

    public async Task<Result<FolderDto>> MoveFolderAsync(int folderId, int? newParentFolderId, string movedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var folder = await _folderRepository.GetByIdAsync(folderId, cancellationToken);
            if (folder == null)
            {
                return Result<FolderDto>.Failure($"Folder with ID {folderId} not found", NOT_FOUND);
            }

            // Validate new parent exists if specified
            if (newParentFolderId.HasValue)
            {
                var newParent = await _folderRepository.GetByIdAsync(newParentFolderId.Value, cancellationToken);
                if (newParent == null)
                {
                    return Result<FolderDto>.Failure($"Target parent folder with ID {newParentFolderId.Value} not found", "PARENT_NOT_FOUND");
                }

                // Check for circular reference
                if (!await ValidateMoveAsync(folderId, newParentFolderId.Value, cancellationToken))
                {
                    return Result<FolderDto>.Failure("Cannot move folder: would create circular reference", "CIRCULAR_REFERENCE");
                }

                // Check depth limit
                var newDepth = await GetFolderDepthInternalAsync(newParentFolderId.Value, cancellationToken);
                var subtreeDepth = await GetSubtreeDepthAsync(folderId, cancellationToken);
                if (newDepth + subtreeDepth + 1 > MaxFolderDepth)
                {
                    return Result<FolderDto>.Failure($"Maximum folder depth of {MaxFolderDepth} would be exceeded", "MAX_DEPTH_EXCEEDED");
                }
            }

            var oldParentId = folder.ParentFolderId;
            folder.ParentFolderId = newParentFolderId;
            await _folderRepository.UpdateAsync(folder, cancellationToken);

            LogInfo("Moved folder {FolderName} (ID: {FolderId}) from parent {OldParent} to {NewParent}",
                folder.Name, folder.Id, oldParentId, newParentFolderId);

            // Publish event
            await EventPublisher.PublishAsync(
                new FolderUpdatedEvent(folder.Id, folder.Name, movedBy, new Dictionary<string, object>
                {
                    ["ParentFolderId"] = $"{oldParentId} -> {newParentFolderId}"
                }),
                cancellationToken);

            // Invalidate cache
            await InvalidateCacheAsync(cancellationToken);

            var dto = await MapToDtoAsync(folder, cancellationToken);
            return Result<FolderDto>.Success(dto);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error moving folder {FolderId}", folderId);
            return Result<FolderDto>.Failure($"Error moving folder: {ex.Message}", "MOVE_ERROR");
        }
    }

    public async Task<Result> DeleteFolderAsync(int folderId, string deletedBy, bool recursive = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var folder = await _folderRepository.GetByIdAsync(folderId, cancellationToken);
            if (folder == null)
            {
                return Result.Failure($"Folder with ID {folderId} not found", NOT_FOUND);
            }

            // Check for child folders
            var hasChildren = await _folderRepository.HasChildrenAsync(folderId, cancellationToken);
            if (hasChildren && !recursive)
            {
                return Result.Failure("Folder has subfolders. Set recursive=true to delete them", "HAS_CHILDREN");
            }

            // Check for files
            var hasFiles = await _folderRepository.HasFilesAsync(folderId, cancellationToken);
            if (hasFiles && !recursive)
            {
                return Result.Failure("Folder contains files. Set recursive=true to delete them", "HAS_FILES");
            }

            // Delete recursively if needed
            if (recursive)
            {
                var children = await _folderRepository.GetChildrenAsync(folderId, cancellationToken);
                foreach (var child in children)
                {
                    await DeleteFolderAsync(child.Id, deletedBy, true, cancellationToken);
                }
            }

            // Soft delete folder (mark as deleted)
            await _folderRepository.DeleteAsync(folder, cancellationToken);

            LogInfo("Deleted folder {FolderName} (ID: {FolderId})", folder.Name, folder.Id);

            // Publish event
            await EventPublisher.PublishAsync(
                new FolderDeletedEvent(folder.Id, folder.Name, folder.ParentFolderId, deletedBy),
                cancellationToken);

            // Invalidate cache
            await InvalidateCacheAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            LogError(ex, "Error deleting folder {FolderId}", folderId);
            return Result.Failure($"Error deleting folder: {ex.Message}", "DELETE_ERROR");
        }
    }

    public async Task<Result<List<FolderDto>>> GetFolderBreadcrumbAsync(int folderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var breadcrumb = new List<FolderDto>();
            var current = await _folderRepository.GetByIdAsync(folderId, cancellationToken);

            while (current != null)
            {
                breadcrumb.Insert(0, await MapToDtoAsync(current, cancellationToken));

                if (current.ParentFolderId.HasValue)
                {
                    current = await _folderRepository.GetByIdAsync(current.ParentFolderId.Value, cancellationToken);
                }
                else
                {
                    break;
                }
            }

            return Result<List<FolderDto>>.Success(breadcrumb);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error getting breadcrumb for folder {FolderId}", folderId);
            return Result<List<FolderDto>>.Failure($"Error getting folder breadcrumb: {ex.Message}", "BREADCRUMB_ERROR");
        }
    }

    public async Task<Result<int>> GetFolderDepthAsync(int folderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var depth = await GetFolderDepthInternalAsync(folderId, cancellationToken);
            return Result<int>.Success(depth);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error getting depth for folder {FolderId}", folderId);
            return Result<int>.Failure($"Error getting folder depth: {ex.Message}", "DEPTH_ERROR");
        }
    }

    private async Task<int> GetFolderDepthInternalAsync(int folderId, CancellationToken cancellationToken)
    {
        var depth = 0;
        var current = await _folderRepository.GetByIdAsync(folderId, cancellationToken);

        while (current?.ParentFolderId != null)
        {
            depth++;
            current = await _folderRepository.GetByIdAsync(current.ParentFolderId.Value, cancellationToken);
        }

        return depth;
    }

    private async Task<int> GetSubtreeDepthAsync(int folderId, CancellationToken cancellationToken)
    {
        var children = await _folderRepository.GetChildrenAsync(folderId, cancellationToken);
        if (!children.Any())
        {
            return 0;
        }

        var maxChildDepth = 0;
        foreach (var child in children)
        {
            var childDepth = await GetSubtreeDepthAsync(child.Id, cancellationToken);
            maxChildDepth = Math.Max(maxChildDepth, childDepth);
        }

        return maxChildDepth + 1;
    }

    private async Task<bool> ValidateMoveAsync(int folderId, int newParentId, CancellationToken cancellationToken)
    {
        if (folderId == newParentId)
        {
            return false;
        }

        var current = await _folderRepository.GetByIdAsync(newParentId, cancellationToken);
        while (current != null)
        {
            if (current.Id == folderId)
            {
                return false;
            }

            if (current.ParentFolderId.HasValue)
            {
                current = await _folderRepository.GetByIdAsync(current.ParentFolderId.Value, cancellationToken);
            }
            else
            {
                break;
            }
        }

        return true;
    }

    private async Task<FolderDto> MapToDtoAsync(Folder folder, CancellationToken cancellationToken)
    {
        var dto = Mapper.Map<FolderDto>(folder);
        dto.FileCount = await _fileRepository.GetCountByFolderAsync(folder.Id, cancellationToken);
        dto.SubFolderCount = (await _folderRepository.GetChildrenAsync(folder.Id, cancellationToken)).Count();
        dto.Path = await _folderRepository.GetFolderPathAsync(folder.Id, cancellationToken);
        return dto;
    }

    private async Task<FolderTreeDto> BuildTreeNodeAsync(Folder folder, CancellationToken cancellationToken)
    {
        var node = Mapper.Map<FolderTreeDto>(folder);
        node.FileCount = await _fileRepository.GetCountByFolderAsync(folder.Id, cancellationToken);

        var children = await _folderRepository.GetChildrenAsync(folder.Id, cancellationToken);
        node.Children = new List<FolderTreeDto>();

        foreach (var child in children)
        {
            node.Children.Add(await BuildTreeNodeAsync(child, cancellationToken));
        }

        return node;
    }

    private async Task InvalidateCacheAsync(CancellationToken cancellationToken)
    {
        await Cache.RemoveByPatternAsync($"{CacheKeyPrefix}*", cancellationToken);
        await Cache.RemoveAsync(CacheKeyTree, cancellationToken);
    }
}
