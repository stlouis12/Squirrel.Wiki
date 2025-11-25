using Microsoft.Extensions.Logging;

namespace Squirrel.Wiki.Core.Services.Configuration;

/// <summary>
/// Service for handling timezone conversions and configuration
/// </summary>
public class TimezoneService : ITimezoneService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<TimezoneService> _logger;
    private TimeZoneInfo? _cachedTimezone;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public TimezoneService(ISettingsService settingsService, ILogger<TimezoneService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TimeZoneInfo> GetConfiguredTimezoneAsync(CancellationToken cancellationToken = default)
    {
        // Check cache
        if (_cachedTimezone != null && DateTime.UtcNow < _cacheExpiry)
        {
            return _cachedTimezone;
        }

        // Get timezone ID from settings
        var timezoneId = await _settingsService.GetSettingAsync<string>("TimeZone", cancellationToken);
        
        if (string.IsNullOrEmpty(timezoneId))
        {
            timezoneId = "UTC";
        }

        try
        {
            _cachedTimezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
            _logger.LogDebug("Configured timezone: {TimezoneId} ({DisplayName})", timezoneId, _cachedTimezone.DisplayName);
            return _cachedTimezone;
        }
        catch (TimeZoneNotFoundException ex)
        {
            _logger.LogWarning(ex, "Configured timezone '{TimezoneId}' not found, falling back to UTC", timezoneId);
            _cachedTimezone = TimeZoneInfo.Utc;
            _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
            return _cachedTimezone;
        }
    }

    /// <inheritdoc/>
    public async Task<DateTime> ConvertFromUtcAsync(DateTime utcDateTime, CancellationToken cancellationToken = default)
    {
        // Ensure the DateTime is treated as UTC
        if (utcDateTime.Kind != DateTimeKind.Utc)
        {
            _logger.LogWarning("ConvertFromUtcAsync called with non-UTC DateTime (Kind: {Kind}). Treating as UTC.", utcDateTime.Kind);
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        }

        var timezone = await GetConfiguredTimezoneAsync(cancellationToken);
        
        // If timezone is UTC, just return the original
        if (timezone.Id == "UTC")
        {
            return utcDateTime;
        }

        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, timezone);
    }

    /// <inheritdoc/>
    public async Task<DateTime> ConvertToUtcAsync(DateTime localDateTime, CancellationToken cancellationToken = default)
    {
        var timezone = await GetConfiguredTimezoneAsync(cancellationToken);
        
        // If timezone is UTC, just ensure the DateTime is marked as UTC
        if (timezone.Id == "UTC")
        {
            return DateTime.SpecifyKind(localDateTime, DateTimeKind.Utc);
        }

        // If the DateTime is already UTC, return it
        if (localDateTime.Kind == DateTimeKind.Utc)
        {
            return localDateTime;
        }

        return TimeZoneInfo.ConvertTimeToUtc(localDateTime, timezone);
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetAvailableTimezoneIds()
    {
        return TimeZoneInfo.GetSystemTimeZones()
            .OrderBy(tz => tz.BaseUtcOffset)
            .ThenBy(tz => tz.DisplayName)
            .Select(tz => tz.Id);
    }

    /// <inheritdoc/>
    public string GetTimezoneDisplayName(string timezoneId)
    {
        try
        {
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            return timezone.DisplayName;
        }
        catch (TimeZoneNotFoundException)
        {
            _logger.LogWarning("Timezone ID '{TimezoneId}' not found", timezoneId);
            return timezoneId;
        }
    }

    /// <inheritdoc/>
    public bool IsValidTimezoneId(string timezoneId)
    {
        if (string.IsNullOrWhiteSpace(timezoneId))
        {
            return false;
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
    }
}
