using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Services.Files;
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

    public FilesController(
        IFileService fileService,
        IFolderService folderService,
        IUserContext userContext,
        ILogger<FilesController> logger,
        INotificationService notificationService)
        : base(logger, notificationService)
    {
        _fileService = fileService;
        _folderService = folderService;
        _userContext = userContext;
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

            ViewBag.CurrentFolder = currentFolder;
            ViewBag.Folders = foldersResult.IsSuccess ? foldersResult.Value : new List<FolderDto>();
            ViewBag.Files = filesResult.IsSuccess ? filesResult.Value : new List<FileDto>();

            // Get breadcrumb if in a folder
            if (folderId.HasValue)
            {
                var breadcrumbResult = await _folderService.GetFolderBreadcrumbAsync(folderId.Value);
                ViewBag.Breadcrumb = breadcrumbResult.IsSuccess ? breadcrumbResult.Value : new List<FolderDto>();
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

    #endregion
}
