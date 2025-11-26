using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using System.Reflection;

namespace Squirrel.Wiki.Core.Configuration;

/// <summary>
/// Main configuration service that orchestrates multiple configuration providers
/// Priority: Environment Variables (100) > Database (50) > Defaults (0)
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly IEnumerable<IConfigurationProvider> _providers;
    private readonly ILogger<ConfigurationService> _logger;

    public ConfigurationService(
        IEnumerable<IConfigurationProvider> providers,
        ILogger<ConfigurationService> logger)
    {
        _providers = providers.OrderByDescending(p => p.Priority).ToList();
        _logger = logger;

        _logger.LogInformation("ConfigurationService initialized with providers: {Providers}",
            string.Join(", ", _providers.Select(p => $"{p.Name}({p.Priority})")));
    }

    public async Task<T> GetAsync<T>(CancellationToken cancellationToken = default) where T : class, new()
    {
        var instance = new T();
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            if (!property.CanWrite)
                continue;

            // Map property name to configuration key
            // Convention: SiteName -> SQUIRREL_SITE_NAME
            var key = ConvertPropertyNameToKey(typeof(T).Name, property.Name);

            try
            {
                var value = await GetValueInternalAsync(key, property.PropertyType, cancellationToken);
                if (value != null)
                {
                    property.SetValue(instance, value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configuration property {Property} for type {Type}",
                    property.Name, typeof(T).Name);
            }
        }

        return instance;
    }

    public async Task<TValue> GetValueAsync<TValue>(string key, CancellationToken cancellationToken = default)
    {
        var value = await GetValueInternalAsync(key, typeof(TValue), cancellationToken);
        
        if (value == null)
        {
            // Try to get default from metadata registry
            try
            {
                var metadata = ConfigurationMetadataRegistry.GetMetadata(key);
                if (metadata.DefaultValue != null)
                {
                    _logger.LogDebug("Configuration '{Key}' not found in any provider, using default from metadata: {Default}",
                        key, metadata.DefaultValue);
                    return (TValue)metadata.DefaultValue;
                }
            }
            catch (KeyNotFoundException)
            {
                // Key not in metadata registry, return default
                _logger.LogDebug("Configuration '{Key}' not found in any provider or metadata registry", key);
            }
            
            return default(TValue)!;
        }

        return (TValue)value;
    }

    public async Task SetValueAsync<TValue>(string key, TValue value, CancellationToken cancellationToken = default)
    {
        // Check if the value is currently from an environment variable
        var source = GetSource(key);
        if (source == ConfigurationSource.EnvironmentVariable)
        {
            throw new InvalidOperationException(
                $"Cannot modify configuration '{key}' because it is set via environment variable. " +
                "To change this setting, update the environment variable and restart the application.");
        }

        // Validate the value
        var validationResult = Validate(key, value!);
        if (!validationResult.IsValid)
        {
            throw new ArgumentException(
                $"Validation failed for '{key}': {string.Join(", ", validationResult.Errors)}");
        }

        // Find a provider that can set values (should be DatabaseConfigurationProvider)
        var writableProvider = _providers.FirstOrDefault(p => p.CanSetValueAsync(key, cancellationToken).Result);
        
        if (writableProvider == null)
        {
            throw new InvalidOperationException(
                $"No writable configuration provider available for key '{key}'");
        }

        await writableProvider.SetValueAsync(key, value!, cancellationToken);

        // Invalidate cache for this key
        InvalidateCache(key);

        _logger.LogInformation("Configuration '{Key}' updated to '{Value}'", key, value);
    }

    public ConfigurationSource GetSource(string key)
    {
        // Check each provider in priority order
        foreach (var provider in _providers)
        {
            var value = provider.GetValueAsync(key).Result;
            if (value != null)
            {
                return value.Source;
            }
        }

        // If not found anywhere, it's using the default
        return ConfigurationSource.Default;
    }

    public ConfigurationProperty GetMetadata(string key)
    {
        return ConfigurationMetadataRegistry.GetMetadata(key);
    }

    public IEnumerable<ConfigurationProperty> GetAllMetadata()
    {
        return ConfigurationMetadataRegistry.GetAllMetadata();
    }

    public IEnumerable<ConfigurationProperty> GetUIVisibleMetadata()
    {
        return ConfigurationMetadataRegistry.GetUIVisibleMetadata();
    }

    public ValidationResult Validate(string key, object value)
    {
        try
        {
            var metadata = ConfigurationMetadataRegistry.GetMetadata(key);
            var rules = metadata.Validation;

            if (rules == null)
            {
                return ValidationResult.Success();
            }

            var errors = new List<string>();

            // Numeric range validation
            if (rules.MinValue.HasValue || rules.MaxValue.HasValue)
            {
                if (!int.TryParse(value?.ToString(), out var numValue))
                {
                    errors.Add($"{metadata.DisplayName} must be a number");
                }
                else
                {
                    if (rules.MinValue.HasValue && numValue < rules.MinValue.Value)
                    {
                        errors.Add($"{metadata.DisplayName} must be at least {rules.MinValue.Value}");
                    }

                    if (rules.MaxValue.HasValue && numValue > rules.MaxValue.Value)
                    {
                        errors.Add($"{metadata.DisplayName} must be at most {rules.MaxValue.Value}");
                    }
                }
            }

            // Allowed values validation
            if (rules.AllowedValues != null && rules.AllowedValues.Length > 0)
            {
                var strValue = value?.ToString() ?? "";
                if (!rules.AllowedValues.Contains(strValue, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"{metadata.DisplayName} must be one of: {string.Join(", ", rules.AllowedValues)}");
                }
            }

            // URL validation
            if (rules.MustBeUrl && !string.IsNullOrEmpty(value?.ToString()))
            {
                if (!Uri.TryCreate(value.ToString(), UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    errors.Add($"{metadata.DisplayName} must be a valid HTTP/HTTPS URL");
                }
            }

            // Regex pattern validation
            if (!string.IsNullOrEmpty(rules.RegexPattern) && !string.IsNullOrEmpty(value?.ToString()))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(value.ToString()!, rules.RegexPattern))
                {
                    errors.Add($"{metadata.DisplayName} does not match the required pattern");
                }
            }

            return errors.Count == 0
                ? ValidationResult.Success()
                : ValidationResult.Failure(errors.ToArray());
        }
        catch (KeyNotFoundException)
        {
            return ValidationResult.Failure($"Unknown configuration key: {key}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating configuration key {Key}", key);
            return ValidationResult.Failure($"Validation error: {ex.Message}");
        }
    }

    public void InvalidateCache(string? key = null)
    {
        // No-op: ConfigurationService no longer caches internally
        // Providers may cache internally and handle their own invalidation
        _logger.LogDebug("Cache invalidation requested for key {Key} (no-op in ConfigurationService)", key ?? "all");
    }

    /// <summary>
    /// Internal method to get a value from providers
    /// </summary>
    private async Task<object?> GetValueInternalAsync(string key, Type targetType, CancellationToken cancellationToken)
    {
        // Query providers in priority order
        foreach (var provider in _providers)
        {
            try
            {
                var configValue = await provider.GetValueAsync(key, cancellationToken);
                if (configValue != null)
                {
                    _logger.LogDebug("Configuration '{Key}' loaded from {Provider}: {Value}",
                        key, provider.Name, configValue.Value);

                    return configValue.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configuration '{Key}' from provider {Provider}",
                    key, provider.Name);
            }
        }

        // Not found in any provider
        _logger.LogWarning("Configuration '{Key}' not found in any provider", key);
        return null;
    }

    /// <summary>
    /// Converts a property name to a configuration key
    /// Example: SiteConfiguration.SiteName -> SQUIRREL_SITE_NAME
    /// </summary>
    private string ConvertPropertyNameToKey(string typeName, string propertyName)
    {
        // Remove "Configuration" suffix from type name
        var prefix = typeName.Replace("Configuration", "");
        
        // Convert to snake_case and add SQUIRREL_ prefix
        var key = $"SQUIRREL_{ConvertToSnakeCase(propertyName)}".ToUpperInvariant();
        
        return key;
    }

    /// <summary>
    /// Converts PascalCase to snake_case
    /// </summary>
    private string ConvertToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = new System.Text.StringBuilder();
        result.Append(char.ToLowerInvariant(input[0]));

        for (int i = 1; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]))
            {
                result.Append('_');
                result.Append(char.ToLowerInvariant(input[i]));
            }
            else
            {
                result.Append(input[i]);
            }
        }

        return result.ToString();
    }
}
