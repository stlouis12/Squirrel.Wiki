using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Database;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Events;
using Squirrel.Wiki.Core.Exceptions;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Services.Caching;

namespace Squirrel.Wiki.Core.Services.Configuration;

/// <summary>
/// Service for managing application settings stored in the database
/// </summary>
public class SettingsService : BaseService, ISettingsService
{
    private readonly SquirrelDbContext _dbContext;
    private readonly IUserContext _userContext;
    private const string CacheKeyPrefix = "settings:";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);

    public SettingsService(
        SquirrelDbContext dbContext,
        IUserContext userContext,
        ILogger<SettingsService> logger,
        ICacheService cache,
        IEventPublisher eventPublisher,
        IConfigurationService configuration)
        : base(logger, cache, eventPublisher, null, configuration)
    {
        _dbContext = dbContext;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public async Task<T?> GetSettingAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Setting key cannot be null or empty", nameof(key));

        // Try cache first (cache as plain string, not JSON)
        var cacheKey = GetCacheKey(key);
        var cachedValue = await Cache.GetAsync<string>(cacheKey, cancellationToken);

        if (cachedValue != null)
        {
            LogDebug("Cache hit for setting {Key}", key);
            try
            {
                return ConvertPlainValue<T>(cachedValue);
            }
            catch (Exception)
            {
                LogWarning("Failed to convert cached setting {Key}", key);
                // Continue to database lookup
            }
        }

        // Get from database
        var setting = await _dbContext.SiteConfigurations
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting == null)
        {
            LogDebug("Setting {Key} not found", key);
            return default;
        }

        try
        {
            // Convert the plain string value to the target type
            T? value = ConvertPlainValue<T>(setting.Value);

            // Cache the result as plain string (not JSON)
            await Cache.SetAsync(cacheKey, setting.Value, CacheExpiration, cancellationToken);

            return value;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to convert setting {Key}", key);
            return default;
        }
    }

    /// <inheritdoc/>
    public async Task SaveSettingAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Setting key cannot be null or empty", nameof(key));

        // Check if this setting is from an environment variable
        var existingSetting = await _dbContext.SiteConfigurations
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (existingSetting?.IsFromEnvironment == true)
        {
            LogWarning(
                "Attempted to modify environment-sourced setting {Key} by {User}. Operation blocked.",
                key, _userContext.Username ?? "System");
            throw new ConfigurationException(
                $"Cannot modify setting '{key}' because it is configured via environment variable '{existingSetting.EnvironmentVariableName}'. " +
                "To change this setting, update the environment variable and restart the application.",
                "SETTING_FROM_ENVIRONMENT"
            ).WithContext("SettingKey", key)
             .WithContext("EnvironmentVariable", existingSetting.EnvironmentVariableName ?? "unknown");
        }

        // Store as plain string value, not JSON-serialized
        // This matches the behavior of DatabaseConfigurationProvider
        var stringValue = value?.ToString() ?? string.Empty;
        var currentUser = _userContext.Username ?? "System";

        if (existingSetting == null)
        {
            // Create new setting
            existingSetting = new SiteConfiguration
            {
                Id = Guid.NewGuid(),
                Key = key,
                Value = stringValue,
                ModifiedOn = DateTime.UtcNow,
                ModifiedBy = currentUser,
                IsFromEnvironment = false
            };

            _dbContext.SiteConfigurations.Add(existingSetting);
            LogInfo("Creating new setting {Key} by {User}", key, currentUser);
        }
        else
        {
            // Update existing setting
            var oldValue = existingSetting.Value;
            existingSetting.Value = stringValue;
            existingSetting.ModifiedOn = DateTime.UtcNow;
            existingSetting.ModifiedBy = currentUser;

            LogInfo(
                "Updating setting {Key} by {User}. Old value: {OldValue}, New value: {NewValue}",
                key, currentUser, oldValue, stringValue);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        await Cache.RemoveAsync(GetCacheKey(key), cancellationToken);

        LogDebug("Setting {Key} saved successfully", key);
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, string>> GetAllSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.SiteConfigurations
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        LogDebug("Retrieved {Count} settings", settings.Count);

        return settings;
    }

    /// <inheritdoc/>
    public async Task DeleteSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Setting key cannot be null or empty", nameof(key));

        var setting = await _dbContext.SiteConfigurations
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting != null)
        {
            _dbContext.SiteConfigurations.Remove(setting);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Invalidate cache
            await Cache.RemoveAsync(GetCacheKey(key), cancellationToken);

            LogInfo("Setting {Key} deleted by {User}", key, _userContext.Username ?? "System");
        }
        else
        {
            LogDebug("Setting {Key} not found for deletion", key);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SettingExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return await _dbContext.SiteConfigurations
            .AnyAsync(s => s.Key == key, cancellationToken);
    }

    private static string GetCacheKey(string key)
    {
        return $"{CacheKeyPrefix}{key}";
    }

    /// <summary>
    /// Converts a plain string value to the target type
    /// Handles common types like bool, int, string, etc.
    /// </summary>
    private static T? ConvertPlainValue<T>(string value)
    {
        var targetType = typeof(T);
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (string.IsNullOrWhiteSpace(value))
            return default;

        try
        {
            // Handle boolean
            if (underlyingType == typeof(bool))
            {
                if (bool.TryParse(value, out var boolValue))
                    return (T)(object)boolValue;
            }
            // Handle int
            else if (underlyingType == typeof(int))
            {
                if (int.TryParse(value, out var intValue))
                    return (T)(object)intValue;
            }
            // Handle long
            else if (underlyingType == typeof(long))
            {
                if (long.TryParse(value, out var longValue))
                    return (T)(object)longValue;
            }
            // Handle double
            else if (underlyingType == typeof(double))
            {
                if (double.TryParse(value, out var doubleValue))
                    return (T)(object)doubleValue;
            }
            // Handle string
            else if (underlyingType == typeof(string))
            {
                return (T)(object)value;
            }

            // For other types, try Convert.ChangeType
            return (T)Convert.ChangeType(value, underlyingType);
        }
        catch
        {
            return default;
        }
    }
}
