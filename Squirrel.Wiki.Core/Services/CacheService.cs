using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Database;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Generic caching service that reads settings directly from database to avoid circular dependencies
/// </summary>
public class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly IMemoryCache? _memoryCache;
    private readonly SquirrelDbContext _context;
    private readonly ILogger<CacheService> _logger;
    private bool? _isEnabled;
    private TimeSpan? _defaultExpiration;
    private DateTime _lastSettingsCheck = DateTime.MinValue;
    private static readonly TimeSpan SettingsCacheDuration = TimeSpan.FromMinutes(5);
    
    // Track cache keys for pattern-based removal
    private static readonly ConcurrentDictionary<string, byte> _cacheKeys = new();

    public CacheService(
        IDistributedCache cache,
        SquirrelDbContext context,
        ILogger<CacheService> logger,
        IMemoryCache? memoryCache = null)
    {
        _cache = cache;
        _memoryCache = memoryCache;
        _context = context;
        _logger = logger;
    }

    public bool IsEnabled
    {
        get
        {
            RefreshSettingsIfNeeded();
            return _isEnabled ?? true; // Default to enabled if not set
        }
    }

    private TimeSpan GetDefaultExpiration()
    {
        RefreshSettingsIfNeeded();
        return _defaultExpiration ?? TimeSpan.FromMinutes(30); // Default to 30 minutes
    }

    /// <summary>
    /// Refresh settings from database if cache has expired
    /// This reads directly from database without using cache to avoid circular dependency
    /// </summary>
    private void RefreshSettingsIfNeeded()
    {
        if (DateTime.UtcNow - _lastSettingsCheck < SettingsCacheDuration)
        {
            return; // Settings are still fresh
        }

        try
        {
            // Check environment variable first, then database setting
            var cacheEnabledEnv = Environment.GetEnvironmentVariable("SQUIRREL_CACHE_ENABLED");
            if (!string.IsNullOrEmpty(cacheEnabledEnv))
            {
                _isEnabled = bool.TryParse(cacheEnabledEnv, out var enabled) ? enabled : true;
                _logger.LogDebug("CacheEnabled loaded from environment variable: {Value}", _isEnabled);
            }
            else
            {
                // Read CacheEnabled setting directly from database (no caching)
                var cacheEnabledSetting = _context.SiteConfigurations
                    .AsNoTracking()
                    .FirstOrDefault(s => s.Key == "CacheEnabled");
                
                _isEnabled = cacheEnabledSetting != null && 
                             bool.TryParse(cacheEnabledSetting.Value, out var enabled) ? 
                             enabled : true;
                _logger.LogDebug("CacheEnabled loaded from database: {Value}", _isEnabled);
            }

            // Check environment variable first, then database setting
            var cacheExpirationEnv = Environment.GetEnvironmentVariable("SQUIRREL_CACHE_EXPIRATION_MINUTES");
            int minutes;
            if (!string.IsNullOrEmpty(cacheExpirationEnv))
            {
                minutes = int.TryParse(cacheExpirationEnv, out var mins) ? mins : 30;
                _logger.LogDebug("CacheExpirationMinutes loaded from environment variable: {Value}", minutes);
            }
            else
            {
                // Read CacheExpirationMinutes setting directly from database (no caching)
                var expirationSetting = _context.SiteConfigurations
                    .AsNoTracking()
                    .FirstOrDefault(s => s.Key == "CacheExpirationMinutes");
                
                minutes = expirationSetting != null && 
                             int.TryParse(expirationSetting.Value, out var mins) ? 
                             mins : 30;
                _logger.LogDebug("CacheExpirationMinutes loaded from database: {Value}", minutes);
            }
            
            _defaultExpiration = TimeSpan.FromMinutes(minutes > 0 ? minutes : 30);
            
            _lastSettingsCheck = DateTime.UtcNow;
            
            _logger.LogDebug("Cache settings refreshed from database: Enabled={Enabled}, Expiration={Minutes}min", 
                _isEnabled, _defaultExpiration.Value.TotalMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh cache settings from database, using defaults");
            _isEnabled = true;
            _defaultExpiration = TimeSpan.FromMinutes(30);
            _lastSettingsCheck = DateTime.UtcNow;
        }
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        if (!IsEnabled)
        {
            _logger.LogDebug("Cache disabled, skipping get for key: {Key}", key);
            return null;
        }

        try
        {
            var cached = await _cache.GetStringAsync(key, cancellationToken);
            if (cached != null)
            {
                _logger.LogDebug("Cache hit: {Key}", key);
                return JsonSerializer.Deserialize<T>(cached);
            }

            _logger.LogDebug("Cache miss: {Key}", key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading from cache: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
    {
        if (!IsEnabled)
        {
            _logger.LogDebug("Cache disabled, skipping set for key: {Key}", key);
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(value);
            var expirationTime = expiration ?? GetDefaultExpiration();
            
            await _cache.SetStringAsync(
                key,
                json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expirationTime
                },
                cancellationToken);

            // Track the key for pattern-based removal
            _cacheKeys.TryAdd(key, 0);

            _logger.LogDebug("Cached: {Key} (expires in {Minutes} minutes)", key, expirationTime.TotalMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error writing to cache: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
            _cacheKeys.TryRemove(key, out _);
            _logger.LogDebug("Removed from cache: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error removing from cache: {Key}", key);
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            // Convert wildcard pattern to regex
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            
            var regex = new System.Text.RegularExpressions.Regex(regexPattern);
            
            // Find all matching keys
            var matchingKeys = _cacheKeys.Keys.Where(k => regex.IsMatch(k)).ToList();
            
            if (matchingKeys.Any())
            {
                _logger.LogDebug("Removing {Count} cache entries matching pattern: {Pattern}", matchingKeys.Count, pattern);
                
                // Remove each matching key
                foreach (var key in matchingKeys)
                {
                    await _cache.RemoveAsync(key, cancellationToken);
                    _cacheKeys.TryRemove(key, out _);
                }
                
                _logger.LogDebug("Removed {Count} cache entries for pattern: {Pattern}", matchingKeys.Count, pattern);
            }
            else
            {
                _logger.LogDebug("No cache entries found matching pattern: {Pattern}", pattern);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error removing cache entries by pattern: {Pattern}", pattern);
        }
    }

    /// <summary>
    /// Refresh cached settings immediately (call when settings change in the database)
    /// Forces a reload of cache settings from the database on the next access
    /// </summary>
    public void RefreshSettings()
    {
        _lastSettingsCheck = DateTime.MinValue; // Force refresh on next access
        _logger.LogInformation("Cache settings will be refreshed from database on next access");
    }
}
