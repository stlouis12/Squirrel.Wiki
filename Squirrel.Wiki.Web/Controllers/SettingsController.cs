using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Database;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Services.Configuration;
using Squirrel.Wiki.Web.Models.Admin;
using Squirrel.Wiki.Web.Resources;
using Squirrel.Wiki.Web.Services;

namespace Squirrel.Wiki.Web.Controllers;

/// <summary>
/// Controller for managing application settings
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class SettingsController : BaseController
{
    private readonly ISettingsService _settingsService;
    private readonly IConfigurationService _configurationService;
    private readonly SquirrelDbContext _dbContext;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ITimezoneService _timezoneService;

    public SettingsController(
        ISettingsService settingsService,
        IConfigurationService configurationService,
        SquirrelDbContext dbContext,
        ILogger<SettingsController> logger,
        IStringLocalizer<SharedResources> localizer,
        INotificationService notifications,
        ITimezoneService timezoneService)
        : base(logger, notifications)
    {
        _settingsService = settingsService;
        _configurationService = configurationService;
        _dbContext = dbContext;
        _localizer = localizer;
        _timezoneService = timezoneService;
    }

    /// <summary>
    /// Display all settings grouped by category - Refactored with Result Pattern
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await LoadSettingsWithResult();

        return result.Match<IActionResult>(
            onSuccess: model => View(model),
            onFailure: (error, code) =>
            {
                _logger.LogError("Failed to load settings: {Error} (Code: {Code})", error, code);
                NotifyError(_localizer["ErrorLoadingSettings"]);
                return View(new SettingsViewModel());
            }
        );
    }

    /// <summary>
    /// Edit a specific setting - Refactored with Result Pattern
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return NotFound();
        }

        var result = await LoadSettingForEditWithResult(key);

        return result.Match<IActionResult>(
            onSuccess: model =>
            {
                // For timezone dropdown, provide display names
                if (key == "TimeZone" && model.Options != null)
                {
                    ViewBag.TimezoneDisplayNames = model.Options.ToDictionary(
                        id => id,
                        id => _timezoneService.GetTimezoneDisplayName(id)
                    );
                }

                return View(model);
            },
            onFailure: (error, code) =>
            {
                _logger.LogError("Failed to load setting {Key}: {Error} (Code: {Code})", key, error, code);
                
                if (code == "SETTING_NOT_FOUND")
                {
                    return NotFound();
                }

                NotifyError(_localizer["ErrorLoadingSetting"]);
                return RedirectToAction(nameof(Index));
            }
        );
    }

    /// <summary>
    /// Save a setting - Refactored with Result Pattern
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditSettingViewModel model)
    {
        if (!ValidateModelState())
        {
            return View(model);
        }

        var result = await SaveSettingWithResult(model);

        return result.Match<IActionResult>(
            onSuccess: displayName =>
            {
                NotifyLocalizedSuccess("Notification_SettingUpdated", displayName);
                return RedirectToAction(nameof(Index));
            },
            onFailure: (error, code) =>
            {
                _logger.LogError("Failed to save setting {Key}: {Error} (Code: {Code})", model.Key, error, code);
                ModelState.AddModelError("", error);
                return View(model);
            }
        );
    }

    /// <summary>
    /// Quick update a setting via AJAX - Refactored with Result Pattern
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickUpdate(string key, string value)
    {
        var result = await QuickUpdateSettingWithResult(key, value);

        return result.Match<IActionResult>(
            onSuccess: _ => Json(new { success = true, message = "Setting updated successfully" }),
            onFailure: (error, code) =>
            {
                _logger.LogError("Failed to quick-update setting {Key}: {Error} (Code: {Code})", key, error, code);
                return Json(new { success = false, message = error });
            }
        );
    }

    #region Helper Methods - Result Pattern

    /// <summary>
    /// Loads all settings with Result Pattern
    /// Encapsulates settings loading and grouping logic
    /// </summary>
    private async Task<Result<SettingsViewModel>> LoadSettingsWithResult()
    {
        try
        {
            var model = new SettingsViewModel();

            // Get all settings from database
            var allSettings = await _settingsService.GetAllSettingsAsync();

            // Define setting groups with their configurations
            model.Groups = await GetSettingGroupsAsync(allSettings);

            // Populate timezone display names for the dropdown
            var timezoneIds = _timezoneService.GetAvailableTimezoneIds();
            model.TimezoneDisplayNames = timezoneIds.ToDictionary(
                id => id,
                id => _timezoneService.GetTimezoneDisplayName(id)
            );

            return Result<SettingsViewModel>.Success(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings");
            return Result<SettingsViewModel>.Failure(
                "An error occurred while loading settings",
                "SETTINGS_LOAD_ERROR"
            );
        }
    }

    /// <summary>
    /// Loads a setting for editing with Result Pattern
    /// </summary>
    private async Task<Result<EditSettingViewModel>> LoadSettingForEditWithResult(string key)
    {
        try
        {
            var value = await _settingsService.GetSettingAsync<string>(key);
            var settingDef = await GetSettingDefinitionAsync(key);

            if (settingDef == null)
            {
                return Result<EditSettingViewModel>.Failure(
                    $"Setting '{key}' not found",
                    "SETTING_NOT_FOUND"
                ).WithContext("Key", key);
            }

            var model = new EditSettingViewModel
            {
                Key = key,
                DisplayName = settingDef.DisplayName,
                Description = settingDef.Description,
                Value = value ?? string.Empty,
                Type = settingDef.Type,
                IsRequired = settingDef.IsRequired,
                ValidationPattern = settingDef.ValidationPattern,
                Options = settingDef.Options
            };

            return Result<EditSettingViewModel>.Success(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading setting {Key} for edit", key);
            return Result<EditSettingViewModel>.Failure(
                "An error occurred while loading the setting",
                "SETTING_LOAD_ERROR"
            ).WithContext("Key", key);
        }
    }

    /// <summary>
    /// Saves a setting with Result Pattern
    /// Encapsulates value conversion and saving logic
    /// </summary>
    private async Task<Result<string>> SaveSettingWithResult(EditSettingViewModel model)
    {
        try
        {
            // Convert value based on type
            object valueToSave = model.Type switch
            {
                SettingType.Boolean => bool.Parse(model.Value),
                SettingType.Number => int.Parse(model.Value),
                _ => model.Value
            };

            await _settingsService.SaveSettingAsync(model.Key, valueToSave);

            _logger.LogInformation("Setting {Key} updated by {User}", model.Key, User.Identity?.Name);

            return Result<string>.Success(model.DisplayName);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Invalid format for setting {Key}", model.Key);
            return Result<string>.Failure(
                $"Invalid value format for {model.Type} setting",
                "SETTING_INVALID_FORMAT"
            ).WithContext("Key", model.Key)
             .WithContext("Type", model.Type.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving setting {Key}", model.Key);
            return Result<string>.Failure(
                "Error saving setting. Please try again.",
                "SETTING_SAVE_ERROR"
            ).WithContext("Key", model.Key);
        }
    }

    /// <summary>
    /// Quick updates a setting with Result Pattern
    /// Used for AJAX updates
    /// </summary>
    private async Task<Result<bool>> QuickUpdateSettingWithResult(string key, string value)
    {
        try
        {
            var settingDef = await GetSettingDefinitionAsync(key);
            if (settingDef == null)
            {
                return Result<bool>.Failure(
                    "Setting not found",
                    "SETTING_NOT_FOUND"
                ).WithContext("Key", key);
            }

            // Convert value based on type
            object valueToSave = settingDef.Type switch
            {
                SettingType.Boolean => bool.Parse(value),
                SettingType.Number => int.Parse(value),
                _ => value
            };

            await _settingsService.SaveSettingAsync(key, valueToSave);

            _logger.LogInformation("Setting {Key} quick-updated by {User}", key, User.Identity?.Name);

            return Result<bool>.Success(true);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Invalid format for quick-update of setting {Key}", key);
            return Result<bool>.Failure(
                "Invalid value format",
                "SETTING_INVALID_FORMAT"
            ).WithContext("Key", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error quick-updating setting {Key}", key);
            return Result<bool>.Failure(
                ex.Message,
                "SETTING_QUICK_UPDATE_ERROR"
            ).WithContext("Key", key);
        }
    }

    #endregion

    #region Private Helper Methods

    private async Task<List<SettingGroup>> GetSettingGroupsAsync(Dictionary<string, string> existingSettings)
    {
        // Get all configuration metadata
        var allMetadata = _configurationService.GetAllMetadata();
        
        // Build environment info by checking the actual source of each setting
        var envInfo = new Dictionary<string, (bool IsFromEnvironment, string? EnvironmentVariableName)>();
        foreach (var metadata in allMetadata)
        {
            var source = _configurationService.GetSource(metadata.Key);
            var isFromEnv = source == Squirrel.Wiki.Contracts.Configuration.ConfigurationSource.EnvironmentVariable;
            envInfo[metadata.Key] = (isFromEnv, isFromEnv ? metadata.EnvironmentVariable : null);
        }

        // Group by category
        var categoryGroups = allMetadata
            .GroupBy(m => m.Category)
            .OrderBy(g => GetCategoryOrder(g.Key));

        var groups = new List<SettingGroup>();

        foreach (var categoryGroup in categoryGroups)
        {
            var category = categoryGroup.Key;
            var settings = new List<SettingItem>();

            foreach (var metadata in categoryGroup.OrderBy(m => m.DisplayName))
            {
                var settingItem = CreateSettingItemFromMetadata(metadata, existingSettings, envInfo);
                settings.Add(settingItem);
            }

            // Handle special case for Redis settings - disable if Redis not selected
            if (category == "Performance")
            {
                existingSettings.TryGetValue("SQUIRREL_CACHE_PROVIDER", out var cacheProviderRaw);
                string? cacheProvider = DeserializeValue(cacheProviderRaw);
                bool isRedisEnabled = string.Equals(cacheProvider ?? "Memory", "Redis", StringComparison.OrdinalIgnoreCase);

                if (!isRedisEnabled)
                {
                    foreach (var setting in settings.Where(s => s.Key.Contains("REDIS")))
                    {
                        setting.IsDisabled = true;
                        setting.DisabledReason = "Redis must be selected as the Cache Provider to configure these settings";
                    }
                }
            }

            groups.Add(new SettingGroup
            {
                Name = _localizer[category],
                Description = _localizer[$"{category}Settings"],
                Icon = GetCategoryIcon(category),
                Settings = settings
            });
        }

        return groups;
    }

    private int GetCategoryOrder(string category)
    {
        return category switch
        {
            "General" => 1,
            "Security" => 2,
            "Content" => 3,
            "Search" => 4,
            "Performance" => 5,
            _ => 99
        };
    }

    private string GetCategoryIcon(string category)
    {
        return category switch
        {
            "General" => "bi-gear",
            "Security" => "bi-shield-lock",
            "Content" => "bi-file-text",
            "Search" => "bi-search",
            "Performance" => "bi-hdd-network",
            _ => "bi-gear"
        };
    }

    private SettingItem CreateSettingItemFromMetadata(
        ConfigurationProperty metadata,
        Dictionary<string, string> existingSettings,
        Dictionary<string, (bool IsFromEnvironment, string? EnvironmentVariableName)> envInfo)
    {
        existingSettings.TryGetValue(metadata.Key, out var value);
        envInfo.TryGetValue(metadata.Key, out var env);

        // Get the display value
        string displayValue = value ?? metadata.DefaultValue?.ToString() ?? string.Empty;
        if (!string.IsNullOrEmpty(value))
        {
            displayValue = DeserializeValue(value) ?? value;
        }

        // Determine the setting type
        var settingType = GetSettingType(metadata.ValueType, metadata.Validation, metadata.Key);

        // Get options for dropdown/enum types
        List<string>? options = null;
        if (metadata.Validation?.AllowedValues != null && metadata.Validation.AllowedValues.Length > 0)
        {
            options = metadata.Validation.AllowedValues.ToList();
        }
        else if (metadata.Key == "SQUIRREL_TIMEZONE")
        {
            options = _timezoneService.GetAvailableTimezoneIds().ToList();
        }

        return new SettingItem
        {
            Key = metadata.Key,
            DisplayName = metadata.DisplayName,
            Description = metadata.Description,
            Value = displayValue,
            Type = settingType,
            IsRequired = metadata.Validation != null,
            Options = options,
            IsFromEnvironment = env.IsFromEnvironment,
            EnvironmentVariableName = env.EnvironmentVariableName,
            MinValue = metadata.Validation?.MinValue,
            MaxValue = metadata.Validation?.MaxValue
        };
    }

    private SettingType GetSettingType(Type valueType, ValidationRules? validation, string? key = null)
    {
        if (valueType == typeof(bool))
            return SettingType.Boolean;

        if (valueType == typeof(int) || valueType == typeof(long) || valueType == typeof(double))
            return SettingType.Number;

        if (validation?.AllowedValues != null && validation.AllowedValues.Length > 0)
            return SettingType.Dropdown;

        // Special case: timezone setting should be a dropdown
        if (key == "SQUIRREL_TIMEZONE")
            return SettingType.Dropdown;

        if (validation?.MustBeUrl == true)
            return SettingType.Url;

        // Check for multi-line text (template fields)
        if (valueType == typeof(string) && (validation == null || string.IsNullOrEmpty(validation.RegexPattern)))
        {
            // Heuristic: if it's a template or has "template" in the key, use TextArea
            return key?.Contains("TEMPLATE", StringComparison.OrdinalIgnoreCase) == true
                ? SettingType.TextArea
                : SettingType.Text;
        }

        return SettingType.Text;
    }

    private string? DeserializeValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<string>(value);
        }
        catch
        {
            return value;
        }
    }

    private async Task<SettingItem?> GetSettingDefinitionAsync(string key)
    {
        var allGroups = await GetSettingGroupsAsync(new Dictionary<string, string>());
        return allGroups
            .SelectMany(g => g.Settings)
            .FirstOrDefault(s => s.Key == key);
    }

    #endregion
}
