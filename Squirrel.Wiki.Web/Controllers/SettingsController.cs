using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Squirrel.Wiki.Core.Database;
using Squirrel.Wiki.Core.Services;
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
    /// Display all settings grouped by category
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return await ExecuteAsync(async () =>
        {
            var model = new SettingsViewModel();

            // Get all settings from database
            var allSettings = await _settingsService.GetAllSettingsAsync();

            // Define setting groups with their configurations
            model.Groups = await GetSettingGroupsAsync(allSettings);

            return View(model);
        },
        _localizer["ErrorLoadingSettings"],
        "Error loading settings");
    }

    /// <summary>
    /// Edit a specific setting
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return NotFound();
        }

        return await ExecuteAsync(async () =>
        {
            var value = await _settingsService.GetSettingAsync<string>(key);
            var settingDef = await GetSettingDefinitionAsync(key);

            if (settingDef == null)
            {
                return NotFound();
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

            // For timezone dropdown, provide display names
            if (key == "TimeZone" && settingDef.Options != null)
            {
                ViewBag.TimezoneDisplayNames = settingDef.Options.ToDictionary(
                    id => id,
                    id => _timezoneService.GetTimezoneDisplayName(id)
                );
            }

            return View(model);
        },
        _localizer["ErrorLoadingSetting"],
        $"Error loading setting {key}");
    }

    /// <summary>
    /// Save a setting
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditSettingViewModel model)
    {
        if (!ValidateModelState())
        {
            return View(model);
        }

        return await ExecuteAsync(async () =>
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

            NotifyLocalizedSuccess("Notification_SettingUpdated", model.DisplayName);
            return RedirectToAction(nameof(Index));
        },
        ex =>
        {
            ModelState.AddModelError("", "Error saving setting. Please try again.");
            return View(model);
        });
    }

    /// <summary>
    /// Quick update a setting via AJAX
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickUpdate(string key, string value)
    {
        try
        {
            var settingDef = await GetSettingDefinitionAsync(key);
            if (settingDef == null)
            {
                return Json(new { success = false, message = "Setting not found" });
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

            return Json(new { success = true, message = "Setting updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error quick-updating setting {Key}", key);
            return Json(new { success = false, message = ex.Message });
        }
    }

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
                CreateSettingItem("SessionTimeoutMinutes",  _localizer["SessionTimeoutMinutes"], _localizer["SessionTimeoutMinutesDescription"], SettingType.Number, true, existingSettings, envInfo),
                CreateSettingItem("MaxLoginAttempts",  _localizer["MaxLoginAttempts"], _localizer["MaxLoginAttemptsDescription"], SettingType.Number, true, existingSettings, envInfo),
                CreateSettingItem("AccountLockDurationMinutes",  _localizer["AccountLockDurationMinutes"], _localizer["AccountLockDurationMinutesDescription"], SettingType.Number, true, existingSettings, envInfo)
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
                CreateSettingItem("MaxPageTitleLength",  _localizer["MaxPageTitleLength"], _localizer["MaxPageTitleLengthDescription"], SettingType.Number, true, existingSettings, envInfo),
                CreateSettingItem("EnablePageVersioning",  _localizer["EnablePageVersioning"], _localizer["EnablePageVersioningDescription"], SettingType.Boolean, false, existingSettings, envInfo)
            }
        });

        // Search Settings
        groups.Add(new SettingGroup
        {
            Name = _localizer["SearchSettings"],
            Description = _localizer["SearchAndIndexingSettings"],
            Icon = "bi-search",
            Settings = new List<SettingItem>
            {
                CreateSettingItem("SearchResultsPerPage",  _localizer["SearchResultsPerPage"], _localizer["SearchResultsPerPageDescription"], SettingType.Number, true, existingSettings, envInfo),
                CreateSettingItem("EnableFuzzySearch",  _localizer["EnableFuzzySearch"], _localizer["EnableFuzzySearchDescription"], SettingType.Boolean, false, existingSettings, envInfo),
                CreateSettingItem("SearchMinimumLength",  _localizer["SearchMinimumLength"], _localizer["SearchMinimumLengthDescription"], SettingType.Number, true, existingSettings, envInfo)
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
                CreateSettingItem("CacheDurationMinutes",  _localizer["CacheDurationMinutes"], _localizer["CacheDurationMinutesDescription"], SettingType.Number, true, existingSettings, envInfo)
            }
        });

        // Distributed Cache Settings
        var cacheSettings = new List<SettingItem>
        {
            CreateSettingItem("CacheEnabled",  _localizer["CacheEnabled"], _localizer["CacheEnabledDescription"], SettingType.Boolean, false, existingSettings, envInfo),
            CreateSettingItem("CacheExpirationMinutes",  _localizer["CacheExpirationMinutes"], _localizer["CacheExpirationMinutesDescription"], SettingType.Number, true, existingSettings, envInfo),
            CreateSettingItem("CacheProvider",  _localizer["CacheProvider"], _localizer["CacheProviderDescription"], SettingType.Dropdown, true, existingSettings, envInfo, new List<string> { "Memory", "Redis" })
        };

        // Only show Redis settings if provider is set to Redis
        existingSettings.TryGetValue("CacheProvider", out var cacheProvider);
        if (string.Equals(cacheProvider ?? "Memory", "Redis", StringComparison.OrdinalIgnoreCase))
        {
            cacheSettings.Add(CreateSettingItem("RedisConfiguration",  _localizer["RedisConfiguration"], _localizer["RedisConfigurationDescription"], SettingType.Text, false, existingSettings, envInfo));
            cacheSettings.Add(CreateSettingItem("RedisInstanceName",  _localizer["RedisInstanceName"], _localizer["RedisInstanceNameDescription"], SettingType.Text, false, existingSettings, envInfo));
        }

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
        List<string>? options = null)
    {
        existingSettings.TryGetValue(key, out var value);
        envInfo.TryGetValue(key, out var env);

        return new SettingItem
        {
            Key = key,
            DisplayName = displayName,
            Description = description,
            Value = value ?? GetDefaultValue(key, type),
            Type = type,
            IsRequired = isRequired,
            Options = options,
            IsFromEnvironment = env.IsFromEnvironment,
            EnvironmentVariableName = env.EnvironmentVariableName
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
