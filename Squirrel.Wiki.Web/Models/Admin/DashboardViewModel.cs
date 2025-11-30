namespace Squirrel.Wiki.Web.Models.Admin;

/// <summary>
/// View model for the admin dashboard
/// </summary>
public class DashboardViewModel : BaseViewModel
{
    public SystemStatistics Statistics { get; set; } = new();
    public List<RecentActivity> RecentActivities { get; set; } = new();
    public SystemHealth Health { get; set; } = new();
}

/// <summary>
/// System statistics for the dashboard
/// </summary>
public class SystemStatistics
{
    public int TotalPages { get; set; }
    public int TotalUsers { get; set; }
    public int TotalCategories { get; set; }
    public int TotalTags { get; set; }
    public int RecentEdits24Hours { get; set; }
    public int RecentEdits7Days { get; set; }
    public long DatabaseSizeBytes { get; set; }
    public long SearchIndexSizeBytes { get; set; }
}

/// <summary>
/// Recent activity item
/// </summary>
public class RecentActivity
{
    public string Type { get; set; } = string.Empty; // "PageCreated", "PageEdited", "UserLogin", etc.
    public string Description { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Url { get; set; }
}

/// <summary>
/// System health status
/// </summary>
public class SystemHealth
{
    public HealthStatus Database { get; set; } = new();
    public HealthStatus Cache { get; set; } = new();
    public HealthStatus SearchIndex { get; set; } = new();
    public HealthStatus DiskSpace { get; set; } = new();
    public HealthStatus Memory { get; set; } = new();
}

/// <summary>
/// Individual health check status
/// </summary>
public class HealthStatus
{
    public string Status { get; set; } = "Unknown"; // "Healthy", "Degraded", "Unhealthy", "Unknown"
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string> Details { get; set; } = new();
    
    public string StatusClass => Status switch
    {
        "Healthy" => "success",
        "Degraded" => "warning",
        "Unhealthy" => "danger",
        _ => "secondary"
    };
    
    public string StatusIcon => Status switch
    {
        "Healthy" => "bi-check-circle-fill",
        "Degraded" => "bi-exclamation-triangle-fill",
        "Unhealthy" => "bi-x-circle-fill",
        _ => "bi-question-circle-fill"
    };
}
