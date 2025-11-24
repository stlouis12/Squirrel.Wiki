using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Configuration;
using Squirrel.Wiki.Core.Database;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Exceptions;
using Squirrel.Wiki.Core.Security;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Service for managing application settings stored in the database
/// </summary>
public class SettingsService : BaseService, ISettingsService
{
    private readonly SquirrelDbContext _dbContext;
    private readonly IUserContext _userContext;
    private readonly EnvironmentVariableProvider _envProvider;
    private const string CacheKeyPrefix = "settings:";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);

    public SettingsService(
        SquirrelDbContext dbContext,
        IUserContext userContext,
        EnvironmentVariableProvider envProvider,
        ILogger<SettingsService> logger,
        ICacheService cache,
        ICacheInvalidationService cacheInvalidation)
        : base(logger, cache, cacheInvalidation, null)
    {
        _dbContext = dbContext;
        _userContext = userContext;
        _envProvider = envProvider;
    }

    /// <inheritdoc/>
    public async Task<T?> GetSettingAsync<T>(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Setting key cannot be null or empty", nameof(key));

        // Try cache first (cache as string since T might be a value type)
        var cacheKey = GetCacheKey(key);
        var cachedJson = await Cache.GetAsync<string>(cacheKey, ct);

        if (cachedJson != null)
        {
            LogDebug("Cache hit for setting {Key}", key);
            try
            {
                return JsonSerializer.Deserialize<T>(cachedJson);
            }
            catch (JsonException)
            {
                LogWarning("Failed to deserialize cached setting {Key}", key);
                // Continue to database lookup
            }
        }

        // Get from database
        var setting = await _dbContext.SiteConfigurations
            .FirstOrDefaultAsync(s => s.Key == key, ct);

        if (setting == null)
        {
            LogDebug("Setting {Key} not found", key);
            return default;
        }

        try
        {
            // Try to deserialize as JSON first
            T? value;
            try
            {
                value = JsonSerializer.Deserialize<T>(setting.Value);
            }
            catch (JsonException)
            {
                // If JSON deserialization fails, try to convert the plain string value
                value = ConvertPlainValue<T>(setting.Value);
            }

            // Cache the result as JSON string
            await Cache.SetAsync(cacheKey, setting.Value, CacheExpiration, ct);

            return value;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to deserialize setting {Key}", key);
            return default;
        }
    }

    /// <inheritdoc/>
    public async Task SaveSettingAsync<T>(string key, T value, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Setting key cannot be null or empty", nameof(key));

        // Check if this setting is from an environment variable
        var existingSetting = await _dbContext.SiteConfigurations
            .FirstOrDefaultAsync(s => s.Key == key, ct);

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

        var jsonValue = JsonSerializer.Serialize(value);
        var currentUser = _userContext.Username ?? "System";

        if (existingSetting == null)
        {
            // Create new setting
            existingSetting = new SiteConfiguration
            {
                Id = Guid.NewGuid(),
                Key = key,
                Value = jsonValue,
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
            existingSetting.Value = jsonValue;
            existingSetting.ModifiedOn = DateTime.UtcNow;
            existingSetting.ModifiedBy = currentUser;

            LogInfo(
                "Updating setting {Key} by {User}. Old value: {OldValue}, New value: {NewValue}",
                key, currentUser, oldValue, jsonValue);
        }

        await _dbContext.SaveChangesAsync(ct);

        // Invalidate cache
        await Cache.RemoveAsync(GetCacheKey(key), ct);

        LogDebug("Setting {Key} saved successfully", key);
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, string>> GetAllSettingsAsync(CancellationToken ct = default)
    {
        var settings = await _dbContext.SiteConfigurations
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        LogDebug("Retrieved {Count} settings", settings.Count);

        return settings;
    }

    /// <inheritdoc/>
    public async Task DeleteSettingAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Setting key cannot be null or empty", nameof(key));

        var setting = await _dbContext.SiteConfigurations
            .FirstOrDefaultAsync(s => s.Key == key, ct);

        if (setting != null)
        {
            _dbContext.SiteConfigurations.Remove(setting);
            await _dbContext.SaveChangesAsync(ct);

            // Invalidate cache
            await Cache.RemoveAsync(GetCacheKey(key), ct);

            LogInfo("Setting {Key} deleted by {User}", key, _userContext.Username ?? "System");
        }
        else
        {
            LogDebug("Setting {Key} not found for deletion", key);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SettingExistsAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return await _dbContext.SiteConfigurations
            .AnyAsync(s => s.Key == key, ct);
    }

    /// <summary>
    /// Synchronizes environment variable settings to the database
    /// This should be called on application startup
    /// </summary>
    public async Task SyncEnvironmentVariablesAsync(CancellationToken ct = default)
    {
        LogInfo("Starting environment variable synchronization...");

        var envSettings = _envProvider.GetAllEnvironmentSettings();
        var syncedCount = 0;

        foreach (var (key, value) in envSettings)
        {
            try
            {
                // Only sync if the environment variable has a valid value
                // Invalid values are already logged by EnvironmentVariableProvider
                if (!_envProvider.IsFromEnvironment(key))
                {
                    LogDebug(
                        "Skipping sync for setting '{Key}' - environment variable has invalid value",
                        key);
                    continue;
                }

                var setting = await _dbContext.SiteConfigurations
                    .FirstOrDefaultAsync(s => s.Key == key, ct);

                var jsonValue = JsonSerializer.Serialize(value);
                var envVarName = _envProvider.GetEnvironmentVariableName(key);

                if (setting == null)
                {
                    // Create new setting from environment variable
                    setting = new SiteConfiguration
                    {
                        Id = Guid.NewGuid(),
                        Key = key,
                        Value = jsonValue,
                        ModifiedOn = DateTime.UtcNow,
                        ModifiedBy = "Environment",
                        IsFromEnvironment = true,
                        EnvironmentVariableName = envVarName
                    };

                    _dbContext.SiteConfigurations.Add(setting);
                    LogInfo(
                        "Created setting '{Key}' from environment variable '{EnvVar}'",
                        key, envVarName);
                }
                else if (!setting.IsFromEnvironment || setting.Value != jsonValue)
                {
                    // Update existing setting with environment variable value
                    setting.Value = jsonValue;
                    setting.ModifiedOn = DateTime.UtcNow;
                    setting.ModifiedBy = "Environment";
                    setting.IsFromEnvironment = true;
                    setting.EnvironmentVariableName = envVarName;

                    LogInfo(
                        "Updated setting '{Key}' from environment variable '{EnvVar}'",
                        key, envVarName);
                }

                // Invalidate cache for this setting
                await Cache.RemoveAsync(GetCacheKey(key), ct);
                syncedCount++;
            }
            catch (Exception ex)
            {
                LogError(ex, "Error syncing environment variable for setting '{Key}'", key);
            }
        }

        if (syncedCount > 0)
        {
            await _dbContext.SaveChangesAsync(ct);
            LogInfo(
                "Environment variable synchronization complete. Synced {Count} settings.",
                syncedCount);
        }
        else
        {
            LogInfo("No environment variables to sync.");
        }
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

