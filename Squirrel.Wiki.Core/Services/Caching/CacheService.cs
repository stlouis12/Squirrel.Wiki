using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Squirrel.Wiki.Core.Services.Caching;

/// <summary>
/// Generic caching service using ConfigurationService for settings
/// </summary>
public class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly IConfigurationService _configuration;
    private readonly ILogger<CacheService> _logger;
    
    // Track cache keys for pattern-based removal
    private static readonly ConcurrentDictionary<string, byte> _cacheKeys = new();

    public CacheService(
        IDistributedCache cache,
        IConfigurationService configuration,
        ILogger<CacheService> logger)
    {
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsEnabled
    {
        get
        {
            try
            {
                // Use ConfigurationService (cached, fast)
                var enabled = _configuration.GetValueAsync<bool>(
                    "SQUIRREL_ENABLE_CACHING").GetAwaiter().GetResult();
                return enabled;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get cache enabled setting, defaulting to true");
                return true;
            }
        }
    }

    private TimeSpan GetDefaultExpiration()
    {
        try
        {
            var minutes = _configuration.GetValueAsync<int>(
                "SQUIRREL_CACHE_DURATION_MINUTES").GetAwaiter().GetResult();
            return TimeSpan.FromMinutes(minutes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get cache duration setting, defaulting to 30 minutes");
            return TimeSpan.FromMinutes(30);
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
}
