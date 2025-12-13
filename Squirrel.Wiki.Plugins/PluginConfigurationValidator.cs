using System.Text.RegularExpressions;
using Squirrel.Wiki.Contracts.Plugins;

namespace Squirrel.Wiki.Plugins;

/// <summary>
/// Validates plugin configuration values based on their schema
/// </summary>
public class PluginConfigurationValidator
{
    /// <summary>
    /// Validates a configuration dictionary against a plugin's configuration schema
    /// </summary>
    public static ValidationResult Validate(
        Dictionary<string, string> configuration,
        IEnumerable<PluginConfigurationItem> schema)
    {
        var result = new ValidationResult();

        foreach (var item in schema)
        {
            var hasValue = configuration.TryGetValue(item.Key, out var value);
            var isEmpty = string.IsNullOrWhiteSpace(value);

            // Check required fields
            if (item.IsRequired && (!hasValue || isEmpty))
            {
                result.AddError(item.Key, $"{item.DisplayName} is required");
                continue;
            }

            // Skip validation for empty optional fields
            if (isEmpty)
            {
                continue;
            }

            // Validate based on type
            switch (item.Type)
            {
                case PluginConfigType.Url:
                    ValidateUrl(item, value!, result);
                    break;

                case PluginConfigType.Number:
                    ValidateNumber(item, value!, result);
                    break;

                case PluginConfigType.Boolean:
                    ValidateBoolean(item, value!, result);
                    break;

                case PluginConfigType.Text:
                case PluginConfigType.Secret:
                case PluginConfigType.TextArea:
                    ValidateText(item, value!, result);
                    break;

                case PluginConfigType.Dropdown:
                    ValidateDropdown(item, value!, result);
                    break;
            }

            // Custom regex validation
            if (!string.IsNullOrEmpty(item.ValidationPattern))
            {
                ValidatePattern(item, value!, result);
            }
        }

        return result;
    }

    private static void ValidateUrl(PluginConfigurationItem item, string value, ValidationResult result)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            result.AddError(item.Key, $"{item.DisplayName} must be a valid URL");
            return;
        }

        // Ensure HTTPS for security
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        {
            result.AddError(item.Key, $"{item.DisplayName} must be an HTTP or HTTPS URL");
        }
    }

    private static void ValidateNumber(PluginConfigurationItem item, string value, ValidationResult result)
    {
        if (!decimal.TryParse(value, out var number))
        {
            result.AddError(item.Key, $"{item.DisplayName} must be a valid number");
            return;
        }

        // Additional range validation could be added here if needed
    }

    private static void ValidateBoolean(PluginConfigurationItem item, string value, ValidationResult result)
    {
        var lowerValue = value.ToLowerInvariant();
        if (lowerValue != "true" && lowerValue != "false" && 
            lowerValue != "1" && lowerValue != "0" &&
            lowerValue != "yes" && lowerValue != "no")
        {
            result.AddError(item.Key, $"{item.DisplayName} must be true or false");
        }
    }

    private static void ValidateText(PluginConfigurationItem item, string value, ValidationResult result)
    {
        // Basic text validation - could be extended with min/max length
        if (value.Length > 10000)
        {
            result.AddError(item.Key, $"{item.DisplayName} is too long (maximum 10000 characters)");
        }
    }

    private static void ValidateDropdown(PluginConfigurationItem item, string value, ValidationResult result)
    {
        if (item.DropdownOptions == null || item.DropdownOptions.Length == 0)
        {
            return;
        }

        if (!item.DropdownOptions.Contains(value))
        {
            result.AddError(item.Key, 
                $"{item.DisplayName} must be one of: {string.Join(", ", item.DropdownOptions)}");
        }
    }

    private static void ValidatePattern(PluginConfigurationItem item, string value, ValidationResult result)
    {
        try
        {
            var regex = new Regex(item.ValidationPattern!, RegexOptions.None, TimeSpan.FromSeconds(1));
            if (!regex.IsMatch(value))
            {
                var message = !string.IsNullOrEmpty(item.ValidationErrorMessage)
                    ? item.ValidationErrorMessage
                    : $"{item.DisplayName} format is invalid";
                result.AddError(item.Key, message);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            result.AddError(item.Key, $"{item.DisplayName} validation timed out");
        }
        catch (ArgumentException)
        {
            // Invalid regex pattern - log but don't fail validation
            result.AddWarning(item.Key, $"Invalid validation pattern for {item.DisplayName}");
        }
    }
}

/// <summary>
/// Result of configuration validation
/// </summary>
public class ValidationResult
{
    private readonly Dictionary<string, List<string>> _errors = new();
    private readonly Dictionary<string, List<string>> _warnings = new();

    public bool IsValid => _errors.Count == 0;

    public IReadOnlyDictionary<string, List<string>> Errors => _errors;
    public IReadOnlyDictionary<string, List<string>> Warnings => _warnings;

    public void AddError(string key, string message)
    {
        if (!_errors.ContainsKey(key))
        {
            _errors[key] = new List<string>();
        }
        _errors[key].Add(message);
    }

    public void AddWarning(string key, string message)
    {
        if (!_warnings.ContainsKey(key))
        {
            _warnings[key] = new List<string>();
        }
        _warnings[key].Add(message);
    }

    public string GetErrorMessage()
    {
        if (IsValid)
        {
            return string.Empty;
        }

        var messages = _errors.SelectMany(kvp => kvp.Value);
        return string.Join("; ", messages);
    }

    public Dictionary<string, string> GetFieldErrors()
    {
        return _errors.ToDictionary(
            kvp => kvp.Key,
            kvp => string.Join(", ", kvp.Value)
        );
    }
}
