using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Services.Configuration;
using Squirrel.Wiki.Core.Services.Files;
using Squirrel.Wiki.Core.Services.Search;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Web.Resources;
using Squirrel.Wiki.Web.Services;
using Squirrel.Wiki.Web.Extensions;

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
    private readonly IConfigurationService _configurationService;
    private readonly ISearchService _searchService;

    public FilesController(
        IFileService fileService,
        IFolderService folderService,
        IUserContext userContext,
        Squirrel.Wiki.Core.Security.IAuthorizationService authorizationService,
        IFileRepository fileRepository,
        IConfigurationService configurationService,
        ISearchService searchService,
        ILogger<FilesController> logger,
        INotificationService notificationService,
        ITimezoneService timezoneService,
        IStringLocalizer<SharedResources> localizer)
        : base(logger, notificationService, timezoneService, localizer)
    {
        _fileService = fileService;
        _folderService = folderService;
        _userContext = userContext;
        _authorizationService = authorizationService;
        _fileRepository = fileRepository;
        _configurationService = configurationService;
        _searchService = searchService;
    }

    /// <summary>
    /// Display file manager interface
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Index(int? folderId)
    {
        try
        {
            // Check if anonymous reading is allowed for unauthenticated users
            if (!_authorizationService.IsAuthenticated())
            {
                var allowAnonymousReading = await _configurationService.GetValueAsync<bool>(
                    "SQUIRREL_ALLOW_ANONYMOUS_READING");
                
                if (!allowAnonymousReading)
                {
                    _logger.LogInformation("Unauthenticated user attempted to access Files page with anonymous reading disabled");
                    return RedirectToAction("Login", "Account", new { returnUrl = Request.Path + Request.QueryString });
                }
            }

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

            // Upload files individually to handle errors properly
            int successCount = 0;
            int duplicateCount = 0;
            int errorCount = 0;
            var errors = new List<string>();

            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    var uploadDto = new FileUploadDto
                    {
                        FileName = file.FileName,
                        FileStream = file.OpenReadStream(),
                        FileSize = file.Length,
                        ContentType = file.ContentType,
                        Description = description,
                        FolderId = folderId,
                        Visibility = Core.Database.Entities.FileVisibility.Inherit
                    };

                    var result = await _fileService.UploadFileAsync(uploadDto);

                    if (result.IsSuccess)
                    {
                        successCount++;
                    }
                    else if (result.ErrorCode == "DUPLICATE_FILE_IN_FOLDER")
                    {
                        duplicateCount++;
                        _logger.LogInformation("Duplicate file upload attempt: {FileName} in folder {FolderId}", 
                            file.FileName, folderId);
                    }
                    else
                    {
                        errorCount++;
                        errors.Add($"{file.FileName}: {result.Error}");
                        _logger.LogWarning("Failed to upload file {FileName}: {Error}", 
                            file.FileName, result.Error);
                    }
                }
            }

            // Provide appropriate feedback
            if (successCount > 0)
            {
                NotifySuccess($"Successfully uploaded {successCount} file(s)");
            }

            if (duplicateCount > 0)
            {
                NotifyWarning($"{duplicateCount} file(s) already exist in this folder and were skipped");
            }

            if (errorCount > 0)
            {
                foreach (var error in errors.Take(3)) // Show first 3 errors
                {
                    NotifyError(error);
                }
                if (errors.Count > 3)
                {
                    NotifyError($"...and {errors.Count - 3} more error(s)");
                }
            }

            return RedirectToAction(nameof(Index), new { folderId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading files");
            NotifyError($"Error uploading files: {ex.Message}");
            return RedirectToAction(nameof(Index), new { folderId });
        }
    }


    /// <summary>
    /// Download a file
    /// </summary>
    [HttpGet("Files/Download/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> Download(Guid id)
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
    /// Get file details for modal display
    /// </summary>
    [HttpGet("Files/GetFileDetails/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFileDetails(Guid id)
    {
        try
        {
            // Check authorization
            var fileEntity = await _fileRepository.GetByIdAsync(id);
            if (fileEntity == null)
            {
                return Content("<div class='alert alert-danger'>File not found</div>");
            }

            if (!await _authorizationService.CanViewFileAsync(fileEntity))
            {
                return Content("<div class='alert alert-danger'>You don't have permission to view this file</div>");
            }

            var result = await _fileService.GetFileDetailsAsync(id);
            if (!result.IsSuccess)
            {
                return Content($"<div class='alert alert-danger'>Error: {result.Error}</div>");
            }

            var file = result.Value;
            var uploadedOnStr = await file.UploadedOn.ToLocalTimeStringAsync(_timezoneService, "F");
            
            // Generate markdown embed code based on file type
            var isImage = file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
            var markdownEmbed = isImage 
                ? $"![{file.FileName}]({file.Id})" 
                : $"[{file.FileName}]({file.Id})";
            
            var html = $@"
                <dl class='row'>
                    <dt class='col-sm-3'>File Name</dt>
                    <dd class='col-sm-9'>{file.FileName}</dd>
                    
                    <dt class='col-sm-3'>Size</dt>
                    <dd class='col-sm-9'>{FormatFileSize(file.FileSize)}</dd>
                    
                    <dt class='col-sm-3'>Type</dt>
                    <dd class='col-sm-9'><span class='badge bg-secondary'>{file.ContentType}</span></dd>
                    
                    <dt class='col-sm-3'>Visibility</dt>
                    <dd class='col-sm-9'><span class='badge bg-{(file.Visibility == Core.Database.Entities.FileVisibility.Public ? "success" : "warning")}'>{file.Visibility}</span></dd>
                    
                    {(!string.IsNullOrEmpty(file.Description) ? $@"
                    <dt class='col-sm-3'>Description</dt>
                    <dd class='col-sm-9'>{file.Description}</dd>" : "")}
                    
                    <dt class='col-sm-3'>Uploaded By</dt>
                    <dd class='col-sm-9'>{file.UploadedBy}</dd>
                    
                    <dt class='col-sm-3'>Uploaded On</dt>
                    <dd class='col-sm-9'>{uploadedOnStr}</dd>
                    
                    <dt class='col-sm-3'>Version</dt>
                    <dd class='col-sm-9'>{file.CurrentVersion}</dd>
                    
                    <dt class='col-sm-3'>Markdown Embed</dt>
                    <dd class='col-sm-9'>
                        <div class='input-group'>
                            <input type='text' class='form-control' id='markdownEmbed' value='{markdownEmbed}' readonly />
                            <button class='btn btn-outline-secondary copy-markdown-btn' type='button' title='Copy to clipboard'>
                                <i class='bi bi-clipboard'></i>
                            </button>
                        </div>
                        <small class='text-muted'>
                            {(isImage ? "Use this markdown to embed the image in a page" : "Use this markdown to link to the file in a page")}
                        </small>
                    </dd>
                </dl>
                <div class='mt-3'>
                    <a href='/Files/Download/{file.Id}' class='btn btn-primary'>
                        <i class='bi bi-download'></i> Download
                    </a>
                </div>";

            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading file details for modal");
            return Content($"<div class='alert alert-danger'>Error: {ex.Message}</div>");
        }
    }

    /// <summary>
    /// Get edit form for modal display
    /// </summary>
    [HttpGet("Files/GetEditForm/{id}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> GetEditForm(Guid id)
    {
        try
        {
            // Check authorization
            var fileEntity = await _fileRepository.GetByIdAsync(id);
            if (fileEntity == null)
            {
                return Content("<div class='alert alert-danger'>File not found</div>");
            }

            if (!await _authorizationService.CanEditFileAsync(fileEntity))
            {
                return Content("<div class='alert alert-danger'>You don't have permission to edit this file</div>");
            }

            var result = await _fileService.GetFileByIdAsync(id);
            if (!result.IsSuccess)
            {
                return Content($"<div class='alert alert-danger'>Error: {result.Error}</div>");
            }

            var file = result.Value;
            var html = $@"
                <input type='hidden' name='id' value='{file.Id}' />
                
                <div class='mb-3'>
                    <label for='editFileName' class='form-label'>File Name</label>
                    <input type='text' class='form-control' id='editFileName' name='FileName' value='{file.FileName}' required />
                </div>
                
                <div class='mb-3'>
                    <label for='editDescription' class='form-label'>Description</label>
                    <textarea class='form-control' id='editDescription' name='Description' rows='3'>{file.Description}</textarea>
                </div>
                
                <div class='mb-3'>
                    <label for='editVisibility' class='form-label'>Visibility</label>
                    <select class='form-select' id='editVisibility' name='Visibility'>
                        <option value='Inherit' {(file.Visibility == Core.Database.Entities.FileVisibility.Inherit ? "selected" : "")}>Inherit from Folder</option>
                        <option value='Public' {(file.Visibility == Core.Database.Entities.FileVisibility.Public ? "selected" : "")}>Public</option>
                        <option value='Private' {(file.Visibility == Core.Database.Entities.FileVisibility.Private ? "selected" : "")}>Private</option>
                    </select>
                </div>";

            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading edit form for modal");
            return Content($"<div class='alert alert-danger'>Error: {ex.Message}</div>");
        }
    }

    /// <summary>
    /// Update file metadata (from modal)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> UpdateMetadata(Guid id, FileUpdateDto updateDto)
    {
        try
        {
            // Check authorization
            var fileEntity = await _fileRepository.GetByIdAsync(id);
            if (fileEntity == null)
            {
                NotifyError("File not found");
                return RedirectToAction(nameof(Index));
            }

            if (!await _authorizationService.CanEditFileAsync(fileEntity))
            {
                NotifyError("You don't have permission to edit this file");
                return RedirectToAction(nameof(Index));
            }

            var username = _userContext.Username ?? "system";
            var result = await _fileService.UpdateFileAsync(id, updateDto, username);

            if (result.IsSuccess)
            {
                NotifySuccess("File updated successfully");
            }
            else
            {
                NotifyError($"Error updating file: {result.Error}");
            }

            return RedirectToAction(nameof(Index), new { folderId = fileEntity.FolderId });
        }
        catch (Exception ex)
        {
            NotifyError($"Error updating file: {ex.Message}");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Move file to different folder
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Move(Guid id, int? targetFolderId)
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
    public async Task<IActionResult> Delete(Guid id, int? folderId)
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
    /// Search files using the configured search strategy (Lucene or database)
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Search(string? searchTerm)
    {
        try
        {
            // Check if anonymous reading is allowed for unauthenticated users
            if (!_authorizationService.IsAuthenticated())
            {
                var allowAnonymousReading = await _configurationService.GetValueAsync<bool>(
                    "SQUIRREL_ALLOW_ANONYMOUS_READING");
                
                if (!allowAnonymousReading)
                {
                    _logger.LogInformation("Unauthenticated user attempted to access Files Search with anonymous reading disabled");
                    return RedirectToAction("Login", "Account", new { returnUrl = Request.Path + Request.QueryString });
                }
            }

            // Use SearchService which will delegate to the appropriate strategy (Lucene or database)
            var searchResults = string.IsNullOrWhiteSpace(searchTerm)
                ? new SearchResultsDto { Results = new List<SearchResultItemDto>() }
                : await _searchService.SearchFilesAsync(searchTerm);

            // Convert search results to FileDto list
            var visibleFiles = new List<FileDto>();
            if (searchResults.Results.Any())
            {
                // Get file entities from search results - use FileId for file results
                var fileIds = searchResults.Results
                    .Where(r => r.FileId.HasValue)
                    .Select(r => r.FileId!.Value)
                    .ToList();
                
                var fileEntities = await _fileRepository.GetByIdsAsync(fileIds);
                
                // Perform batch authorization check
                var viewPermissions = await _authorizationService.CanViewFilesAsync(fileEntities);
                
                // Get full file details for authorized files
                foreach (var fileEntity in fileEntities.Where(f => viewPermissions.GetValueOrDefault(f.Id, false)))
                {
                    var fileResult = await _fileService.GetFileByIdAsync(fileEntity.Id);
                    if (fileResult.IsSuccess)
                    {
                        visibleFiles.Add(fileResult.Value);
                    }
                }
                
                _logger.LogDebug(
                    "File search filtered by permissions for user {Username}: {VisibleCount}/{TotalCount} files visible",
                    _userContext.Username ?? "Anonymous",
                    visibleFiles.Count,
                    searchResults.Results.Count);
            }

            ViewBag.SearchTerm = searchTerm;
            return View(visibleFiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching files");
            NotifyError($"Error searching files: {ex.Message}");
            return View(new List<FileDto>());
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

    #region Helper Methods

    /// <summary>
    /// Format file size for display
    /// </summary>
    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    #endregion
}
