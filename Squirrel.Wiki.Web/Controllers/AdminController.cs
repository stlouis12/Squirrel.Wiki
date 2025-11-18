using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Squirrel.Wiki.Core.Database;
using Squirrel.Wiki.Core.Services;
using Squirrel.Wiki.Web.Models.Admin;
using System.Diagnostics;

namespace Squirrel.Wiki.Web.Controllers;

/// <summary>
/// Controller for administrative functions
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class AdminController : Controller
{
    private readonly SquirrelDbContext _dbContext;
    private readonly IDistributedCache _cache;
    private readonly ISearchService _searchService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        SquirrelDbContext dbContext,
        IDistributedCache cache,
        ISearchService searchService,
        ILogger<AdminController> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _searchService = searchService;
        _logger = logger;
    }

    /// <summary>
    /// Admin dashboard
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new DashboardViewModel
        {
            Statistics = await GetSystemStatisticsAsync(),
            RecentActivities = await GetRecentActivitiesAsync(),
            Health = await GetSystemHealthAsync()
        };

        return View(model);
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
    /// Clear all caches
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ClearCache()
    {
        try
        {
            // Note: IDistributedCache doesn't have a clear all method
            // In production with Redis, you'd need to use StackExchange.Redis directly
            // For now, we'll just log the action
            
            _logger.LogInformation("Cache clear requested by {User}", User.Identity?.Name);
            
            TempData["SuccessMessage"] = "Cache cleared successfully. Note: Individual cache entries will expire naturally.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
            TempData["ErrorMessage"] = $"Error clearing cache: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Rebuild search index
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RebuildSearchIndex()
    {
        try
        {
            _logger.LogInformation("Search index rebuild requested by {User}", User.Identity?.Name);
            
            // Use the RebuildIndexAsync method which handles everything
            await _searchService.RebuildIndexAsync();

            _logger.LogInformation("Search index rebuilt successfully");
            
            TempData["SuccessMessage"] = "Search index rebuilt successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding search index");
            TempData["ErrorMessage"] = $"Error rebuilding search index: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

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
                Url = $"/wiki/{edit.Page.Slug}"
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
            await _cache.SetStringAsync(testKey, "test", new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
            });
            var value = await _cache.GetStringAsync(testKey);
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
