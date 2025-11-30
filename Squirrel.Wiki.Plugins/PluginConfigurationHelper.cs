using Squirrel.Wiki.Contracts.Plugins;

namespace Squirrel.Wiki.Plugins;

/// <summary>
/// Helper class for parsing and managing plugin configuration values
/// Provides type-safe parsing and configuration manipulation utilities
/// </summary>
public static class PluginConfigurationHelper
{
    /// <summary>
    /// Parses a configuration value as boolean
    /// Supports: true/false, 1/0, yes/no (case-insensitive)
    /// </summary>
    /// <param name="value">The string value to parse</param>
    /// <param name="defaultValue">The default value if parsing fails or value is null/empty</param>
    /// <returns>The parsed boolean value or default</returns>
    public static bool ParseBool(string? value, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        var lower = value.ToLowerInvariant().Trim();
        
        // True values
        if (lower == "true" || lower == "1" || lower == "yes" || lower == "on" || lower == "enabled")
        {
            return true;
        }
        
        // False values
        if (lower == "false" || lower == "0" || lower == "no" || lower == "off" || lower == "disabled")
        {
            return false;
        }

        // If we can't parse it, return default
        return defaultValue;
    }

    /// <summary>
    /// Parses a configuration value as integer
    /// </summary>
    /// <param name="value">The string value to parse</param>
    /// <param name="defaultValue">The default value if parsing fails or value is null/empty</param>
    /// <returns>The parsed integer value or default</returns>
    public static int ParseInt(string? value, int defaultValue = 0)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return int.TryParse(value.Trim(), out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Parses a configuration value as long integer
    /// </summary>
    /// <param name="value">The string value to parse</param>
    /// <param name="defaultValue">The default value if parsing fails or value is null/empty</param>
    /// <returns>The parsed long value or default</returns>
    public static long ParseLong(string? value, long defaultValue = 0)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return long.TryParse(value.Trim(), out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Parses a configuration value as double
    /// </summary>
    /// <param name="value">The string value to parse</param>
    /// <param name="defaultValue">The default value if parsing fails or value is null/empty</param>
    /// <returns>The parsed double value or default</returns>
    public static double ParseDouble(string? value, double defaultValue = 0.0)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return double.TryParse(value.Trim(), out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Parses a configuration value as URL
    /// </summary>
    /// <param name="value">The string value to parse</param>
    /// <returns>The parsed URI or null if invalid/empty</returns>
    public static Uri? ParseUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ? uri : null;
    }

    /// <summary>
    /// Parses a configuration value as TimeSpan
    /// Supports formats like: "00:05:00", "5m", "30s", "1h"
    /// </summary>
    /// <param name="value">The string value to parse</param>
    /// <param name="defaultValue">The default value if parsing fails or value is null/empty</param>
    /// <returns>The parsed TimeSpan value or default</returns>
    public static TimeSpan ParseTimeSpan(string? value, TimeSpan? defaultValue = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue ?? TimeSpan.Zero;
        }

        var trimmed = value.Trim();

        // Try standard TimeSpan format first
        if (TimeSpan.TryParse(trimmed, out var result))
        {
            return result;
        }

        // Try shorthand formats (e.g., "30s", "5m", "1h")
        if (trimmed.Length > 1)
        {
            var unit = trimmed[^1];
            var numberPart = trimmed[..^1];

            if (double.TryParse(numberPart, out var number))
            {
                return unit switch
                {
                    's' or 'S' => TimeSpan.FromSeconds(number),
                    'm' or 'M' => TimeSpan.FromMinutes(number),
                    'h' or 'H' => TimeSpan.FromHours(number),
                    'd' or 'D' => TimeSpan.FromDays(number),
                    _ => defaultValue ?? TimeSpan.Zero
                };
            }
        }

        return defaultValue ?? TimeSpan.Zero;
    }

    /// <summary>
    /// Merges configuration with defaults from schema
    /// Values in config take precedence over defaults
    /// </summary>
    /// <param name="config">The current configuration dictionary</param>
    /// <param name="schema">The configuration schema with default values</param>
    /// <returns>A new dictionary with merged values</returns>
    public static Dictionary<string, string> MergeWithDefaults(
        Dictionary<string, string> config,
        IEnumerable<PluginConfigurationItem> schema)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (schema == null)
        {
            throw new ArgumentNullException(nameof(schema));
        }

        var result = new Dictionary<string, string>(config, StringComparer.OrdinalIgnoreCase);

        foreach (var item in schema)
        {
            // Only add default if key doesn't exist and default is not null/empty
            if (!result.ContainsKey(item.Key) && !string.IsNullOrEmpty(item.DefaultValue))
            {
                result[item.Key] = item.DefaultValue;
            }
        }

        return result;
    }

    /// <summary>
    /// Filters configuration to only include keys defined in schema
    /// Useful for removing obsolete or invalid configuration keys
    /// </summary>
    /// <param name="config">The current configuration dictionary</param>
    /// <param name="schema">The configuration schema</param>
    /// <returns>A new dictionary with only valid keys</returns>
    public static Dictionary<string, string> FilterBySchema(
        Dictionary<string, string> config,
        IEnumerable<PluginConfigurationItem> schema)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (schema == null)
        {
            throw new ArgumentNullException(nameof(schema));
        }

        var validKeys = new HashSet<string>(
            schema.Select(s => s.Key),
            StringComparer.OrdinalIgnoreCase);

        return config
            .Where(kvp => validKeys.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets a configuration value with type conversion
    /// </summary>
    /// <typeparam name="T">The target type</typeparam>
    /// <param name="config">The configuration dictionary</param>
    /// <param name="key">The configuration key</param>
    /// <param name="defaultValue">The default value if key not found or conversion fails</param>
    /// <returns>The typed value or default</returns>
    public static T GetValue<T>(Dictionary<string, string> config, string key, T defaultValue = default!)
    {
        if (config == null || !config.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        try
        {
            var targetType = typeof(T);

            // Handle nullable types
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                targetType = Nullable.GetUnderlyingType(targetType)!;
            }

            // String - no conversion needed
            if (targetType == typeof(string))
            {
                return (T)(object)value;
            }

            // Boolean
            if (targetType == typeof(bool))
            {
                return (T)(object)ParseBool(value);
            }

            // Integer types
            if (targetType == typeof(int))
            {
                return (T)(object)ParseInt(value);
            }

            if (targetType == typeof(long))
            {
                return (T)(object)ParseLong(value);
            }

            // Floating point types
            if (targetType == typeof(double))
            {
                return (T)(object)ParseDouble(value);
            }

            // URI
            if (targetType == typeof(Uri))
            {
                var uri = ParseUrl(value);
                return uri != null ? (T)(object)uri : defaultValue;
            }

            // TimeSpan
            if (targetType == typeof(TimeSpan))
            {
                return (T)(object)ParseTimeSpan(value);
            }

            // Enum
            if (targetType.IsEnum)
            {
                return (T)Enum.Parse(targetType, value, ignoreCase: true);
            }

            // Generic conversion for other types
            return (T)Convert.ChangeType(value, targetType);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Validates that all required configuration keys are present and not empty
    /// </summary>
    /// <param name="config">The configuration dictionary</param>
    /// <param name="schema">The configuration schema</param>
    /// <returns>List of missing required keys</returns>
    public static List<string> GetMissingRequiredKeys(
        Dictionary<string, string> config,
        IEnumerable<PluginConfigurationItem> schema)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (schema == null)
        {
            throw new ArgumentNullException(nameof(schema));
        }

        var missing = new List<string>();

        foreach (var item in schema.Where(s => s.IsRequired))
        {
            if (!config.TryGetValue(item.Key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                missing.Add(item.Key);
            }
        }

        return missing;
    }

    /// <summary>
    /// Masks secret values in configuration for display purposes
    /// </summary>
    /// <param name="config">The configuration dictionary</param>
    /// <param name="schema">The configuration schema</param>
    /// <param name="mask">The mask string to use (default: "••••••••")</param>
    /// <returns>A new dictionary with secret values masked</returns>
    public static Dictionary<string, string> MaskSecrets(
        Dictionary<string, string> config,
        IEnumerable<PluginConfigurationItem> schema,
        string mask = "••••••••")
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (schema == null)
        {
            throw new ArgumentNullException(nameof(schema));
        }

        var result = new Dictionary<string, string>(config, StringComparer.OrdinalIgnoreCase);
        var secretKeys = new HashSet<string>(
            schema.Where(s => s.IsSecret).Select(s => s.Key),
            StringComparer.OrdinalIgnoreCase);

        foreach (var key in secretKeys)
        {
            if (result.ContainsKey(key) && !string.IsNullOrEmpty(result[key]))
            {
                result[key] = mask;
            }
        }

        return result;
    }
}
