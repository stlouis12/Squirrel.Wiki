using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Services;
using Squirrel.Wiki.Web.Models;
using Squirrel.Wiki.Web.Services;

namespace Squirrel.Wiki.Web.Controllers;

/// <summary>
/// Controller for managing authentication plugins
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class PluginsController : BaseController
{
    private readonly IPluginService _pluginService;

    public PluginsController(
        IPluginService pluginService,
        ILogger<PluginsController> logger,
        INotificationService notifications)
        : base(logger, notifications)
    {
        _pluginService = pluginService;
    }

    /// <summary>
    /// Display list of all plugins
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return await ExecuteAsync(async () =>
        {
            var plugins = await _pluginService.GetAllPluginsAsync();
            
            var viewModel = new PluginListViewModel
            {
                Plugins = plugins.Select(p =>
                {
                    var loadedPlugin = _pluginService.GetLoadedPlugin(p.PluginId);
                    
                    return new PluginItemViewModel
                    {
                        Id = p.Id,
                        PluginId = p.PluginId,
                        Name = p.Name,
                        Description = loadedPlugin?.Metadata.Description ?? "No description available",
                        Version = loadedPlugin?.Metadata.Version ?? p.Version,
                        Author = loadedPlugin?.Metadata.Author ?? "Unknown",
                        IsEnabled = p.IsEnabled,
                        IsConfigured = p.IsConfigured,
                        IsCorePlugin = p.IsCorePlugin
                    };
                }).ToList()
            };

            return View(viewModel);
        },
        ex =>
        {
            NotifyError("Failed to load plugins: " + ex.Message);
            return View(new PluginListViewModel());
        });
    }

    /// <summary>
    /// Display plugin details
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        return await ExecuteAsync(async () =>
        {
            var plugin = await _pluginService.GetPluginAsync(id);
            
            if (!ValidateEntityExists(plugin, "Plugin"))
                return RedirectToAction(nameof(Index));

            var loadedPlugin = _pluginService.GetLoadedPlugin(plugin.PluginId);
            var currentConfig = await _pluginService.GetPluginConfigurationAsync(id);

            var viewModel = new PluginDetailsViewModel
            {
                Id = plugin.Id,
                PluginId = plugin.PluginId,
                Name = plugin.Name,
                Description = loadedPlugin?.Metadata.Description ?? "No description available",
                Version = loadedPlugin?.Metadata.Version ?? plugin.Version,
                Author = loadedPlugin?.Metadata.Author ?? "Unknown",
                IsEnabled = plugin.IsEnabled,
                IsConfigured = plugin.IsConfigured,
                IsCorePlugin = plugin.IsCorePlugin,
                ConfigurationSchema = loadedPlugin?.Metadata.Configuration.Select(c => new PluginConfigurationItemViewModel
                {
                    Key = c.Key,
                    DisplayName = c.DisplayName,
                    Description = c.Description,
                    IsRequired = c.IsRequired,
                    IsSecret = c.IsSecret,
                    DefaultValue = c.DefaultValue ?? string.Empty,
                    ValidationPattern = c.ValidationPattern ?? string.Empty,
                    ValidationMessage = c.ValidationErrorMessage ?? string.Empty
                }).ToList() ?? new List<PluginConfigurationItemViewModel>(),
                CurrentValues = currentConfig
            };

            // Mask secret values in the display
            foreach (var item in viewModel.ConfigurationSchema.Where(c => c.IsSecret))
            {
                if (viewModel.CurrentValues.ContainsKey(item.Key) && !string.IsNullOrEmpty(viewModel.CurrentValues[item.Key]))
                {
                    viewModel.CurrentValues[item.Key] = "••••••••";
                }
            }

            return View(viewModel);
        },
        "Failed to load plugin details.",
        $"Error loading plugin details for ID: {id}");
    }

    /// <summary>
    /// Display configuration form
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Configure(Guid id)
    {
        return await ExecuteAsync(async () =>
        {
            var plugin = await _pluginService.GetPluginAsync(id);
            
            if (!ValidateEntityExists(plugin, "Plugin"))
                return RedirectToAction(nameof(Index));

            var loadedPlugin = _pluginService.GetLoadedPlugin(plugin.PluginId);
            var currentConfig = await _pluginService.GetPluginConfigurationAsync(id);

            var viewModel = new PluginConfigureViewModel
            {
                Id = plugin.Id,
                PluginId = plugin.PluginId,
                Name = plugin.Name,
                Fields = loadedPlugin?.Metadata.Configuration.Select(c => new PluginConfigurationFieldViewModel
                {
                    Key = c.Key,
                    DisplayName = c.DisplayName,
                    Description = c.Description,
                    Value = currentConfig.ContainsKey(c.Key) ? currentConfig[c.Key] : (c.DefaultValue ?? string.Empty),
                    IsRequired = c.IsRequired,
                    IsSecret = c.IsSecret,
                    DefaultValue = c.DefaultValue ?? string.Empty,
                    ValidationPattern = c.ValidationPattern ?? string.Empty,
                    ValidationMessage = c.ValidationErrorMessage ?? string.Empty,
                    Placeholder = c.DefaultValue ?? string.Empty
                }).ToList() ?? new List<PluginConfigurationFieldViewModel>()
            };

            return View(viewModel);
        },
        "Failed to load configuration form.",
        $"Error loading configuration form for plugin ID: {id}");
    }

    /// <summary>
    /// Save plugin configuration
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Configure(Guid id, [FromForm] Dictionary<string, string> configuration)
    {
        return await ExecuteAsync(async () =>
        {
            var plugin = await _pluginService.GetPluginAsync(id);
            
            if (!ValidateEntityExists(plugin, "Plugin"))
                return RedirectToAction(nameof(Index));

            // Remove empty values
            var cleanedConfig = configuration
                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            await _pluginService.UpdatePluginConfigurationAsync(id, cleanedConfig);

            NotifyLocalizedSuccess("Notification_PluginConfigured", plugin.Name);
            return RedirectToAction(nameof(Details), new { id });
        },
        ex =>
        {
            NotifyError("Failed to save configuration: " + ex.Message);
            return RedirectToAction(nameof(Configure), new { id });
        });
    }

    /// <summary>
    /// Enable a plugin
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enable(Guid id)
    {
        return await ExecuteAsync(async () =>
        {
            var plugin = await _pluginService.GetPluginAsync(id);
            
            if (!ValidateEntityExists(plugin, "Plugin"))
                return RedirectToAction(nameof(Index));

            await _pluginService.EnablePluginAsync(id);

            NotifyLocalizedSuccess("Notification_PluginEnabled", plugin.Name);
            return RedirectToAction(nameof(Index));
        },
        "Failed to enable plugin.",
        $"Error enabling plugin ID: {id}");
    }

    /// <summary>
    /// Disable a plugin
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(Guid id)
    {
        return await ExecuteAsync(async () =>
        {
            var plugin = await _pluginService.GetPluginAsync(id);
            
            if (!ValidateEntityExists(plugin, "Plugin"))
                return RedirectToAction(nameof(Index));

            await _pluginService.DisablePluginAsync(id);

            NotifyLocalizedSuccess("Notification_PluginDisabled", plugin.Name);
            return RedirectToAction(nameof(Index));
        },
        "Failed to disable plugin.",
        $"Error disabling plugin ID: {id}");
    }

    /// <summary>
    /// Delete a plugin (non-core only)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        return await ExecuteAsync(async () =>
        {
            var plugin = await _pluginService.GetPluginAsync(id);
            
            if (!ValidateEntityExists(plugin, "Plugin"))
                return RedirectToAction(nameof(Index));

            if (plugin.IsCorePlugin)
            {
                NotifyError("Cannot delete core plugins");
                return RedirectToAction(nameof(Index));
            }

            var pluginName = plugin.Name;
            await _pluginService.DeletePluginAsync(id);

            NotifyLocalizedSuccess("Notification_PluginDeleted", pluginName);
            return RedirectToAction(nameof(Index));
        },
        "Failed to delete plugin.",
        $"Error deleting plugin ID: {id}");
    }
}
