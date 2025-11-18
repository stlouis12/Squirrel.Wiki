using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Database;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Security;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Service for managing application settings stored in the database
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly SquirrelDbContext _dbContext;
    private readonly IDistributedCache _cache;
    private readonly IUserContext _userContext;
    private readonly ILogger<SettingsService> _logger;
    private const string CacheKeyPrefix = "settings:";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);

    public SettingsService(
        SquirrelDbContext dbContext,
        IDistributedCache cache,
        IUserContext userContext,
        ILogger<SettingsService> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _userContext = userContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<T?> GetSettingAsync<T>(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Setting key cannot be null or empty", nameof(key));

        // Try cache first
        var cacheKey = GetCacheKey(key);
        var cachedValue = await _cache.GetStringAsync(cacheKey, ct);

        if (cachedValue != null)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(cachedValue);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize cached setting {Key}", key);
                // Continue to database lookup
            }
        }

        // Get from database
        var setting = await _dbContext.SiteConfigurations
            .FirstOrDefaultAsync(s => s.Key == key, ct);

        if (setting == null)
        {
            _logger.LogDebug("Setting {Key} not found", key);
            return default;
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(setting.Value);

            // Cache the result
            await _cache.SetStringAsync(
                cacheKey,
                setting.Value,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheExpiration
                },
                ct);

            return value;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize setting {Key}", key);
            return default;
        }
    }

    /// <inheritdoc/>
    public async Task SaveSettingAsync<T>(string key, T value, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Setting key cannot be null or empty", nameof(key));

        var jsonValue = JsonSerializer.Serialize(value);
        var currentUser = _userContext.Username ?? "System";

        var setting = await _dbContext.SiteConfigurations
            .FirstOrDefaultAsync(s => s.Key == key, ct);

        if (setting == null)
        {
            // Create new setting
            setting = new SiteConfiguration
            {
                Id = Guid.NewGuid(),
                Key = key,
                Value = jsonValue,
                ModifiedOn = DateTime.UtcNow,
                ModifiedBy = currentUser
            };

            _dbContext.SiteConfigurations.Add(setting);
            _logger.LogInformation("Creating new setting {Key} by {User}", key, currentUser);
        }
        else
        {
            // Update existing setting
            var oldValue = setting.Value;
            setting.Value = jsonValue;
            setting.ModifiedOn = DateTime.UtcNow;
            setting.ModifiedBy = currentUser;

            _logger.LogInformation(
                "Updating setting {Key} by {User}. Old value: {OldValue}, New value: {NewValue}",
                key, currentUser, oldValue, jsonValue);
        }

        await _dbContext.SaveChangesAsync(ct);

        // Invalidate cache
        await _cache.RemoveAsync(GetCacheKey(key), ct);

        _logger.LogDebug("Setting {Key} saved successfully", key);
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, string>> GetAllSettingsAsync(CancellationToken ct = default)
    {
        var settings = await _dbContext.SiteConfigurations
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        _logger.LogDebug("Retrieved {Count} settings", settings.Count);

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
            await _cache.RemoveAsync(GetCacheKey(key), ct);

            _logger.LogInformation("Setting {Key} deleted by {User}", key, _userContext.Username ?? "System");
        }
        else
        {
            _logger.LogDebug("Setting {Key} not found for deletion", key);
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

    private static string GetCacheKey(string key)
    {
        return $"{CacheKeyPrefix}{key}";
    }
}
