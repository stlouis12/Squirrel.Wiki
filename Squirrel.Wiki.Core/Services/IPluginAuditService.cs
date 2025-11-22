using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Service for auditing plugin operations
/// </summary>
public interface IPluginAuditService
{
    /// <summary>
    /// Log a plugin operation
    /// </summary>
    /// <param name="pluginId">ID of the plugin</param>
    /// <param name="pluginIdentifier">Plugin identifier string</param>
    /// <param name="pluginName">Plugin name</param>
    /// <param name="operation">Type of operation</param>
    /// <param name="userId">ID of user performing operation</param>
    /// <param name="username">Username for display</param>
    /// <param name="success">Whether operation succeeded</param>
    /// <param name="changes">JSON of what changed</param>
    /// <param name="errorMessage">Error message if failed</param>
    /// <param name="ipAddress">IP address of request</param>
    /// <param name="userAgent">User agent string</param>
    /// <param name="notes">Additional notes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task LogOperationAsync(
        Guid pluginId,
        string pluginIdentifier,
        string pluginName,
        PluginOperation operation,
        Guid? userId,
        string username,
        bool success,
        string? changes = null,
        string? errorMessage = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? notes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all audit logs with optional filtering
    /// </summary>
    /// <param name="pluginId">Filter by plugin ID</param>
    /// <param name="userId">Filter by user ID</param>
    /// <param name="operation">Filter by operation type</param>
    /// <param name="startDate">Filter by start date</param>
    /// <param name="endDate">Filter by end date</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Page size</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paged list of audit logs</returns>
    Task<(IEnumerable<PluginAuditLog> Logs, int TotalCount)> GetAuditLogsAsync(
        Guid? pluginId = null,
        Guid? userId = null,
        PluginOperation? operation = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit logs for a specific plugin
    /// </summary>
    /// <param name="pluginId">Plugin ID</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Page size</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<(IEnumerable<PluginAuditLog> Logs, int TotalCount)> GetPluginAuditLogsAsync(
        Guid pluginId,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit logs for a specific user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Page size</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<(IEnumerable<PluginAuditLog> Logs, int TotalCount)> GetUserAuditLogsAsync(
        Guid userId,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a single audit log entry by ID
    /// </summary>
    /// <param name="id">Audit log ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<PluginAuditLog?> GetAuditLogAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete old audit logs (for cleanup/retention policy)
    /// </summary>
    /// <param name="olderThan">Delete logs older than this date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of logs deleted</returns>
    Task<int> DeleteOldLogsAsync(
        DateTime olderThan,
        CancellationToken cancellationToken = default);
}
