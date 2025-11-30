namespace Squirrel.Wiki.Core.Services.Configuration;

/// <summary>
/// Service for handling timezone conversions and configuration
/// </summary>
public interface ITimezoneService
{
    /// <summary>
    /// Gets the configured timezone for the application
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The configured TimeZoneInfo</returns>
    Task<TimeZoneInfo> GetConfiguredTimezoneAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Converts a UTC DateTime to the configured timezone
    /// </summary>
    /// <param name="utcDateTime">The UTC DateTime to convert</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>DateTime in the configured timezone</returns>
    Task<DateTime> ConvertFromUtcAsync(DateTime utcDateTime, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Converts a DateTime in the configured timezone to UTC
    /// </summary>
    /// <param name="localDateTime">The local DateTime to convert</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>DateTime in UTC</returns>
    Task<DateTime> ConvertToUtcAsync(DateTime localDateTime, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all available timezone IDs from the system
    /// </summary>
    /// <returns>Collection of timezone IDs</returns>
    IEnumerable<string> GetAvailableTimezoneIds();
    
    /// <summary>
    /// Gets the display name for a given timezone ID
    /// </summary>
    /// <param name="timezoneId">The timezone ID</param>
    /// <returns>Display name of the timezone</returns>
    string GetTimezoneDisplayName(string timezoneId);
    
    /// <summary>
    /// Validates if a timezone ID is valid
    /// </summary>
    /// <param name="timezoneId">The timezone ID to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    bool IsValidTimezoneId(string timezoneId);
}
