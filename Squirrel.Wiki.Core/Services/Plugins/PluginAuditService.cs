using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Database;
using Squirrel.Wiki.Core.Database.Entities;
using System.Text.Json;

namespace Squirrel.Wiki.Core.Services.Plugins;

/// <summary>
/// Service for auditing plugin operations
/// </summary>
public class PluginAuditService : IPluginAuditService
{
    private readonly SquirrelDbContext _context;
    private readonly ILogger<PluginAuditService> _logger;

    public PluginAuditService(
        SquirrelDbContext context,
        ILogger<PluginAuditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task LogOperationAsync(
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
        CancellationToken cancellationToken = default)
    {
        try
        {
            var auditLog = new PluginAuditLog
            {
                Id = Guid.NewGuid(),
                PluginId = pluginId,
                PluginIdentifier = pluginIdentifier,
                PluginName = pluginName,
                Operation = operation,
                UserId = userId,
                Username = username,
                Changes = changes,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Timestamp = DateTime.UtcNow,
                Success = success,
                ErrorMessage = errorMessage,
                Notes = notes
            };

            _context.PluginAuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Audit log created: Plugin={PluginId}, Operation={Operation}, User={Username}, Success={Success}",
                pluginIdentifier,
                operation,
                username,
                success);
        }
        catch (Exception ex)
        {
            // Don't let audit logging failures break the application
            _logger.LogError(ex, "Failed to create audit log for plugin {PluginId}", pluginIdentifier);
        }
    }

    /// <inheritdoc/>
    public async Task<(IEnumerable<PluginAuditLog> Logs, int TotalCount)> GetAuditLogsAsync(
        Guid? pluginId = null,
        Guid? userId = null,
        PluginOperation? operation = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PluginAuditLogs
            .Include(a => a.Plugin)
            .Include(a => a.User)
            .AsQueryable();

        // Apply filters
        if (pluginId.HasValue)
        {
            query = query.Where(a => a.PluginId == pluginId.Value);
        }

        if (userId.HasValue)
        {
            query = query.Where(a => a.UserId == userId.Value);
        }

        if (operation.HasValue)
        {
            query = query.Where(a => a.Operation == operation.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= endDate.Value);
        }

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination and ordering
        var logs = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (logs, totalCount);
    }

    /// <inheritdoc/>
    public async Task<(IEnumerable<PluginAuditLog> Logs, int TotalCount)> GetPluginAuditLogsAsync(
        Guid pluginId,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return await GetAuditLogsAsync(
            pluginId: pluginId,
            pageNumber: pageNumber,
            pageSize: pageSize,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<(IEnumerable<PluginAuditLog> Logs, int TotalCount)> GetUserAuditLogsAsync(
        Guid userId,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return await GetAuditLogsAsync(
            userId: userId,
            pageNumber: pageNumber,
            pageSize: pageSize,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PluginAuditLog?> GetAuditLogAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.PluginAuditLogs
            .Include(a => a.Plugin)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> DeleteOldLogsAsync(
        DateTime olderThan,
        CancellationToken cancellationToken = default)
    {
        var logsToDelete = await _context.PluginAuditLogs
            .Where(a => a.Timestamp < olderThan)
            .ToListAsync(cancellationToken);

        var count = logsToDelete.Count;

        if (count > 0)
        {
            _context.PluginAuditLogs.RemoveRange(logsToDelete);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted {Count} old audit logs older than {Date}", count, olderThan);
        }

        return count;
    }

    /// <summary>
    /// Helper method to serialize configuration changes to JSON
    /// </summary>
    public static string SerializeChanges(object changes)
    {
        try
        {
            return JsonSerializer.Serialize(changes, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch
        {
            return changes.ToString() ?? string.Empty;
        }
    }
}
