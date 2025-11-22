namespace Squirrel.Wiki.Core.Database.Entities;

/// <summary>
/// Audit log entry for plugin operations
/// </summary>
public class PluginAuditLog
{
    /// <summary>
    /// Unique identifier for the audit log entry
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the plugin this operation was performed on
    /// </summary>
    public Guid PluginId { get; set; }

    /// <summary>
    /// Plugin ID string (e.g., "oidc-auth")
    /// </summary>
    public required string PluginIdentifier { get; set; }

    /// <summary>
    /// Plugin name for display
    /// </summary>
    public required string PluginName { get; set; }

    /// <summary>
    /// Type of operation performed
    /// </summary>
    public required PluginOperation Operation { get; set; }

    /// <summary>
    /// ID of the user who performed the operation
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Username for display (stored for historical purposes)
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    /// JSON representation of what changed
    /// </summary>
    public string? Changes { get; set; }

    /// <summary>
    /// IP address of the request
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string from the request
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// When the operation occurred
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Whether the operation succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional context or notes
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Navigation property to the plugin
    /// </summary>
    public AuthenticationPlugin? Plugin { get; set; }

    /// <summary>
    /// Navigation property to the user
    /// </summary>
    public User? User { get; set; }
}

/// <summary>
/// Types of operations that can be performed on plugins
/// </summary>
public enum PluginOperation
{
    /// <summary>
    /// Plugin was registered/installed
    /// </summary>
    Register = 0,

    /// <summary>
    /// Plugin was enabled
    /// </summary>
    Enable = 1,

    /// <summary>
    /// Plugin was disabled
    /// </summary>
    Disable = 2,

    /// <summary>
    /// Plugin configuration was updated
    /// </summary>
    Configure = 3,

    /// <summary>
    /// Plugin configuration was viewed
    /// </summary>
    ViewConfiguration = 4,

    /// <summary>
    /// Plugin was deleted/uninstalled
    /// </summary>
    Delete = 5,

    /// <summary>
    /// Plugin was reloaded
    /// </summary>
    Reload = 6,

    /// <summary>
    /// Plugin details were viewed
    /// </summary>
    ViewDetails = 7,

    /// <summary>
    /// Plugin list was viewed
    /// </summary>
    ViewList = 8,

    /// <summary>
    /// Plugin validation was performed
    /// </summary>
    Validate = 9
}
