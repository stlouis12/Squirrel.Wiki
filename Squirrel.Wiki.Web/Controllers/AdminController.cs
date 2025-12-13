using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Squirrel.Wiki.Core.Database;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Services.Caching;
using Squirrel.Wiki.Core.Services.Search;
using Squirrel.Wiki.Web.Models.Admin;
using Squirrel.Wiki.Web.Resources;
using Squirrel.Wiki.Web.Services;

namespace Squirrel.Wiki.Web.Controllers;

/// <summary>
/// Controller for administrative functions
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class AdminController : BaseController
{
    private readonly SquirrelDbContext _dbContext;
    private readonly ICacheService _cache;
    private readonly ISearchService _searchService;

    public AdminController(
        SquirrelDbContext dbContext,
        ICacheService cache,
        ISearchService searchService,
        ILogger<AdminController> logger,
        INotificationService notifications,
        IStringLocalizer<SharedResources> localizer)
        : base(logger, notifications, null, localizer)
    {
        _dbContext = dbContext;
        _cache = cache;
        _searchService = searchService;
    }

    /// <summary>
    /// Admin dashboard - Refactored with Result Pattern
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await LoadDashboardWithResult();

        return result.Match<IActionResult>(
            onSuccess: model => View(model),
            onFailure: (error, code) =>
            {
                _logger.LogError("Failed to load admin dashboard: {Error} (Code: {Code})", error, code);
                NotifyError("An error occurred while loading the dashboard.");
                return View(new DashboardViewModel());
            }
        );
    }

    /// <summary>
    /// System information page
    /// </summary>
    [HttpGet]
    public IActionResult SystemInfo()
    {
        var model = new
        {
            Environment = new
            {
                MachineName = Environment.MachineName,
                OSVersion = Environment.OSVersion.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                Is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
                Is64BitProcess = Environment.Is64BitProcess,
                RuntimeVersion = Environment.Version.ToString(),
                WorkingSet = Environment.WorkingSet,
                SystemPageSize = Environment.SystemPageSize
            },
            Application = new
            {
                Version = "1.0.0",
                Framework = ".NET 8.0",
                StartTime = Process.GetCurrentProcess().StartTime,
                Uptime = DateTime.Now - Process.GetCurrentProcess().StartTime
            }
        };

        return View(model);
    }

    #region Helper Methods - Result Pattern

    /// <summary>
    /// Loads admin dashboard with Result Pattern
    /// Encapsulates all dashboard data loading
    /// </summary>
    private async Task<Result<DashboardViewModel>> LoadDashboardWithResult()
    {
        try
        {
            var model = new DashboardViewModel
            {
                Statistics = await GetSystemStatisticsAsync(),
                RecentActivities = await GetRecentActivitiesAsync(),
                Health = await GetSystemHealthAsync()
            };

            return Result<DashboardViewModel>.Success(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading admin dashboard");
            return Result<DashboardViewModel>.Failure(
                "An error occurred while loading the dashboard",
                "DASHBOARD_LOAD_ERROR"
            );
        }
    }

    /// <summary>
    /// Clears cache with Result Pattern
    /// </summary>
    private Result<bool> ClearCacheWithResult()
    {
        try
        {
            // Note: IDistributedCache doesn't have a clear all method
            // In production with Redis, you'd need to use StackExchange.Redis directly
            // For now, we'll just log the action
            
            _logger.LogInformation("Cache clear requested by {User}", User.Identity?.Name);
            
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
            return Result<bool>.Failure(
                ex.Message,
                "CACHE_CLEAR_ERROR"
            );
        }
    }

    /// <summary>
    /// Rebuilds search index with Result Pattern
    /// </summary>
    private async Task<Result<bool>> RebuildSearchIndexWithResult()
    {
        try
        {
            _logger.LogInformation("Search index rebuild requested by {User}", User.Identity?.Name);
            
            // Use the RebuildIndexAsync method which handles everything
            await _searchService.RebuildIndexAsync();

            _logger.LogInformation("Search index rebuilt successfully");
            
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding search index");
            return Result<bool>.Failure(
                ex.Message,
                "SEARCH_INDEX_REBUILD_ERROR"
            );
        }
    }

    #endregion

    #region Private Helper Methods

    private async Task<SystemStatistics> GetSystemStatisticsAsync()
    {
        var now = DateTime.UtcNow;
        var yesterday = now.AddDays(-1);
        var weekAgo = now.AddDays(-7);

        var stats = new SystemStatistics
        {
            TotalPages = await _dbContext.Pages.CountAsync(p => !p.IsDeleted),
            TotalUsers = await _dbContext.Users.CountAsync(),
            TotalCategories = await _dbContext.Categories.CountAsync(),
            TotalTags = await _dbContext.Tags.CountAsync(),
            RecentEdits24Hours = await _dbContext.PageContents
                .CountAsync(pc => pc.EditedOn >= yesterday),
            RecentEdits7Days = await _dbContext.PageContents
                .CountAsync(pc => pc.EditedOn >= weekAgo)
        };

        // Get database size (this is database-specific, simplified here)
        try
        {
            // This would need to be implemented per database provider
            stats.DatabaseSizeBytes = 0; // Placeholder
        }
        catch
        {
            stats.DatabaseSizeBytes = 0;
        }

        // Get search index size
        try
        {
            // This would need to check the actual index directory
            stats.SearchIndexSizeBytes = 0; // Placeholder
        }
        catch
        {
            stats.SearchIndexSizeBytes = 0;
        }

        return stats;
    }

    private async Task<List<RecentActivity>> GetRecentActivitiesAsync()
    {
        var activities = new List<RecentActivity>();

        // Get recent page edits
        var recentEdits = await _dbContext.PageContents
            .Include(pc => pc.Page)
            .OrderByDescending(pc => pc.EditedOn)
            .Take(10)
            .ToListAsync();

        foreach (var edit in recentEdits)
        {
            activities.Add(new RecentActivity
            {
                Type = edit.VersionNumber == 1 ? "PageCreated" : "PageEdited",
                Description = $"{(edit.VersionNumber == 1 ? "Created" : "Edited")} page: {edit.Page.Title}",
                User = edit.EditedBy,
                Timestamp = edit.EditedOn,
                Url = Url.Action("Index", "Wiki", new { id = edit.Page.Id, slug = edit.Page.Slug })
            });
        }

        return activities.OrderByDescending(a => a.Timestamp).ToList();
    }

    private async Task<SystemHealth> GetSystemHealthAsync()
    {
        return new SystemHealth
        {
            Database = await CheckDatabaseHealthAsync(),
            Cache = await CheckCacheHealthAsync(),
            SearchIndex = await CheckSearchIndexHealthAsync(),
            DiskSpace = CheckDiskSpaceHealth(),
            Memory = CheckMemoryHealth()
        };
    }

    private async Task<HealthStatus> CheckDatabaseHealthAsync()
    {
        try
        {
            await _dbContext.Database.CanConnectAsync();
            return new HealthStatus
            {
                Status = _localizer?["HealthStatus_Healthy"] ?? "Healthy",
                Message = _localizer?["HealthStatus_DatabaseConnectionWorking"] ?? "Database connection is working"
            };
        }
        catch (Exception ex)
        {
            return new HealthStatus
            {
                Status = _localizer?["HealthStatus_Unhealthy"] ?? "Unhealthy",
                Message = _localizer?["HealthStatus_DatabaseConnectionFailed", ex.Message] ?? $"Database connection failed: {ex.Message}"
            };
        }
    }

    private async Task<HealthStatus> CheckCacheHealthAsync()
    {
        try
        {
            var testKey = $"health_check_{Guid.NewGuid()}";
            await _cache.SetAsync(testKey, "test", TimeSpan.FromSeconds(10));
            var value = await _cache.GetAsync<string>(testKey);
            await _cache.RemoveAsync(testKey);

            return new HealthStatus
            {
                Status = value == "test" ? (_localizer?["HealthStatus_Healthy"] ?? "Healthy") : (_localizer?["HealthStatus_Degraded"] ?? "Degraded"),
                Message = value == "test" ? (_localizer?["HealthStatus_CacheWorking"] ?? "Cache is working") : (_localizer?["HealthStatus_CacheReadWriteIssue"] ?? "Cache read/write issue")
            };
        }
        catch (Exception ex)
        {
            return new HealthStatus
            {
                Status = _localizer?["HealthStatus_Unhealthy"] ?? "Unhealthy",
                Message = _localizer?["HealthStatus_CacheCheckFailed", ex.Message] ?? $"Cache check failed: {ex.Message}"
            };
        }
    }

    private async Task<HealthStatus> CheckSearchIndexHealthAsync()
    {
        try
        {
            // Simple check - try to search
            await _searchService.SearchAsync("test", 1, 1);
            return new HealthStatus
            {
                Status = _localizer?["HealthStatus_Healthy"] ?? "Healthy",
                Message = _localizer?["HealthStatus_SearchIndexAccessible"] ?? "Search index is accessible"
            };
        }
        catch (Exception ex)
        {
            return new HealthStatus
            {
                Status = _localizer?["HealthStatus_Degraded"] ?? "Degraded",
                Message = _localizer?["HealthStatus_SearchIndexIssue", ex.Message] ?? $"Search index issue: {ex.Message}"
            };
        }
    }

    private HealthStatus CheckDiskSpaceHealth()
    {
        try
        {
            var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady);
            if (drive == null)
            {
                return new HealthStatus
                {
                    Status = _localizer?["HealthStatus_Unknown"] ?? "Unknown",
                    Message = _localizer?["HealthStatus_NoReadyDrivesFound"] ?? "No ready drives found"
                };
            }

            var freeSpacePercent = (double)drive.AvailableFreeSpace / drive.TotalSize * 100;
            var status = DetermineDiskSpaceStatus(freeSpacePercent);

            return new HealthStatus
            {
                Status = status,
                Message = _localizer?["HealthStatus_DiskSpaceInfo", 
                    freeSpacePercent.ToString("F1"), 
                    FormatBytes(drive.AvailableFreeSpace), 
                    FormatBytes(drive.TotalSize)] ?? $"{freeSpacePercent:F1}% free ({FormatBytes(drive.AvailableFreeSpace)} of {FormatBytes(drive.TotalSize)})",
                Details = new Dictionary<string, string>
                {
                    ["Drive"] = drive.Name,
                    ["Free"] = FormatBytes(drive.AvailableFreeSpace),
                    ["Total"] = FormatBytes(drive.TotalSize)
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthStatus
            {
                Status = _localizer?["HealthStatus_Unknown"] ?? "Unknown",
                Message = _localizer?["HealthStatus_CouldNotCheckDiskSpace", ex.Message] ?? $"Could not check disk space: {ex.Message}"
            };
        }
    }

    private HealthStatus CheckMemoryHealth()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var workingSetMB = process.WorkingSet64 / 1024 / 1024;
            var status = DetermineMemoryStatus(workingSetMB);

            return new HealthStatus
            {
                Status = status,
                Message = _localizer?["HealthStatus_WorkingSetMemory", workingSetMB] ?? $"Working set: {workingSetMB} MB",
                Details = new Dictionary<string, string>
                {
                    ["WorkingSet"] = $"{workingSetMB} MB",
                    ["PrivateMemory"] = $"{process.PrivateMemorySize64 / 1024 / 1024} MB"
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthStatus
            {
                Status = _localizer?["HealthStatus_Unknown"] ?? "Unknown",
                Message = _localizer?["HealthStatus_CouldNotCheckMemory", ex.Message] ?? $"Could not check memory: {ex.Message}"
            };
        }
    }

    private string DetermineDiskSpaceStatus(double freeSpacePercent)
    {
        if (freeSpacePercent > 10)
            return _localizer?["HealthStatus_Healthy"] ?? "Healthy";
        if (freeSpacePercent > 5)
            return _localizer?["HealthStatus_Degraded"] ?? "Degraded";
        return _localizer?["HealthStatus_Unhealthy"] ?? "Unhealthy";
    }

    private string DetermineMemoryStatus(long workingSetMB)
    {
        if (workingSetMB < 1024)
            return _localizer?["HealthStatus_Healthy"] ?? "Healthy";
        if (workingSetMB < 2048)
            return _localizer?["HealthStatus_Degraded"] ?? "Degraded";
        return _localizer?["HealthStatus_Unhealthy"] ?? "Unhealthy";
    }

    private static string FormatBytes(long bytes)
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
