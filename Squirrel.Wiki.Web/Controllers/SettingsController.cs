using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
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
    private readonly SquirrelDbContext _dbContext;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ITimezoneService _timezoneService;

    public SettingsController(
        ISettingsService settingsService,
        SquirrelDbContext dbContext,
        ILogger<SettingsController> logger,
        IStringLocalizer<SharedResources> localizer,
        INotificationService notifications,
        ITimezoneService timezoneService)
        : base(logger, notifications)
    {
        _settingsService = settingsService;
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
        // Get environment info from database
        var envInfo = await _dbContext.SiteConfigurations
            .AsNoTracking()
            .Where(s => s.IsFromEnvironment)
            .ToDictionaryAsync(s => s.Key, s => (s.IsFromEnvironment, s.EnvironmentVariableName));

        var groups = new List<SettingGroup>();

        // General Settings
        groups.Add(new SettingGroup
        {
            Name = _localizer["General"],
            Description = _localizer["BasicSiteConfiguration"],
            Icon = "bi-gear",
            Settings = new List<SettingItem>
            {
                CreateSettingItem("SiteName", _localizer["SiteName"], "The name of your wiki", SettingType.Text, true, existingSettings, envInfo),
                CreateSettingItem("SiteUrl", _localizer["SiteURL"], _localizer["SiteURLDescription"], SettingType.Url, true, existingSettings, envInfo),
                CreateSettingItem("DefaultLanguage", _localizer["DefaultLanguage"], _localizer["DefaultLanguageDescription"], SettingType.Dropdown, true, existingSettings, envInfo, new List<string> { "en", "es", "fr", "de", "it" }),
                CreateSettingItem("TimeZone", _localizer["TimeZone"], _localizer["TimeZoneDescription"], SettingType.Dropdown, true, existingSettings, envInfo, _timezoneService.GetAvailableTimezoneIds().ToList())
            }
        });

        // Security Settings
        groups.Add(new SettingGroup
        {
            Name = _localizer["Security"],
            Description = _localizer["SecurityAndAuthenticationSettings"],
            Icon = "bi-shield-lock",
            Settings = new List<SettingItem>
            {
                CreateSettingItem("AllowAnonymousReading", _localizer["AllowAnonymousReading"], _localizer["AllowAnonymousReadingDescription"], SettingType.Boolean, false, existingSettings, envInfo),
                CreateSettingItem("SessionTimeoutMinutes",  _localizer["SessionTimeoutMinutes"], _localizer["SessionTimeoutMinutesDescription"], SettingType.Number, true, existingSettings, envInfo, minValue: 30, maxValue: 20160),
                CreateSettingItem("MaxLoginAttempts",  _localizer["MaxLoginAttempts"], _localizer["MaxLoginAttemptsDescription"], SettingType.Number, true, existingSettings, envInfo, minValue: 3, maxValue: 10),
                CreateSettingItem("AccountLockDurationMinutes",  _localizer["AccountLockDurationMinutes"], _localizer["AccountLockDurationMinutesDescription"], SettingType.Number, true, existingSettings, envInfo, minValue: 5, maxValue: 60)
            }
        });

        // Content Settings
        groups.Add(new SettingGroup
        {
            Name = _localizer["ContentSettings"],
            Description = _localizer["ContentAndEditingSettings"],
            Icon = "bi-file-text",
            Settings = new List<SettingItem>
            {
                CreateSettingItem("DefaultPageTemplate",  _localizer["DefaultPageTemplate"], _localizer["DefaultPageTemplateDescription"], SettingType.TextArea, false, existingSettings, envInfo),
                CreateSettingItem("MaxPageTitleLength",  _localizer["MaxPageTitleLength"], _localizer["MaxPageTitleLengthDescription"], SettingType.Number, true, existingSettings, envInfo, minValue: 25, maxValue: 200),
                CreateSettingItem("EnablePageVersioning",  _localizer["EnablePageVersioning"], _localizer["EnablePageVersioningDescription"], SettingType.Boolean, false, existingSettings, envInfo)
            }
        });

        // Performance Settings
        groups.Add(new SettingGroup
        {
            Name = _localizer["Performance"],
            Description = _localizer["CachingAndPerformanceSettings"],
            Icon = "bi-speedometer2",
            Settings = new List<SettingItem>
            {
                CreateSettingItem("EnableCaching",  _localizer["EnableCaching"], _localizer["EnableCachingDescription"], SettingType.Boolean, false, existingSettings, envInfo),
                CreateSettingItem("CacheDurationMinutes",  _localizer["CacheDurationMinutes"], _localizer["CacheDurationMinutesDescription"], SettingType.Number, true, existingSettings, envInfo, minValue: 5, maxValue: 120)
            }
        });

        // Distributed Cache Settings
        var cacheSettings = new List<SettingItem>
        {
            CreateSettingItem("CacheEnabled",  _localizer["CacheEnabled"], _localizer["CacheEnabledDescription"], SettingType.Boolean, false, existingSettings, envInfo),
            CreateSettingItem("CacheExpirationMinutes",  _localizer["CacheExpirationMinutes"], _localizer["CacheExpirationMinutesDescription"], SettingType.Number, true, existingSettings, envInfo),
            CreateSettingItem("CacheProvider",  _localizer["CacheProvider"], _localizer["CacheProviderDescription"], SettingType.Dropdown, true, existingSettings, envInfo, new List<string> { "Memory", "Redis" })
        };

        // Check if Redis is selected
        existingSettings.TryGetValue("CacheProvider", out var cacheProviderRaw);
        
        // Deserialize the value since it's stored as JSON
        string? cacheProvider = cacheProviderRaw;
        if (!string.IsNullOrEmpty(cacheProviderRaw))
        {
            try
            {
                cacheProvider = System.Text.Json.JsonSerializer.Deserialize<string>(cacheProviderRaw);
            }
            catch
            {
                // If deserialization fails, use the raw value
                cacheProvider = cacheProviderRaw;
            }
        }
        
        bool isRedisEnabled = string.Equals(cacheProvider ?? "Memory", "Redis", StringComparison.OrdinalIgnoreCase);
        
        // Always show Redis settings, but disable them if Redis is not selected
        var redisConfig = CreateSettingItem("RedisConfiguration",  _localizer["RedisConfiguration"], _localizer["RedisConfigurationDescription"], SettingType.Text, false, existingSettings, envInfo);
        var redisInstance = CreateSettingItem("RedisInstanceName",  _localizer["RedisInstanceName"], _localizer["RedisInstanceNameDescription"], SettingType.Text, false, existingSettings, envInfo);
        
        if (!isRedisEnabled)
        {
            redisConfig.IsDisabled = true;
            redisConfig.DisabledReason = "Redis must be selected as the Cache Provider to configure these settings";
            redisInstance.IsDisabled = true;
            redisInstance.DisabledReason = "Redis must be selected as the Cache Provider to configure these settings";
        }
        
        cacheSettings.Add(redisConfig);
        cacheSettings.Add(redisInstance);

        groups.Add(new SettingGroup
        {
            Name = _localizer["DistributedCache"],
            Description = _localizer["DistributedCacheSettings"],
            Icon = "bi-hdd-network",
            Settings = cacheSettings
        });

        return groups;
    }


    private SettingItem CreateSettingItem(
        string key,
        string displayName,
        string description,
        SettingType type,
        bool isRequired,
        Dictionary<string, string> existingSettings,
        Dictionary<string, (bool IsFromEnvironment, string? EnvironmentVariableName)> envInfo,
        List<string>? options = null,
        int? minValue = null,
        int? maxValue = null)
    {
        existingSettings.TryGetValue(key, out var value);
        envInfo.TryGetValue(key, out var env);

        // Deserialize JSON value if present
        string displayValue = value ?? GetDefaultValue(key, type);
        if (!string.IsNullOrEmpty(value))
        {
            try
            {
                // Try to deserialize as JSON (values are stored as JSON in the database)
                displayValue = System.Text.Json.JsonSerializer.Deserialize<string>(value) ?? value;
            }
            catch
            {
                // If deserialization fails, use the raw value
                displayValue = value;
            }
        }

        return new SettingItem
        {
            Key = key,
            DisplayName = displayName,
            Description = description,
            Value = displayValue,
            Type = type,
            IsRequired = isRequired,
            Options = options,
            IsFromEnvironment = env.IsFromEnvironment,
            EnvironmentVariableName = env.EnvironmentVariableName,
            MinValue = minValue,
            MaxValue = maxValue
        };
    }


    private async Task<SettingItem?> GetSettingDefinitionAsync(string key)
    {
        var allGroups = await GetSettingGroupsAsync(new Dictionary<string, string>());
        return allGroups
            .SelectMany(g => g.Settings)
            .FirstOrDefault(s => s.Key == key);
    }

    private string GetDefaultValue(string key, SettingType type)
    {
        return type switch
        {
            SettingType.Boolean => key switch
            {
                "CacheEnabled" => "true",
                "EnableCaching" => "true",
                _ => "false"
            },
            SettingType.Number => key switch
            {
                "SessionTimeoutMinutes" => "480",
                "MaxLoginAttempts" => "5",
                "AccountLockDurationMinutes" => "30",
                "MaxPageTitleLength" => "200",
                "SearchResultsPerPage" => "20",
                "SearchMinimumLength" => "3",
                "CacheDurationMinutes" => "60",
                "CacheExpirationMinutes" => "30",
                _ => "0"
            },
            _ => key switch
            {
                "SiteName" => "Squirrel Wiki",
                "DefaultLanguage" => "en",
                "TimeZone" => "UTC",
                "CacheProvider" => "Memory",
                "RedisConfiguration" => "localhost:6379",
                "RedisInstanceName" => "Squirrel_",
                _ => string.Empty
            }
        };
    }

    #endregion
}
