using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Services;
using Squirrel.Wiki.Web.Models.Admin;

namespace Squirrel.Wiki.Web.Controllers;

/// <summary>
/// Controller for managing application settings
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class SettingsController : Controller
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        ISettingsService settingsService,
        ILogger<SettingsController> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// Display all settings grouped by category
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new SettingsViewModel();

        try
        {
            // Get all settings from database
            var allSettings = await _settingsService.GetAllSettingsAsync();

            // Define setting groups with their configurations
            model.Groups = GetSettingGroups(allSettings);

            if (TempData["SuccessMessage"] != null)
            {
                model.SuccessMessage = TempData["SuccessMessage"]?.ToString();
            }

            if (TempData["ErrorMessage"] != null)
            {
                model.ErrorMessage = TempData["ErrorMessage"]?.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings");
            model.ErrorMessage = "Error loading settings. Please try again.";
        }

        return View(model);
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

        try
        {
            var value = await _settingsService.GetSettingAsync<string>(key);
            var settingDef = GetSettingDefinition(key);

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

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading setting {Key}", key);
            TempData["ErrorMessage"] = "Error loading setting. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Save a setting
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditSettingViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

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

            TempData["SuccessMessage"] = $"Setting '{model.DisplayName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving setting {Key}", model.Key);
            ModelState.AddModelError("", "Error saving setting. Please try again.");
            return View(model);
        }
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
            var settingDef = GetSettingDefinition(key);
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

    private List<SettingGroup> GetSettingGroups(Dictionary<string, string> existingSettings)
    {
        var groups = new List<SettingGroup>();

        // General Settings
        groups.Add(new SettingGroup
        {
            Name = "General",
            Description = "Basic site configuration",
            Icon = "bi-gear",
            Settings = new List<SettingItem>
            {
                CreateSettingItem("SiteName", "Site Name", "The name of your wiki", SettingType.Text, true, existingSettings),
                CreateSettingItem("SiteDescription", "Site Description", "A brief description of your wiki", SettingType.TextArea, false, existingSettings),
                CreateSettingItem("SiteUrl", "Site URL", "The base URL of your wiki", SettingType.Url, true, existingSettings),
                CreateSettingItem("DefaultLanguage", "Default Language", "Default language for the wiki", SettingType.Dropdown, true, existingSettings, new List<string> { "en", "es", "fr", "de", "it" }),
                CreateSettingItem("TimeZone", "Time Zone", "Default time zone", SettingType.Text, true, existingSettings)
            }
        });

        // Security Settings
        groups.Add(new SettingGroup
        {
            Name = "Security",
            Description = "Security and authentication settings",
            Icon = "bi-shield-lock",
            Settings = new List<SettingItem>
            {
                CreateSettingItem("AllowAnonymousReading", "Allow Anonymous Reading", "Allow non-authenticated users to read pages", SettingType.Boolean, false, existingSettings),
                CreateSettingItem("RequireEmailVerification", "Require Email Verification", "Require users to verify their email", SettingType.Boolean, false, existingSettings),
                CreateSettingItem("SessionTimeoutMinutes", "Session Timeout (minutes)", "Session timeout in minutes", SettingType.Number, true, existingSettings),
                CreateSettingItem("MaxLoginAttempts", "Max Login Attempts", "Maximum failed login attempts before lockout", SettingType.Number, true, existingSettings)
            }
        });

        // Content Settings
        groups.Add(new SettingGroup
        {
            Name = "Content",
            Description = "Content and editing settings",
            Icon = "bi-file-text",
            Settings = new List<SettingItem>
            {
                CreateSettingItem("DefaultPageTemplate", "Default Page Template", "Default template for new pages", SettingType.TextArea, false, existingSettings),
                CreateSettingItem("EnableMarkdownExtensions", "Enable Markdown Extensions", "Enable extended Markdown features", SettingType.Boolean, false, existingSettings),
                CreateSettingItem("MaxPageTitleLength", "Max Page Title Length", "Maximum length for page titles", SettingType.Number, true, existingSettings),
                CreateSettingItem("EnablePageVersioning", "Enable Page Versioning", "Keep history of page changes", SettingType.Boolean, false, existingSettings)
            }
        });

        // Search Settings
        groups.Add(new SettingGroup
        {
            Name = "Search",
            Description = "Search and indexing settings",
            Icon = "bi-search",
            Settings = new List<SettingItem>
            {
                CreateSettingItem("SearchResultsPerPage", "Results Per Page", "Number of search results per page", SettingType.Number, true, existingSettings),
                CreateSettingItem("EnableFuzzySearch", "Enable Fuzzy Search", "Allow typo-tolerant searching", SettingType.Boolean, false, existingSettings),
                CreateSettingItem("SearchMinimumLength", "Minimum Search Length", "Minimum characters for search query", SettingType.Number, true, existingSettings)
            }
        });

        // Email Settings
        groups.Add(new SettingGroup
        {
            Name = "Email",
            Description = "Email notification settings",
            Icon = "bi-envelope",
            Settings = new List<SettingItem>
            {
                CreateSettingItem("SmtpHost", "SMTP Host", "SMTP server hostname", SettingType.Text, false, existingSettings),
                CreateSettingItem("SmtpPort", "SMTP Port", "SMTP server port", SettingType.Number, false, existingSettings),
                CreateSettingItem("SmtpUsername", "SMTP Username", "SMTP authentication username", SettingType.Text, false, existingSettings),
                CreateSettingItem("SmtpFromEmail", "From Email", "Email address for outgoing emails", SettingType.Email, false, existingSettings),
                CreateSettingItem("SmtpFromName", "From Name", "Display name for outgoing emails", SettingType.Text, false, existingSettings)
            }
        });

        // Performance Settings
        groups.Add(new SettingGroup
        {
            Name = "Performance",
            Description = "Caching and performance settings",
            Icon = "bi-speedometer2",
            Settings = new List<SettingItem>
            {
                CreateSettingItem("EnableCaching", "Enable Caching", "Enable response caching", SettingType.Boolean, false, existingSettings),
                CreateSettingItem("CacheDurationMinutes", "Cache Duration (minutes)", "Default cache duration", SettingType.Number, true, existingSettings),
                CreateSettingItem("EnableCompression", "Enable Compression", "Enable response compression", SettingType.Boolean, false, existingSettings)
            }
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
        List<string>? options = null)
    {
        existingSettings.TryGetValue(key, out var value);

        return new SettingItem
        {
            Key = key,
            DisplayName = displayName,
            Description = description,
            Value = value ?? GetDefaultValue(key, type),
            Type = type,
            IsRequired = isRequired,
            Options = options
        };
    }

    private SettingItem? GetSettingDefinition(string key)
    {
        var allGroups = GetSettingGroups(new Dictionary<string, string>());
        return allGroups
            .SelectMany(g => g.Settings)
            .FirstOrDefault(s => s.Key == key);
    }

    private string GetDefaultValue(string key, SettingType type)
    {
        return type switch
        {
            SettingType.Boolean => "false",
            SettingType.Number => key switch
            {
                "SessionTimeoutMinutes" => "480",
                "MaxLoginAttempts" => "5",
                "MaxPageTitleLength" => "200",
                "SearchResultsPerPage" => "20",
                "SearchMinimumLength" => "3",
                "SmtpPort" => "587",
                "CacheDurationMinutes" => "60",
                _ => "0"
            },
            _ => key switch
            {
                "SiteName" => "Squirrel Wiki",
                "SiteDescription" => "A modern wiki application",
                "DefaultLanguage" => "en",
                "TimeZone" => "UTC",
                _ => string.Empty
            }
        };
    }

    #endregion
}
