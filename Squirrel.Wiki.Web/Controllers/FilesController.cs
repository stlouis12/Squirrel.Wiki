using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Services.Files;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Web.Services;

namespace Squirrel.Wiki.Web.Controllers;

/// <summary>
/// Controller for file management operations
/// </summary>
[Authorize]
public class FilesController : BaseController
{
    private readonly IFileService _fileService;
    private readonly IFolderService _folderService;
    private readonly IUserContext _userContext;
    private readonly Squirrel.Wiki.Core.Security.IAuthorizationService _authorizationService;
    private readonly IFileRepository _fileRepository;

    public FilesController(
        IFileService fileService,
        IFolderService folderService,
        IUserContext userContext,
        Squirrel.Wiki.Core.Security.IAuthorizationService authorizationService,
        IFileRepository fileRepository,
        ILogger<FilesController> logger,
        INotificationService notificationService)
        : base(logger, notificationService)
    {
        _fileService = fileService;
        _folderService = folderService;
        _userContext = userContext;
        _authorizationService = authorizationService;
        _fileRepository = fileRepository;
    }

    /// <summary>
    /// Display file manager interface
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(int? folderId)
    {
        try
        {
            // Get current folder if specified
            FolderDto? currentFolder = null;
            if (folderId.HasValue)
            {
                var folderResult = await _folderService.GetFolderByIdAsync(folderId.Value);
                if (!folderResult.IsSuccess)
                {
                    NotifyError($"Folder not found: {folderResult.Error}");
                    return RedirectToAction(nameof(Index));
                }
                currentFolder = folderResult.Value;
            }

            // Get folders in current location
            var foldersResult = folderId.HasValue
                ? await _folderService.GetChildFoldersAsync(folderId.Value)
                : await _folderService.GetRootFoldersAsync();

            // Get files in current location
            var filesResult = await _fileService.GetFilesByFolderAsync(folderId);

            // Filter files by visibility using batch authorization
            var visibleFiles = new List<FileDto>();
            IEnumerable<Core.Database.Entities.File> fileEntities = new List<Core.Database.Entities.File>();
            
            if (filesResult.IsSuccess && filesResult.Value.Any())
            {
                // Get file entities for authorization check
                var fileIds = filesResult.Value.Select(f => f.Id).ToList();
                fileEntities = await _fileRepository.GetByIdsAsync(fileIds);
                
                // Perform batch authorization check
                var viewPermissions = await _authorizationService.CanViewFilesAsync(fileEntities);
                
                // Filter files based on permissions
                visibleFiles = filesResult.Value
                    .Where(f => viewPermissions.GetValueOrDefault(f.Id, false))
                    .ToList();
                
                _logger.LogDebug(
                    "File list filtered by visibility for user {Username}: {VisibleCount}/{TotalCount} files visible",
                    _userContext.Username ?? "Anonymous",
                    visibleFiles.Count,
                    filesResult.Value.Count);
            }

            ViewBag.CurrentFolder = currentFolder;
            ViewBag.Folders = foldersResult.IsSuccess ? foldersResult.Value : new List<FolderDto>();
            ViewBag.Files = visibleFiles;

            // Get breadcrumb if in a folder
            if (folderId.HasValue)
            {
                var breadcrumbResult = await _folderService.GetFolderBreadcrumbAsync(folderId.Value);
                ViewBag.Breadcrumb = breadcrumbResult.IsSuccess ? breadcrumbResult.Value : new List<FolderDto>();
            }

            // Pass authorization flags to view
            ViewBag.CanUpload = await _authorizationService.CanUploadFileAsync();
            ViewBag.CanManageFolders = await _authorizationService.CanManageFoldersAsync();

            // For files, pass edit/delete permissions
            if (visibleFiles.Any())
            {
                var canEditPermissions = await _authorizationService.CanEditFilesAsync(fileEntities);
                var canDeletePermissions = await _authorizationService.CanDeleteFilesAsync(fileEntities);
                ViewBag.CanEditFiles = canEditPermissions;
                ViewBag.CanDeleteFiles = canDeletePermissions;
            }

            return View();
        }
        catch (Exception ex)
        {
            NotifyError($"Error loading file manager: {ex.Message}");
            return View();
        }
    }

    /// <summary>
    /// Upload file(s)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFileCollection files, int? folderId, string? description)
    {
        try
        {
            // Check authorization before uploading
            if (!await _authorizationService.CanUploadFileAsync())
            {
                _logger.LogWarning("User {Username} attempted to upload files without permission", 
                    _userContext.Username ?? "Anonymous");
                NotifyError("You don't have permission to upload files");
                return RedirectToAction(nameof(Index), new { folderId });
            }

            if (files == null || files.Count == 0)
            {
                NotifyError("No files selected for upload");
                return RedirectToAction(nameof(Index), new { folderId });
            }

            var uploadDtos = new List<FileUploadDto>();
            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    uploadDtos.Add(new FileUploadDto
                    {
                        FileName = file.FileName,
                        FileStream = file.OpenReadStream(),
                        FileSize = file.Length,
                        ContentType = file.ContentType,
                        Description = description,
                        FolderId = folderId,
                        Visibility = Core.Database.Entities.FileVisibility.Inherit
                    });
                }
            }

            if (uploadDtos.Count == 0)
            {
                NotifyError("No valid files to upload");
                return RedirectToAction(nameof(Index), new { folderId });
            }

            var result = await _fileService.UploadMultipleFilesAsync(uploadDtos);

            if (result.IsSuccess)
            {
                NotifySuccess($"Successfully uploaded {result.Value!.Count} file(s)");
            }
            else
            {
                NotifyError($"Error uploading files: {result.Error}");
            }

            return RedirectToAction(nameof(Index), new { folderId });
        }
        catch (Exception ex)
        {
            NotifyError($"Error uploading files: {ex.Message}");
            return RedirectToAction(nameof(Index), new { folderId });
        }
    }

    /// <summary>
    /// Download a file
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        try
        {
            // Check authorization before downloading
            var fileEntity = await _fileRepository.GetByIdAsync(id);
            if (fileEntity == null)
            {
                NotifyError("File not found");
                return RedirectToAction(nameof(Index));
            }

            if (!await _authorizationService.CanViewFileAsync(fileEntity))
            {
                _logger.LogWarning("User {Username} attempted to download file {FileId} without permission", 
                    _userContext.Username ?? "Anonymous", id);
                NotifyError("You don't have permission to download this file");
                return RedirectToAction(nameof(Index));
            }

            var result = await _fileService.DownloadFileAsync(id);

            if (!result.IsSuccess)
            {
                NotifyError($"Error downloading file: {result.Error}");
                return RedirectToAction(nameof(Index));
            }

            var (stream, fileName, contentType) = result.Value;
            return File(stream, contentType, fileName);
        }
        catch (Exception ex)
        {
            NotifyError($"Error downloading file: {ex.Message}");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Show file details
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            // Check authorization before showing details
            var fileEntity = await _fileRepository.GetByIdAsync(id);
            if (fileEntity == null)
            {
                NotifyError("File not found");
                return RedirectToAction(nameof(Index));
            }

            if (!await _authorizationService.CanViewFileAsync(fileEntity))
            {
                _logger.LogWarning("User {Username} attempted to view file {FileId} details without permission", 
                    _userContext.Username ?? "Anonymous", id);
                NotifyError("You don't have permission to view this file");
                return RedirectToAction(nameof(Index));
            }

            var result = await _fileService.GetFileByIdAsync(id);

            if (!result.IsSuccess)
            {
                NotifyError($"File not found: {result.Error}");
                return RedirectToAction(nameof(Index));
            }

            return View(result.Value);
        }
        catch (Exception ex)
        {
            NotifyError($"Error loading file details: {ex.Message}");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Show edit file form
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            // Check authorization before showing edit form
            var fileEntity = await _fileRepository.GetByIdAsync(id);
            if (fileEntity == null)
            {
                NotifyError("File not found");
                return RedirectToAction(nameof(Index));
            }

            if (!await _authorizationService.CanEditFileAsync(fileEntity))
            {
                _logger.LogWarning("User {Username} attempted to edit file {FileId} without permission", 
                    _userContext.Username ?? "Anonymous", id);
                NotifyError("You don't have permission to edit this file");
                return RedirectToAction(nameof(Index));
            }

            var result = await _fileService.GetFileByIdAsync(id);

            if (!result.IsSuccess)
            {
                NotifyError($"File not found: {result.Error}");
                return RedirectToAction(nameof(Index));
            }

            return View(result.Value);
        }
        catch (Exception ex)
        {
            NotifyError($"Error loading file: {ex.Message}");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Update file metadata
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Edit(int id, FileUpdateDto updateDto)
    {
        try
        {
            // Check authorization before updating
            var fileEntity = await _fileRepository.GetByIdAsync(id);
            if (fileEntity == null)
            {
                NotifyError("File not found");
                return RedirectToAction(nameof(Index));
            }

            if (!await _authorizationService.CanEditFileAsync(fileEntity))
            {
                _logger.LogWarning("User {Username} attempted to update file {FileId} without permission", 
                    _userContext.Username ?? "Anonymous", id);
                NotifyError("You don't have permission to edit this file");
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                NotifyError("Invalid file data");
                return View(updateDto);
            }

            var username = _userContext.Username ?? "system";
            var result = await _fileService.UpdateFileAsync(id, updateDto, username);

            if (result.IsSuccess)
            {
                NotifySuccess("File updated successfully");
                return RedirectToAction(nameof(Details), new { id });
            }

            NotifyError($"Error updating file: {result.Error}");
            return View(updateDto);
        }
        catch (Exception ex)
        {
            NotifyError($"Error updating file: {ex.Message}");
            return View(updateDto);
        }
    }

    /// <summary>
    /// Move file to different folder
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Move(int id, int? targetFolderId)
    {
        try
        {
            var username = _userContext.Username ?? "system";
            var result = await _fileService.MoveFileAsync(id, targetFolderId, username);

            if (result.IsSuccess)
            {
                NotifySuccess("File moved successfully");
            }
            else
            {
                NotifyError($"Error moving file: {result.Error}");
            }

            return RedirectToAction(nameof(Index), new { folderId = targetFolderId });
        }
        catch (Exception ex)
        {
            NotifyError($"Error moving file: {ex.Message}");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Delete file (soft delete)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Delete(int id, int? folderId)
    {
        try
        {
            // Check authorization before deleting
            var fileEntity = await _fileRepository.GetByIdAsync(id);
            if (fileEntity == null)
            {
                NotifyError("File not found");
                return RedirectToAction(nameof(Index), new { folderId });
            }

            if (!await _authorizationService.CanDeleteFileAsync(fileEntity))
            {
                _logger.LogWarning("User {Username} attempted to delete file {FileId} without permission", 
                    _userContext.Username ?? "Anonymous", id);
                NotifyError("You don't have permission to delete this file");
                return RedirectToAction(nameof(Index), new { folderId });
            }

            var username = _userContext.Username ?? "system";
            var result = await _fileService.DeleteFileAsync(id, username);

            if (result.IsSuccess)
            {
                NotifySuccess("File deleted successfully");
            }
            else
            {
                NotifyError($"Error deleting file: {result.Error}");
            }

            return RedirectToAction(nameof(Index), new { folderId });
        }
        catch (Exception ex)
        {
            NotifyError($"Error deleting file: {ex.Message}");
            return RedirectToAction(nameof(Index), new { folderId });
        }
    }

    /// <summary>
    /// Search files
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Search(string? searchTerm, int? folderId, string? contentType)
    {
        try
        {
            var searchDto = new FileSearchDto
            {
                SearchTerm = searchTerm,
                FolderId = folderId,
                ContentType = contentType
            };

            var result = await _fileService.SearchFilesAsync(searchDto);

            ViewBag.SearchTerm = searchTerm;
            ViewBag.Files = result.IsSuccess ? result.Value : new List<FileDto>();

            return View();
        }
        catch (Exception ex)
        {
            NotifyError($"Error searching files: {ex.Message}");
            return View();
        }
    }

    /// <summary>
    /// Public-facing file browser (read-only)
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Browse(int? folderId = null, string? search = null)
    {
        try
        {
            // Get current folder if specified
            FolderDto? currentFolder = null;
            if (folderId.HasValue)
            {
                var folderResult = await _folderService.GetFolderByIdAsync(folderId.Value);
                if (folderResult.IsSuccess)
                {
                    currentFolder = folderResult.Value;
                }
            }

            // Get folders in current location
            var foldersResult = folderId.HasValue
                ? await _folderService.GetChildFoldersAsync(folderId.Value)
                : await _folderService.GetRootFoldersAsync();

            // Get files - with visibility filtering
            IEnumerable<FileDto> visibleFiles;
            
            if (!string.IsNullOrWhiteSpace(search))
            {
                // Search files
                var searchDto = new FileSearchDto
                {
                    SearchTerm = search,
                    FolderId = folderId
                };
                var searchResult = await _fileService.SearchFilesAsync(searchDto);
                visibleFiles = searchResult.IsSuccess ? searchResult.Value : new List<FileDto>();
            }
            else
            {
                // Browse by folder with visibility filtering
                var filesResult = await _fileService.GetFilesByFolderAsync(folderId);
                
                if (filesResult.IsSuccess && filesResult.Value.Any())
                {
                    // Get file entities for authorization check
                    var fileIds = filesResult.Value.Select(f => f.Id).ToList();
                    var fileEntities = await _fileRepository.GetByIdsAsync(fileIds);
                    
                    // Perform batch authorization check
                    var viewPermissions = await _authorizationService.CanViewFilesAsync(fileEntities);
                    
                    // Filter files based on permissions
                    visibleFiles = filesResult.Value
                        .Where(f => viewPermissions.GetValueOrDefault(f.Id, false))
                        .ToList();
                }
                else
                {
                    visibleFiles = new List<FileDto>();
                }
            }

            // Get breadcrumb if in a folder
            List<FolderDto> breadcrumb = new List<FolderDto>();
            if (folderId.HasValue)
            {
                var breadcrumbResult = await _folderService.GetFolderBreadcrumbAsync(folderId.Value);
                breadcrumb = breadcrumbResult.IsSuccess ? breadcrumbResult.Value : new List<FolderDto>();
            }

            ViewBag.CurrentFolder = currentFolder;
            ViewBag.Folders = foldersResult.IsSuccess ? foldersResult.Value : new List<FolderDto>();
            ViewBag.Files = visibleFiles;
            ViewBag.Breadcrumb = breadcrumb;
            ViewBag.SearchQuery = search;

            return View();
        }
        catch (Exception ex)
        {
            NotifyError($"Error loading file browser: {ex.Message}");
            return View();
        }
    }

    #region Folder Management

    /// <summary>
    /// Create new folder
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> CreateFolder(FolderCreateDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                NotifyError("Invalid folder data");
                return RedirectToAction(nameof(Index), new { folderId = createDto.ParentFolderId });
            }

            var username = _userContext.Username ?? "system";
            createDto.CreatedBy = username;
            var result = await _folderService.CreateFolderAsync(createDto);

            if (result.IsSuccess)
            {
                NotifySuccess("Folder created successfully");
            }
            else
            {
                NotifyError($"Error creating folder: {result.Error}");
            }

            return RedirectToAction(nameof(Index), new { folderId = createDto.ParentFolderId });
        }
        catch (Exception ex)
        {
            NotifyError($"Error creating folder: {ex.Message}");
            return RedirectToAction(nameof(Index), new { folderId = createDto.ParentFolderId });
        }
    }

    /// <summary>
    /// Edit folder
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> EditFolder(int id)
    {
        try
        {
            var result = await _folderService.GetFolderByIdAsync(id);

            if (!result.IsSuccess)
            {
                NotifyError($"Folder not found: {result.Error}");
                return RedirectToAction(nameof(Index));
            }

            return View(result.Value);
        }
        catch (Exception ex)
        {
            NotifyError($"Error loading folder: {ex.Message}");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Update folder
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> EditFolder(int id, FolderUpdateDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                NotifyError("Invalid folder data");
                return View(updateDto);
            }

            var username = _userContext.Username ?? "system";
            var result = await _folderService.UpdateFolderAsync(id, updateDto, username);

            if (result.IsSuccess)
            {
                NotifySuccess("Folder updated successfully");
                return RedirectToAction(nameof(Index), new { folderId = result.Value!.ParentFolderId });
            }

            NotifyError($"Error updating folder: {result.Error}");
            return View(updateDto);
        }
        catch (Exception ex)
        {
            NotifyError($"Error updating folder: {ex.Message}");
            return View(updateDto);
        }
    }

    /// <summary>
    /// Delete folder
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteFolder(int id, bool recursive = false)
    {
        try
        {
            // Get folder to know parent
            var folderResult = await _folderService.GetFolderByIdAsync(id);
            int? parentFolderId = folderResult.IsSuccess ? folderResult.Value!.ParentFolderId : null;

            var username = _userContext.Username ?? "system";
            var result = await _folderService.DeleteFolderAsync(
                folderId: id, 
                deletedBy: username, 
                recursive: recursive);

            if (result.IsSuccess)
            {
                NotifySuccess("Folder deleted successfully");
            }
            else
            {
                NotifyError($"Error deleting folder: {result.Error}");
            }

            return RedirectToAction(nameof(Index), new { folderId = parentFolderId });
        }
        catch (Exception ex)
        {
            NotifyError($"Error deleting folder: {ex.Message}");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Move folder to different parent
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> MoveFolder(int id, int? newParentFolderId)
    {
        try
        {
            var username = _userContext.Username ?? "system";
            var result = await _folderService.MoveFolderAsync(id, newParentFolderId, username);

            if (result.IsSuccess)
            {
                NotifySuccess("Folder moved successfully");
            }
            else
            {
                NotifyError($"Error moving folder: {result.Error}");
            }

            return RedirectToAction(nameof(Index), new { folderId = newParentFolderId });
        }
        catch (Exception ex)
        {
            NotifyError($"Error moving folder: {ex.Message}");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Get folder tree for navigation
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetFolderTree()
    {
        try
        {
            var result = await _folderService.GetFolderTreeAsync();

            if (result.IsSuccess)
            {
                return Json(result.Value);
            }

            return Json(new List<FolderTreeDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting folder tree");
            return Json(new List<FolderTreeDto>());
        }
    }

    #endregion
}
