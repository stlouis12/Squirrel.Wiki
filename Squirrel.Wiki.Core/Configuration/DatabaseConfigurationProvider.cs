using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Database;
using Squirrel.Wiki.Core.Security;

namespace Squirrel.Wiki.Core.Configuration;

/// <summary>
/// Configuration provider that reads and writes values from the database
/// This is medium priority (between environment variables and defaults)
/// </summary>
public class DatabaseConfigurationProvider : IConfigurationProvider
{
    private readonly SquirrelDbContext _context;
    private readonly IUserContext _userContext;
    private readonly ILogger<DatabaseConfigurationProvider> _logger;

    public string Name => "Database";
    public int Priority => 50; // Medium priority

    public DatabaseConfigurationProvider(
        SquirrelDbContext context,
        IUserContext userContext,
        ILogger<DatabaseConfigurationProvider> logger)
    {
        _context = context;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task<ConfigurationValue?> GetValueAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var setting = await _context.SiteConfigurations
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == key, ct);

            if (setting == null)
            {
                return null;
            }

            // Get metadata to determine the type
            if (!ConfigurationMetadataRegistry.HasMetadata(key))
            {
                _logger.LogWarning("Configuration key {Key} found in database but not in metadata registry", key);
                return null;
            }

            var metadata = ConfigurationMetadataRegistry.GetMetadata(key);

            // Convert the string value to the appropriate type
            object typedValue;
            try
            {
                typedValue = ConvertValue(setting.Value, metadata.ValueType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert database value for {Key} to type {Type}", 
                    key, metadata.ValueType.Name);
                return null;
            }

            var configValue = new ConfigurationValue
            {
                Key = key,
                Value = typedValue,
                Source = ConfigurationSource.Database,
                LastModified = setting.ModifiedOn,
                ModifiedBy = setting.ModifiedBy
            };

            _logger.LogDebug("Loaded configuration from database {Key}: {Value}", key, typedValue);

            return configValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration from database for {Key}", key);
            return null;
        }
    }

    public Task<bool> CanSetValueAsync(string key, CancellationToken ct = default)
    {
        // Database provider can set values
        return Task.FromResult(true);
    }

    public async Task SetValueAsync(string key, object value, CancellationToken ct = default)
    {
        try
        {
            // Validate the key exists in metadata
            if (!ConfigurationMetadataRegistry.HasMetadata(key))
            {
                throw new ArgumentException($"Unknown configuration key: {key}", nameof(key));
            }

            var metadata = ConfigurationMetadataRegistry.GetMetadata(key);

            // Convert value to string for storage
            var stringValue = value?.ToString() ?? "";

            // Find existing setting or create new one
            var setting = await _context.SiteConfigurations
                .FirstOrDefaultAsync(s => s.Key == key, ct);

            var currentUser = _userContext.Username ?? "System";

            if (setting == null)
            {
                // Create new setting
                setting = new Database.Entities.SiteConfiguration
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    Value = stringValue,
                    ModifiedOn = DateTime.UtcNow,
                    ModifiedBy = currentUser
                };
                _context.SiteConfigurations.Add(setting);
                _logger.LogInformation("Created new configuration {Key} = {Value} by {User}", 
                    key, stringValue, currentUser);
            }
            else
            {
                // Update existing setting
                setting.Value = stringValue;
                setting.ModifiedOn = DateTime.UtcNow;
                setting.ModifiedBy = currentUser;
                _logger.LogInformation("Updated configuration {Key} = {Value} by {User}", 
                    key, stringValue, currentUser);
            }

            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration to database for {Key}", key);
            throw;
        }
    }

    public async Task<IEnumerable<string>> GetAllKeysAsync(CancellationToken ct = default)
    {
        try
        {
            var keys = await _context.SiteConfigurations
                .AsNoTracking()
                .Select(s => s.Key)
                .ToListAsync(ct);

            return keys;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading all configuration keys from database");
            return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// Converts a string value to the specified type
    /// </summary>
    private object ConvertValue(string value, Type targetType)
    {
        if (targetType == typeof(string))
        {
            return value;
        }

        if (targetType == typeof(int))
        {
            return int.Parse(value);
        }

        if (targetType == typeof(bool))
        {
            return bool.Parse(value);
        }

        if (targetType == typeof(long))
        {
            return long.Parse(value);
        }

        if (targetType == typeof(double))
        {
            return double.Parse(value);
        }

        if (targetType == typeof(decimal))
        {
            return decimal.Parse(value);
        }

        // For other types, try Convert.ChangeType
        return Convert.ChangeType(value, targetType);
    }
}
