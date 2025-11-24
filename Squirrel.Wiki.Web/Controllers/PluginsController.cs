using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Services;
using Squirrel.Wiki.Web.Extensions;
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
    /// Save plugin configuration - Refactored with Result Pattern
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Configure(Guid id, [FromForm] Dictionary<string, string> configuration)
    {
        var result = await ConfigurePluginWithResult(id, configuration);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Configured plugin '{PluginName}' (ID: {PluginId})", 
                result.Value!.Name, result.Value.Id);
            
            NotifyLocalizedSuccess("Notification_PluginConfigured", result.Value.Name);
            return RedirectToAction(nameof(Details), new { id });
        }
        else
        {
            NotifyError(result.Error!);
            return RedirectToAction(nameof(Configure), new { id });
        }
    }

    /// <summary>
    /// Enable a plugin - Refactored with Result Pattern
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enable(Guid id)
    {
        var result = await EnablePluginWithResult(id);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Enabled plugin '{PluginName}' (ID: {PluginId})", 
                result.Value!.Name, result.Value.Id);
            
            NotifyLocalizedSuccess("Notification_PluginEnabled", result.Value.Name);
            return RedirectToAction(nameof(Index));
        }
        else
        {
            NotifyError(result.Error!);
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Disable a plugin - Refactored with Result Pattern
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(Guid id)
    {
        var result = await DisablePluginWithResult(id);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Disabled plugin '{PluginName}' (ID: {PluginId})", 
                result.Value!.Name, result.Value.Id);
            
            NotifyLocalizedSuccess("Notification_PluginDisabled", result.Value.Name);
            return RedirectToAction(nameof(Index));
        }
        else
        {
            NotifyError(result.Error!);
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Delete a plugin (non-core only) - Refactored with Result Pattern
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await DeletePluginWithResult(id);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Deleted plugin '{PluginName}' (ID: {PluginId})", 
                result.Value!, id);
            
            NotifyLocalizedSuccess("Notification_PluginDeleted", result.Value);
            return RedirectToAction(nameof(Index));
        }
        else
        {
            NotifyError(result.Error!);
            return RedirectToAction(nameof(Index));
        }
    }

    #region Helper Methods - Result Pattern

    /// <summary>
    /// Configures a plugin with Result Pattern
    /// Encapsulates plugin configuration logic with validation
    /// </summary>
    private async Task<Result<AuthenticationPlugin>> ConfigurePluginWithResult(Guid id, Dictionary<string, string> configuration)
    {
        try
        {
            var plugin = await _pluginService.GetPluginAsync(id);
            
            if (plugin == null)
            {
                return Result<AuthenticationPlugin>.Failure(
                    "Plugin not found",
                    "PLUGIN_NOT_FOUND"
                ).WithContext("PluginId", id);
            }

            // Remove empty values
            var cleanedConfig = configuration
                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            await _pluginService.UpdatePluginConfigurationAsync(id, cleanedConfig);
            
            // Get updated plugin
            var updatedPlugin = await _pluginService.GetPluginAsync(id);
            return Result<AuthenticationPlugin>.Success(updatedPlugin!);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validation error configuring plugin {PluginId}", id);
            return Result<AuthenticationPlugin>.Failure(ex.Message, "PLUGIN_CONFIGURATION_ERROR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring plugin {PluginId}", id);
            return Result<AuthenticationPlugin>.Failure(
                $"Failed to save configuration: {ex.Message}",
                "PLUGIN_CONFIGURATION_FAILED"
            );
        }
    }

    /// <summary>
    /// Enables a plugin with Result Pattern
    /// Encapsulates plugin enable logic with validation
    /// </summary>
    private async Task<Result<AuthenticationPlugin>> EnablePluginWithResult(Guid id)
    {
        try
        {
            var plugin = await _pluginService.GetPluginAsync(id);
            
            if (plugin == null)
            {
                return Result<AuthenticationPlugin>.Failure(
                    "Plugin not found",
                    "PLUGIN_NOT_FOUND"
                ).WithContext("PluginId", id);
            }

            await _pluginService.EnablePluginAsync(id);
            
            // Get updated plugin
            var updatedPlugin = await _pluginService.GetPluginAsync(id);
            return Result<AuthenticationPlugin>.Success(updatedPlugin!);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validation error enabling plugin {PluginId}", id);
            return Result<AuthenticationPlugin>.Failure(ex.Message, "PLUGIN_ENABLE_ERROR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling plugin {PluginId}", id);
            return Result<AuthenticationPlugin>.Failure(
                $"Failed to enable plugin: {ex.Message}",
                "PLUGIN_ENABLE_FAILED"
            );
        }
    }

    /// <summary>
    /// Disables a plugin with Result Pattern
    /// Encapsulates plugin disable logic with validation
    /// </summary>
    private async Task<Result<AuthenticationPlugin>> DisablePluginWithResult(Guid id)
    {
        try
        {
            var plugin = await _pluginService.GetPluginAsync(id);
            
            if (plugin == null)
            {
                return Result<AuthenticationPlugin>.Failure(
                    "Plugin not found",
                    "PLUGIN_NOT_FOUND"
                ).WithContext("PluginId", id);
            }

            await _pluginService.DisablePluginAsync(id);
            
            // Get updated plugin
            var updatedPlugin = await _pluginService.GetPluginAsync(id);
            return Result<AuthenticationPlugin>.Success(updatedPlugin!);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validation error disabling plugin {PluginId}", id);
            return Result<AuthenticationPlugin>.Failure(ex.Message, "PLUGIN_DISABLE_ERROR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling plugin {PluginId}", id);
            return Result<AuthenticationPlugin>.Failure(
                $"Failed to disable plugin: {ex.Message}",
                "PLUGIN_DISABLE_FAILED"
            );
        }
    }

    /// <summary>
    /// Deletes a plugin with Result Pattern
    /// Encapsulates plugin deletion logic with validation
    /// </summary>
    private async Task<Result<string>> DeletePluginWithResult(Guid id)
    {
        try
        {
            var plugin = await _pluginService.GetPluginAsync(id);
            
            if (plugin == null)
            {
                return Result<string>.Failure(
                    "Plugin not found",
                    "PLUGIN_NOT_FOUND"
                ).WithContext("PluginId", id);
            }

            if (plugin.IsCorePlugin)
            {
                return Result<string>.Failure(
                    "Cannot delete core plugins",
                    "PLUGIN_CORE_DELETE_FORBIDDEN"
                ).WithContext("PluginId", id)
                 .WithContext("PluginName", plugin.Name);
            }

            var pluginName = plugin.Name;
            await _pluginService.DeletePluginAsync(id);
            
            return Result<string>.Success(pluginName);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validation error deleting plugin {PluginId}", id);
            return Result<string>.Failure(ex.Message, "PLUGIN_DELETE_ERROR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting plugin {PluginId}", id);
            return Result<string>.Failure(
                $"Failed to delete plugin: {ex.Message}",
                "PLUGIN_DELETE_FAILED"
            );
        }
    }

    #endregion
}
