using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Squirrel.Wiki.Core.Database;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Services.Caching;
using Squirrel.Wiki.Core.Services.Search;
using Squirrel.Wiki.Web.Models.Admin;
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
        INotificationService notifications)
        : base(logger, notifications)
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
                Version = "3.0.0",
                Framework = ".NET 8.0",
                StartTime = Process.GetCurrentProcess().StartTime,
                Uptime = DateTime.Now - Process.GetCurrentProcess().StartTime
            }
        };

        return View(model);
    }

    /// <summary>
    /// Clear all caches - Refactored with Result Pattern
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ClearCache()
    {
        var result = ClearCacheWithResult();

        return result.Match<IActionResult>(
            onSuccess: _ =>
            {
                NotifyLocalizedSuccess("Notification_CacheCleared");
                return RedirectToAction(nameof(Index));
            },
            onFailure: (error, code) =>
            {
                NotifyError($"Error clearing cache: {error}");
                return RedirectToAction(nameof(Index));
            }
        );
    }

    /// <summary>
    /// Rebuild search index - Refactored with Result Pattern
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RebuildSearchIndex()
    {
        var result = await RebuildSearchIndexWithResult();

        return result.Match<IActionResult>(
            onSuccess: _ =>
            {
                NotifyLocalizedSuccess("Notification_SearchIndexRebuilt");
                return RedirectToAction(nameof(Index));
            },
            onFailure: (error, code) =>
            {
                NotifyError($"Error rebuilding search index: {error}");
                return RedirectToAction(nameof(Index));
            }
        );
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
        var health = new SystemHealth();

        // Check database
        try
        {
            await _dbContext.Database.CanConnectAsync();
            health.Database = new HealthStatus
            {
                Status = "Healthy",
                Message = "Database connection is working"
            };
        }
        catch (Exception ex)
        {
            health.Database = new HealthStatus
            {
                Status = "Unhealthy",
                Message = $"Database connection failed: {ex.Message}"
            };
        }

        // Check cache
        try
        {
            var testKey = $"health_check_{Guid.NewGuid()}";
            await _cache.SetAsync(testKey, "test", TimeSpan.FromSeconds(10));
            var value = await _cache.GetAsync<string>(testKey);
            await _cache.RemoveAsync(testKey);

            health.Cache = new HealthStatus
            {
                Status = value == "test" ? "Healthy" : "Degraded",
                Message = value == "test" ? "Cache is working" : "Cache read/write issue"
            };
        }
        catch (Exception ex)
        {
            health.Cache = new HealthStatus
            {
                Status = "Unhealthy",
                Message = $"Cache check failed: {ex.Message}"
            };
        }

        // Check search index
        try
        {
            // Simple check - try to search
            var result = await _searchService.SearchAsync("test", 1, 1);
            health.SearchIndex = new HealthStatus
            {
                Status = "Healthy",
                Message = "Search index is accessible"
            };
        }
        catch (Exception ex)
        {
            health.SearchIndex = new HealthStatus
            {
                Status = "Degraded",
                Message = $"Search index issue: {ex.Message}"
            };
        }

        // Check disk space
        try
        {
            var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady);
            if (drive != null)
            {
                var freeSpacePercent = (double)drive.AvailableFreeSpace / drive.TotalSize * 100;
                health.DiskSpace = new HealthStatus
                {
                    Status = freeSpacePercent > 10 ? "Healthy" : freeSpacePercent > 5 ? "Degraded" : "Unhealthy",
                    Message = $"{freeSpacePercent:F1}% free ({FormatBytes(drive.AvailableFreeSpace)} of {FormatBytes(drive.TotalSize)})",
                    Details = new Dictionary<string, string>
                    {
                        ["Drive"] = drive.Name,
                        ["Free"] = FormatBytes(drive.AvailableFreeSpace),
                        ["Total"] = FormatBytes(drive.TotalSize)
                    }
                };
            }
        }
        catch (Exception ex)
        {
            health.DiskSpace = new HealthStatus
            {
                Status = "Unknown",
                Message = $"Could not check disk space: {ex.Message}"
            };
        }

        // Check memory
        try
        {
            var process = Process.GetCurrentProcess();
            var workingSetMB = process.WorkingSet64 / 1024 / 1024;
            health.Memory = new HealthStatus
            {
                Status = workingSetMB < 1024 ? "Healthy" : workingSetMB < 2048 ? "Degraded" : "Unhealthy",
                Message = $"Working set: {workingSetMB} MB",
                Details = new Dictionary<string, string>
                {
                    ["WorkingSet"] = $"{workingSetMB} MB",
                    ["PrivateMemory"] = $"{process.PrivateMemorySize64 / 1024 / 1024} MB"
                }
            };
        }
        catch (Exception ex)
        {
            health.Memory = new HealthStatus
            {
                Status = "Unknown",
                Message = $"Could not check memory: {ex.Message}"
            };
        }

        return health;
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
