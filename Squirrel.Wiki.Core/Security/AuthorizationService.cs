using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Services;

namespace Squirrel.Wiki.Core.Security;

/// <summary>
/// Implementation of authorization service
/// </summary>
public class AuthorizationService : IAuthorizationService
{
    private readonly IUserContext _userContext;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<AuthorizationService> _logger;

    public AuthorizationService(
        IUserContext userContext,
        IConfigurationService configurationService,
        ILogger<AuthorizationService> logger)
    {
        _userContext = userContext;
        _configurationService = configurationService;
        _logger = logger;
    }

    public async Task<bool> CanViewPageAsync(Page page, CancellationToken cancellationToken = default)
    {
        // Deleted pages cannot be viewed
        if (page.IsDeleted)
        {
            _logger.LogDebug("Page {PageId} is deleted, denying access", page.Id);
            return false;
        }

        // Check page-specific visibility first
        switch (page.Visibility)
        {
            case PageVisibility.Public:
                // Public pages are always viewable
                _logger.LogDebug("Page {PageId} is public, allowing access", page.Id);
                return true;

            case PageVisibility.Private:
                // Private pages require authentication
                var isAuthenticatedForPrivate = IsAuthenticated();
                _logger.LogDebug("Page {PageId} is private, authenticated: {IsAuthenticated}", 
                    page.Id, isAuthenticatedForPrivate);
                return isAuthenticatedForPrivate;

            case PageVisibility.Inherit:
            default:
                // Inherit from global setting
                var allowAnonymousReading = await _configurationService.GetValueAsync<bool>(
                    "SQUIRREL_ALLOW_ANONYMOUS_READING", cancellationToken);
                
                if (allowAnonymousReading)
                {
                    // Global setting allows anonymous reading
                    _logger.LogDebug("Page {PageId} inherits global setting (anonymous allowed), allowing access", 
                        page.Id);
                    return true;
                }
                else
                {
                    // Global setting requires authentication
                    var isAuthenticatedForInherit = IsAuthenticated();
                    _logger.LogDebug("Page {PageId} inherits global setting (authentication required), authenticated: {IsAuthenticated}", 
                        page.Id, isAuthenticatedForInherit);
                    return isAuthenticatedForInherit;
                }
        }
    }

    public async Task<Dictionary<int, bool>> CanViewPagesAsync(IEnumerable<Page> pages, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<int, bool>();
        var pagesList = pages.ToList();
        
        if (!pagesList.Any())
        {
            return result;
        }

        _logger.LogDebug("Batch authorization check for {Count} pages", pagesList.Count);

        // Get the global setting once for all pages that inherit
        var allowAnonymousReading = await _configurationService.GetValueAsync<bool>(
            "SQUIRREL_ALLOW_ANONYMOUS_READING", cancellationToken);
        var isAuthenticated = IsAuthenticated();

        // Process each page using the same logic as CanViewPageAsync
        foreach (var page in pagesList)
        {
            // Deleted pages cannot be viewed
            if (page.IsDeleted)
            {
                result[page.Id] = false;
                continue;
            }

            // Check page-specific visibility
            switch (page.Visibility)
            {
                case PageVisibility.Public:
                    // Public pages are always viewable
                    result[page.Id] = true;
                    break;

                case PageVisibility.Private:
                    // Private pages require authentication
                    result[page.Id] = isAuthenticated;
                    break;

                case PageVisibility.Inherit:
                default:
                    // Inherit from global setting
                    if (allowAnonymousReading)
                    {
                        result[page.Id] = true;
                    }
                    else
                    {
                        result[page.Id] = isAuthenticated;
                    }
                    break;
            }
        }

        _logger.LogDebug("Batch authorization complete: {Authorized}/{Total} pages authorized", 
            result.Count(r => r.Value), result.Count);

        return result;
    }

    public bool IsAdmin()
    {
        return IsAuthenticated() && _userContext.IsAdmin;
    }

    public bool IsAuthenticated()
    {
        return _userContext.IsAuthenticated;
    }

    public Task<bool> CanEditPageAsync(Models.PageDto page, string? username, string? userRole)
    {
        // If page is locked, only admins can edit
        if (page.IsLocked)
        {
            return Task.FromResult(userRole == "Admin");
        }

        // Otherwise, admins and editors can edit
        return Task.FromResult(userRole == "Admin" || userRole == "Editor");
    }

    public Task<bool> CanDeletePageAsync(Models.PageDto page, string? userRole)
    {
        // If page is locked, only admins can delete
        if (page.IsLocked)
        {
            return Task.FromResult(userRole == "Admin");
        }

        // Otherwise, admins and editors can delete
        return Task.FromResult(userRole == "Admin" || userRole == "Editor");
    }

    public async Task<bool> CanViewFileAsync(Database.Entities.File file, CancellationToken cancellationToken = default)
    {
        // Deleted files cannot be viewed
        if (file.IsDeleted)
        {
            _logger.LogDebug("File {FileId} is deleted, denying access", file.Id);
            return false;
        }

        // Check file-specific visibility
        switch (file.Visibility)
        {
            case Database.Entities.FileVisibility.Public:
                // Public files are always viewable
                _logger.LogDebug("File {FileId} is public, allowing access", file.Id);
                return true;

            case Database.Entities.FileVisibility.Private:
                // Private files require authentication
                var isAuthenticatedForPrivate = IsAuthenticated();
                _logger.LogDebug("File {FileId} is private, authenticated: {IsAuthenticated}", 
                    file.Id, isAuthenticatedForPrivate);
                return isAuthenticatedForPrivate;

            case Database.Entities.FileVisibility.Inherit:
            default:
                // Inherit from global setting
                var allowAnonymousReading = await _configurationService.GetValueAsync<bool>(
                    "SQUIRREL_ALLOW_ANONYMOUS_READING", cancellationToken);
                
                if (allowAnonymousReading)
                {
                    _logger.LogDebug("File {FileId} inherits global setting (anonymous allowed), allowing access", 
                        file.Id);
                    return true;
                }
                else
                {
                    var isAuthenticatedForInherit = IsAuthenticated();
                    _logger.LogDebug("File {FileId} inherits global setting (authentication required), authenticated: {IsAuthenticated}", 
                        file.Id, isAuthenticatedForInherit);
                    return isAuthenticatedForInherit;
                }
        }
    }

    public Task<bool> CanUploadFileAsync(CancellationToken cancellationToken = default)
    {
        // Only authenticated users with Admin or Editor role can upload files
        var canUpload = IsAuthenticated() && (_userContext.IsAdmin || _userContext.IsEditor);
        _logger.LogDebug("Can upload file: {CanUpload} (IsAuthenticated: {IsAuthenticated}, IsAdmin: {IsAdmin}, IsEditor: {IsEditor})", 
            canUpload, IsAuthenticated(), _userContext.IsAdmin, _userContext.IsEditor);
        return Task.FromResult(canUpload);
    }

    public Task<bool> CanEditFileAsync(Database.Entities.File file, CancellationToken cancellationToken = default)
    {
        // Deleted files cannot be edited
        if (file.IsDeleted)
        {
            _logger.LogDebug("File {FileId} is deleted, denying edit access", file.Id);
            return Task.FromResult(false);
        }

        // Only authenticated users with Admin or Editor role can edit files
        var canEdit = IsAuthenticated() && (_userContext.IsAdmin || _userContext.IsEditor);
        _logger.LogDebug("Can edit file {FileId}: {CanEdit}", file.Id, canEdit);
        return Task.FromResult(canEdit);
    }

    public Task<bool> CanDeleteFileAsync(Database.Entities.File file, CancellationToken cancellationToken = default)
    {
        // Deleted files cannot be deleted again
        if (file.IsDeleted)
        {
            _logger.LogDebug("File {FileId} is already deleted, denying delete access", file.Id);
            return Task.FromResult(false);
        }

        // Only authenticated users with Admin or Editor role can delete files
        var canDelete = IsAuthenticated() && (_userContext.IsAdmin || _userContext.IsEditor);
        _logger.LogDebug("Can delete file {FileId}: {CanDelete}", file.Id, canDelete);
        return Task.FromResult(canDelete);
    }

    public Task<bool> CanManageFoldersAsync()
    {
        // Only authenticated users with Admin or Editor role can manage folders
        var canManage = IsAuthenticated() && (_userContext.IsAdmin || _userContext.IsEditor);
        _logger.LogDebug("Can manage folders: {CanManage}", canManage);
        return Task.FromResult(canManage);
    }

    public async Task<Dictionary<Guid, bool>> CanViewFilesAsync(IEnumerable<Database.Entities.File> files)
    {
        var result = new Dictionary<Guid, bool>();
        var filesList = files.ToList();
        
        if (!filesList.Any())
        {
            return result;
        }

        // Get allow anonymous reading setting once for all files
        var allowAnonymousReading = await _configurationService.GetValueAsync<bool>(
            "SQUIRREL_ALLOW_ANONYMOUS_READING");
        
        var isAuthenticated = IsAuthenticated();
        var username = _userContext.Username ?? "Anonymous";
        
        foreach (var file in filesList)
        {
            // Deleted files cannot be viewed
            if (file.IsDeleted)
            {
                result[file.Id] = false;
                continue;
            }
            
            bool canView;
            
            switch (file.Visibility)
            {
                case Database.Entities.FileVisibility.Public:
                    // Public files are always viewable
                    canView = true;
                    break;
                    
                case Database.Entities.FileVisibility.Private:
                    // Private files require authentication
                    canView = isAuthenticated;
                    break;
                    
                case Database.Entities.FileVisibility.Inherit:
                default:
                    // Inherit visibility follows global setting
                    canView = allowAnonymousReading || isAuthenticated;
                    break;
            }
            
            result[file.Id] = canView;
        }
        
        _logger.LogDebug(
            "Batch file view authorization for user {Username}: {AuthorizedCount}/{TotalCount} files authorized",
            username,
            result.Count(kvp => kvp.Value),
            result.Count);
        
        return result;
    }

    public Task<Dictionary<Guid, bool>> CanEditFilesAsync(IEnumerable<Database.Entities.File> files)
    {
        var result = new Dictionary<Guid, bool>();
        
        // Only Admin and Editor roles can edit files
        var canEdit = IsAuthenticated() && (_userContext.IsAdmin || _userContext.IsEditor);
        
        foreach (var file in files)
        {
            // Deleted files cannot be edited
            result[file.Id] = !file.IsDeleted && canEdit;
        }
        
        _logger.LogDebug(
            "Batch file edit authorization for user {Username}: {AuthorizedCount}/{TotalCount} files authorized",
            _userContext.Username ?? "Anonymous",
            result.Count(kvp => kvp.Value),
            result.Count);
        
        return Task.FromResult(result);
    }

    public Task<Dictionary<Guid, bool>> CanDeleteFilesAsync(IEnumerable<Database.Entities.File> files)
    {
        var result = new Dictionary<Guid, bool>();
        
        // Only Admin and Editor roles can delete files
        var canDelete = IsAuthenticated() && (_userContext.IsAdmin || _userContext.IsEditor);
        
        foreach (var file in files)
        {
            // Deleted files cannot be deleted again
            result[file.Id] = !file.IsDeleted && canDelete;
        }
        
        _logger.LogDebug(
            "Batch file delete authorization for user {Username}: {AuthorizedCount}/{TotalCount} files authorized",
            _userContext.Username ?? "Anonymous",
            result.Count(kvp => kvp.Value),
            result.Count);
        
        return Task.FromResult(result);
    }
}
