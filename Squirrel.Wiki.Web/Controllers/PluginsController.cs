using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Services;
using Squirrel.Wiki.Web.Models;

namespace Squirrel.Wiki.Web.Controllers;

/// <summary>
/// Controller for managing authentication plugins
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class PluginsController : Controller
{
    private readonly IPluginService _pluginService;
    private readonly ILogger<PluginsController> _logger;

    public PluginsController(
        IPluginService pluginService,
        ILogger<PluginsController> logger)
    {
        _pluginService = pluginService;
        _logger = logger;
    }

    /// <summary>
    /// Display list of all plugins
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading plugins list");
            TempData["Error"] = "Failed to load plugins: " + ex.Message;
            return View(new PluginListViewModel());
        }
    }

    /// <summary>
    /// Display plugin details
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        try
        {
            var plugin = await _pluginService.GetPluginAsync(id);
            
            if (plugin == null)
            {
                TempData["Error"] = "Plugin not found";
                return RedirectToAction(nameof(Index));
            }

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading plugin details for ID: {PluginId}", id);
            TempData["Error"] = "Failed to load plugin details: " + ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Display configuration form
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Configure(Guid id)
    {
        try
        {
            var plugin = await _pluginService.GetPluginAsync(id);
            
            if (plugin == null)
            {
                TempData["Error"] = "Plugin not found";
                return RedirectToAction(nameof(Index));
            }

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration form for plugin ID: {PluginId}", id);
            TempData["Error"] = "Failed to load configuration form: " + ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Save plugin configuration
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Configure(Guid id, [FromForm] Dictionary<string, string> configuration)
    {
        try
        {
            var plugin = await _pluginService.GetPluginAsync(id);
            
            if (plugin == null)
            {
                TempData["Error"] = "Plugin not found";
                return RedirectToAction(nameof(Index));
            }

            // Remove empty values
            var cleanedConfig = configuration
                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            await _pluginService.UpdatePluginConfigurationAsync(id, cleanedConfig);

            TempData["Success"] = $"Configuration for '{plugin.Name}' saved successfully";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration for plugin ID: {PluginId}", id);
            TempData["Error"] = "Failed to save configuration: " + ex.Message;
            return RedirectToAction(nameof(Configure), new { id });
        }
    }

    /// <summary>
    /// Enable a plugin
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enable(Guid id)
    {
        try
        {
            var plugin = await _pluginService.GetPluginAsync(id);
            
            if (plugin == null)
            {
                TempData["Error"] = "Plugin not found";
                return RedirectToAction(nameof(Index));
            }

            await _pluginService.EnablePluginAsync(id);

            TempData["Success"] = $"Plugin '{plugin.Name}' enabled successfully";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling plugin ID: {PluginId}", id);
            TempData["Error"] = "Failed to enable plugin: " + ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Disable a plugin
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(Guid id)
    {
        try
        {
            var plugin = await _pluginService.GetPluginAsync(id);
            
            if (plugin == null)
            {
                TempData["Error"] = "Plugin not found";
                return RedirectToAction(nameof(Index));
            }

            await _pluginService.DisablePluginAsync(id);

            TempData["Success"] = $"Plugin '{plugin.Name}' disabled successfully";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling plugin ID: {PluginId}", id);
            TempData["Error"] = "Failed to disable plugin: " + ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Delete a plugin (non-core only)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var plugin = await _pluginService.GetPluginAsync(id);
            
            if (plugin == null)
            {
                TempData["Error"] = "Plugin not found";
                return RedirectToAction(nameof(Index));
            }

            if (plugin.IsCorePlugin)
            {
                TempData["Error"] = "Cannot delete core plugins";
                return RedirectToAction(nameof(Index));
            }

            var pluginName = plugin.Name;
            await _pluginService.DeletePluginAsync(id);

            TempData["Success"] = $"Plugin '{pluginName}' deleted successfully";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting plugin ID: {PluginId}", id);
            TempData["Error"] = "Failed to delete plugin: " + ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }
}
