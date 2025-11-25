using Microsoft.Extensions.Localization;
using Squirrel.Wiki.Core.Services.Configuration;
using System.Globalization;

namespace Squirrel.Wiki.Web.Extensions;

/// <summary>
/// Extension methods for DateTime timezone conversion with localization support
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Converts a UTC DateTime to the configured timezone for display
    /// </summary>
    /// <param name="utcDateTime">The UTC DateTime to convert</param>
    /// <param name="timezoneService">The timezone service</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>DateTime in the configured timezone</returns>
    public static async Task<DateTime> ToLocalTimeAsync(
        this DateTime utcDateTime, 
        ITimezoneService timezoneService,
        CancellationToken cancellationToken = default)
    {
        // Ensure the DateTime is treated as UTC
        if (utcDateTime.Kind != DateTimeKind.Utc)
        {
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        }
        
        return await timezoneService.ConvertFromUtcAsync(utcDateTime, cancellationToken);
    }

    /// <summary>
    /// Formats a UTC DateTime for display in the configured timezone using culture-aware formatting
    /// </summary>
    /// <param name="utcDateTime">The UTC DateTime to format</param>
    /// <param name="timezoneService">The timezone service</param>
    /// <param name="format">The format string (e.g., "g" for general short, "f" for full date/time, or custom format)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Formatted string in the configured timezone</returns>
    public static async Task<string> ToLocalTimeStringAsync(
        this DateTime utcDateTime,
        ITimezoneService timezoneService,
        string format = "g",
        CancellationToken cancellationToken = default)
    {
        var localTime = await utcDateTime.ToLocalTimeAsync(timezoneService, cancellationToken);
        
        // Use current culture for formatting
        return localTime.ToString(format, CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Formats a UTC DateTime for display in short date format using culture-aware formatting
    /// </summary>
    /// <param name="utcDateTime">The UTC DateTime to format</param>
    /// <param name="timezoneService">The timezone service</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Formatted string in short date format</returns>
    public static async Task<string> ToLocalDateStringAsync(
        this DateTime utcDateTime,
        ITimezoneService timezoneService,
        CancellationToken cancellationToken = default)
    {
        var localTime = await utcDateTime.ToLocalTimeAsync(timezoneService, cancellationToken);
        // Use "d" for short date pattern (culture-aware)
        return localTime.ToString("d", CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Formats a UTC DateTime for display with relative time (e.g., "2 hours ago")
    /// Falls back to formatted date if older than 7 days
    /// </summary>
    /// <param name="utcDateTime">The UTC DateTime to format</param>
    /// <param name="timezoneService">The timezone service</param>
    /// <param name="localizer">String localizer for localized relative time strings</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Relative time string or formatted date</returns>
    public static async Task<string> ToRelativeTimeStringAsync(
        this DateTime utcDateTime,
        ITimezoneService timezoneService,
        IStringLocalizer localizer,
        CancellationToken cancellationToken = default)
    {
        var localTime = await utcDateTime.ToLocalTimeAsync(timezoneService, cancellationToken);
        var now = await DateTime.UtcNow.ToLocalTimeAsync(timezoneService, cancellationToken);
        var timeSpan = now - localTime;

        if (timeSpan.TotalSeconds < 60)
            return localizer["JustNow"];
        
        if (timeSpan.TotalMinutes < 60)
        {
            var minutes = (int)timeSpan.TotalMinutes;
            return minutes == 1 
                ? localizer["OneMinuteAgo"] 
                : localizer["MinutesAgo", minutes];
        }
        
        if (timeSpan.TotalHours < 24)
        {
            var hours = (int)timeSpan.TotalHours;
            return hours == 1 
                ? localizer["OneHourAgo"] 
                : localizer["HoursAgo", hours];
        }
        
        if (timeSpan.TotalDays < 7)
        {
            var days = (int)timeSpan.TotalDays;
            return days == 1 
                ? localizer["OneDayAgo"] 
                : localizer["DaysAgo", days];
        }

        // For older dates, show the formatted date
        return await utcDateTime.ToLocalDateStringAsync(timezoneService, cancellationToken);
    }

    /// <summary>
    /// Converts a nullable UTC DateTime to the configured timezone for display
    /// </summary>
    /// <param name="utcDateTime">The nullable UTC DateTime to convert</param>
    /// <param name="timezoneService">The timezone service</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>DateTime in the configured timezone, or null if input is null</returns>
    public static async Task<DateTime?> ToLocalTimeAsync(
        this DateTime? utcDateTime, 
        ITimezoneService timezoneService,
        CancellationToken cancellationToken = default)
    {
        if (!utcDateTime.HasValue)
            return null;

        return await utcDateTime.Value.ToLocalTimeAsync(timezoneService, cancellationToken);
    }

    /// <summary>
    /// Formats a nullable UTC DateTime for display in the configured timezone
    /// </summary>
    /// <param name="utcDateTime">The nullable UTC DateTime to format</param>
    /// <param name="timezoneService">The timezone service</param>
    /// <param name="format">The format string (e.g., "g" for general short, "f" for full date/time)</param>
    /// <param name="defaultValue">Value to return if DateTime is null (default: "N/A")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Formatted string in the configured timezone, or default value if null</returns>
    public static async Task<string> ToLocalTimeStringAsync(
        this DateTime? utcDateTime,
        ITimezoneService timezoneService,
        string format = "g",
        string defaultValue = "N/A",
        CancellationToken cancellationToken = default)
    {
        if (!utcDateTime.HasValue)
            return defaultValue;

        return await utcDateTime.Value.ToLocalTimeStringAsync(timezoneService, format, cancellationToken);
    }
}
