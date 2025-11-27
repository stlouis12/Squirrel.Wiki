using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Contracts.Plugins;
using Squirrel.Wiki.Core.Configuration;
using Squirrel.Wiki.Core.Database;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Events;
using Squirrel.Wiki.Core.Exceptions;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Services.Caching;
using Squirrel.Wiki.Plugins;

namespace Squirrel.Wiki.Core.Services.Plugins;

/// <summary>
/// Service for managing plugins
/// </summary>
public class PluginService : BaseService, IPluginService
{
    private readonly SquirrelDbContext _context;
    private readonly IPluginLoader _pluginLoader;
    private readonly ISecretEncryptionService _encryptionService;
    private readonly IPluginAuditService _auditService;
    private readonly IUserContext _userContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly PluginConfigurationValidator _validator;
    private readonly string _pluginsPath;
    private bool _initialized = false;

    public PluginService(
        SquirrelDbContext context,
        IPluginLoader pluginLoader,
        ILogger<PluginService> logger,
        ICacheService cache,
        IEventPublisher eventPublisher,
        ISecretEncryptionService encryptionService,
        IPluginAuditService auditService,
        IUserContext userContext,
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        string pluginsPath,
        IConfigurationService configuration)
        : base(logger, cache, eventPublisher, null, configuration)
    {
        _context = context;
        _pluginLoader = pluginLoader;
        _encryptionService = encryptionService;
        _auditService = auditService;
        _userContext = userContext;
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
        _validator = new PluginConfigurationValidator();
        _pluginsPath = pluginsPath;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            LogWarning("Plugin service already initialized");
            return;
        }

        LogInfo("Initializing plugin service from path: {PluginsPath}", _pluginsPath);

        // Load all plugins from the plugins directory
        await _pluginLoader.LoadPluginsAsync(_pluginsPath, cancellationToken);

        var loadedPlugins = _pluginLoader.GetLoadedPlugins();
        LogInfo("Loaded {Count} plugins", loadedPlugins.Count());

        // Register or update plugins in database
        foreach (var plugin in loadedPlugins)
        {
            var existingPlugin = await _context.Plugins
                .FirstOrDefaultAsync(p => p.PluginId == plugin.Metadata.Id, cancellationToken);

            if (existingPlugin == null)
            {
                // Determine plugin type based on interfaces
                string pluginType = "Unknown";
                if (plugin is IAuthenticationPlugin)
                {
                    pluginType = PluginType.Authentication.ToString();
                }
                else if (plugin is ISearchPlugin)
                {
                    pluginType = PluginType.SearchProvider.ToString();
                }

                // Check if plugin has any required configuration fields
                var hasRequiredFields = plugin.GetConfigurationSchema()
                    .Any(c => c.IsRequired);

                var newPlugin = new Plugin
                {
                    Id = Guid.NewGuid(),
                    PluginId = plugin.Metadata.Id,
                    Name = plugin.Metadata.Name,
                    Version = plugin.Metadata.Version,
                    PluginType = pluginType,
                    IsEnabled = false,
                    IsConfigured = !hasRequiredFields, // Auto-configure if no required fields
                    LoadOrder = 0,
                    IsCorePlugin = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Plugins.Add(newPlugin);
                LogInfo("Registered new plugin: {PluginId} ({PluginType}), IsConfigured: {IsConfigured}", 
                    plugin.Metadata.Id, pluginType, newPlugin.IsConfigured);
            }
            else if (existingPlugin.Version != plugin.Metadata.Version)
            {
                LogInfo(
                    "Updating plugin version: {PluginId} from {OldVersion} to {NewVersion}",
                    plugin.Metadata.Id,
                    existingPlugin.Version,
                    plugin.Metadata.Version);

                existingPlugin.Version = plugin.Metadata.Version;
                existingPlugin.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Initialize all enabled plugins
        var enabledPlugins = await _context.Plugins
            .Where(p => p.IsEnabled && p.IsConfigured)
            .ToListAsync(cancellationToken);

        foreach (var dbPlugin in enabledPlugins)
        {
            var loadedPlugin = GetLoadedPlugin<IPlugin>(dbPlugin.PluginId);
            if (loadedPlugin != null)
            {
                try
                {
                    LogInfo("Initializing enabled plugin: {PluginId}", dbPlugin.PluginId);
                    await loadedPlugin.InitializeAsync(_serviceProvider, cancellationToken);
                    LogInfo("Plugin initialized successfully: {PluginId}", dbPlugin.PluginId);
                }
                catch (Exception ex)
                {
                    LogError(ex, "Failed to initialize enabled plugin: {PluginId}", dbPlugin.PluginId);
                    // Don't throw - continue initializing other plugins
                }
            }
            else
            {
                LogWarning("Enabled plugin not loaded: {PluginId}", dbPlugin.PluginId);
            }
        }

        _initialized = true;
        LogInfo("Plugin service initialized successfully");
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Plugin>> GetAllPluginsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Plugins
            .Include(p => p.Settings)
            .OrderBy(p => p.LoadOrder)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Plugin?> GetPluginAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Plugins
            .Include(p => p.Settings)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Plugin?> GetPluginByPluginIdAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Plugins
            .Include(p => p.Settings)
            .FirstOrDefaultAsync(p => p.PluginId == pluginId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Plugin>> GetEnabledPluginsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Plugins
            .Include(p => p.Settings)
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.LoadOrder)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Plugin> RegisterPluginAsync(
        string pluginId,
        string name,
        string version,
        string pluginType,
        bool isCorePlugin = false,
        CancellationToken cancellationToken = default)
    {
        var existingPlugin = await _context.Plugins
            .FirstOrDefaultAsync(p => p.PluginId == pluginId, cancellationToken);

        if (existingPlugin != null)
        {
            throw new BusinessRuleException(
                $"Plugin with ID '{pluginId}' already exists.",
                "PLUGIN_ALREADY_EXISTS"
            ).WithContext("PluginId", pluginId);
        }

        var plugin = new Plugin
        {
            Id = Guid.NewGuid(),
            PluginId = pluginId,
            Name = name,
            Version = version,
            PluginType = pluginType,
            IsEnabled = false,
            IsConfigured = false,
            LoadOrder = 0,
            IsCorePlugin = isCorePlugin,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Plugins.Add(plugin);
        await _context.SaveChangesAsync(cancellationToken);

        LogInfo("Registered plugin: {PluginId} ({PluginType})", pluginId, pluginType);

        // Audit log
        await LogAuditAsync(
            plugin.Id,
            pluginId,
            name,
            PluginOperation.Register,
            true,
            $"Registered {pluginType} plugin version {version}",
            cancellationToken);

        return plugin;
    }

    /// <inheritdoc/>
    public async Task EnablePluginAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plugin = await GetPluginAsync(id, cancellationToken);
        
        if (plugin == null)
        {
            throw new EntityNotFoundException("Plugin", id);
        }

        if (!plugin.IsConfigured)
        {
            throw new BusinessRuleException(
                $"Plugin '{plugin.Name}' must be configured before enabling.",
                "PLUGIN_NOT_CONFIGURED"
            ).WithContext("PluginId", plugin.PluginId)
             .WithContext("PluginName", plugin.Name);
        }

        // Get the loaded plugin instance
        var loadedPlugin = GetLoadedPlugin<IPlugin>(plugin.PluginId);
        if (loadedPlugin == null)
        {
            throw new ConfigurationException(
                $"Plugin '{plugin.Name}' is not loaded.",
                "PLUGIN_NOT_LOADED"
            ).WithContext("PluginId", plugin.PluginId)
             .WithContext("PluginName", plugin.Name);
        }

        // Initialize the plugin with the service provider
        try
        {
            LogInfo("Initializing plugin: {PluginId}", plugin.PluginId);
            await loadedPlugin.InitializeAsync(_serviceProvider, cancellationToken);
            LogInfo("Plugin initialized successfully: {PluginId}", plugin.PluginId);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to initialize plugin: {PluginId}", plugin.PluginId);
            throw new ConfigurationException(
                $"Failed to initialize plugin '{plugin.Name}': {ex.Message}",
                "PLUGIN_INITIALIZATION_FAILED"
            ).WithContext("PluginId", plugin.PluginId)
             .WithContext("PluginName", plugin.Name);
        }

        plugin.IsEnabled = true;
        plugin.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        LogInfo("Enabled plugin: {PluginId}", plugin.PluginId);

        // Audit log
        await LogAuditAsync(
            plugin.Id,
            plugin.PluginId,
            plugin.Name,
            PluginOperation.Enable,
            true,
            "Plugin enabled and initialized",
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DisablePluginAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plugin = await GetPluginAsync(id, cancellationToken);
        
        if (plugin == null)
        {
            throw new EntityNotFoundException("Plugin", id);
        }

        plugin.IsEnabled = false;
        plugin.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        LogInfo("Disabled plugin: {PluginId}", plugin.PluginId);

        // Audit log
        await LogAuditAsync(
            plugin.Id,
            plugin.PluginId,
            plugin.Name,
            PluginOperation.Disable,
            true,
            "Plugin disabled",
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdatePluginConfigurationAsync(
        Guid id,
        Dictionary<string, string> configuration,
        CancellationToken cancellationToken = default)
    {
        var plugin = await GetPluginAsync(id, cancellationToken);
        
        if (plugin == null)
        {
            throw new EntityNotFoundException("Plugin", id);
        }

        // Validate configuration
        var isValid = await ValidatePluginConfigurationAsync(plugin.PluginId, configuration, cancellationToken);
        if (!isValid)
        {
            throw new ConfigurationException(
                $"Invalid configuration for plugin '{plugin.Name}'.",
                "INVALID_PLUGIN_CONFIGURATION"
            ).WithContext("PluginId", plugin.PluginId)
             .WithContext("PluginName", plugin.Name);
        }

        // Get loaded plugin to check which fields are secrets
        var loadedPlugin = GetLoadedPlugin<IPlugin>(plugin.PluginId);
        var secretKeys = new HashSet<string>();
        if (loadedPlugin != null)
        {
            secretKeys = loadedPlugin.GetConfigurationSchema()
                .Where(c => c.IsSecret)
                .Select(c => c.Key)
                .ToHashSet();
        }

        // Remove existing settings
        _context.PluginSettings.RemoveRange(plugin.Settings);

        // Add new settings
        foreach (var kvp in configuration)
        {
            var isSecret = secretKeys.Contains(kvp.Key);
            var value = kvp.Value;

            // Encrypt secret values
            if (isSecret && !string.IsNullOrEmpty(value))
            {
                value = _encryptionService.EncryptIfNeeded(value);
                LogDebug("Encrypted secret value for key: {Key}", kvp.Key);
            }

            var setting = new PluginSetting
            {
                Id = Guid.NewGuid(),
                PluginId = plugin.Id,
                Key = kvp.Key,
                Value = value,
                IsFromEnvironment = false,
                IsSecret = isSecret,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.PluginSettings.Add(setting);
        }

        // Determine if plugin is configured
        // A plugin is considered configured if:
        // 1. It has configuration values saved, OR
        // 2. It has no required configuration fields (all fields are optional)
        var hasRequiredFields = loadedPlugin?.GetConfigurationSchema()
            .Any(c => c.IsRequired) ?? false;
        
        plugin.IsConfigured = configuration.Any() || !hasRequiredFields;
        plugin.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        LogInfo("Updated configuration for plugin: {PluginId}", plugin.PluginId);

        // Audit log
        await LogAuditAsync(
            plugin.Id,
            plugin.PluginId,
            plugin.Name,
            PluginOperation.Configure,
            true,
            $"Configuration updated with {configuration.Count} settings",
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, string>> GetPluginConfigurationAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var plugin = await GetPluginAsync(id, cancellationToken);
        
        if (plugin == null)
        {
            throw new EntityNotFoundException("Plugin", id);
        }

        var configuration = new Dictionary<string, string>();

        foreach (var setting in plugin.Settings)
        {
            string value;

            // Check if value comes from environment variable
            if (setting.IsFromEnvironment && !string.IsNullOrEmpty(setting.EnvironmentVariableName))
            {
                // Use IConfigurationService to get environment variable value
                try
                {
                    value = await Configuration.GetValueAsync<string>(setting.EnvironmentVariableName, cancellationToken) ?? string.Empty;
                    
                    if (string.IsNullOrEmpty(value))
                    {
                        LogWarning(
                            "Configuration key {Key} not found for plugin {PluginId} setting {SettingKey}",
                            setting.EnvironmentVariableName,
                            plugin.PluginId,
                            setting.Key);
                    }
                    else
                    {
                        LogDebug(
                            "Loaded setting {Key} from configuration key {ConfigKey}",
                            setting.Key,
                            setting.EnvironmentVariableName);
                    }
                }
                catch (Exception ex)
                {
                    LogError(ex, "Failed to load configuration for key: {Key}", setting.EnvironmentVariableName);
                    value = string.Empty;
                }
            }
            else
            {
                value = setting.Value ?? string.Empty;

                // Decrypt secret values from database
                if (setting.IsSecret && !string.IsNullOrEmpty(value))
                {
                    try
                    {
                        value = _encryptionService.DecryptIfNeeded(value);
                    }
                    catch (Exception ex)
                    {
                        LogError(ex, "Failed to decrypt secret value for key: {Key}", setting.Key);
                        // Return empty string if decryption fails
                        value = string.Empty;
                    }
                }
            }

            configuration[setting.Key] = value;
        }

        return configuration;
    }

    /// <inheritdoc/>
    public T? GetLoadedPlugin<T>(string pluginId) where T : class, IPlugin
    {
        var plugin = _pluginLoader.GetLoadedPlugin(pluginId);
        return plugin as T;
    }

    /// <inheritdoc/>
    public IEnumerable<T> GetLoadedPlugins<T>() where T : class, IPlugin
    {
        return _pluginLoader.GetLoadedPlugins()
            .OfType<T>();
    }

    /// <inheritdoc/>
    public async Task ReloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        LogInfo("Reloading plugin: {PluginId}", pluginId);

        var plugin = await _pluginLoader.ReloadPluginAsync(_pluginsPath, pluginId, cancellationToken);

        if (plugin == null)
        {
            throw new ConfigurationException(
                $"Failed to reload plugin '{pluginId}'.",
                "PLUGIN_RELOAD_FAILED"
            ).WithContext("PluginId", pluginId);
        }

        // Update version in database if changed
        var dbPlugin = await GetPluginByPluginIdAsync(pluginId, cancellationToken);
        if (dbPlugin != null && dbPlugin.Version != plugin.Metadata.Version)
        {
            dbPlugin.Version = plugin.Metadata.Version;
            dbPlugin.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        LogInfo("Plugin reloaded successfully: {PluginId}", pluginId);
    }

    /// <inheritdoc/>
    public async Task<bool> ValidatePluginConfigurationAsync(
        string pluginId,
        Dictionary<string, string> configuration,
        CancellationToken cancellationToken = default)
    {
        var plugin = GetLoadedPlugin<IPlugin>(pluginId);
        
        if (plugin == null)
        {
            LogWarning("Plugin not loaded: {PluginId}", pluginId);
            return false;
        }

        // Use our validator first for schema-based validation
        var schema = plugin.GetConfigurationSchema().ToList();
        var validationResult = _validator.Validate(configuration, schema);
        
        if (!validationResult.IsValid)
        {
            LogWarning(
                "Configuration validation failed for plugin {PluginId}: {Errors}",
                pluginId,
                validationResult.GetErrorMessage());
            return false;
        }

        // Then let the plugin do any custom validation
        return await plugin.ValidateConfigurationAsync(configuration, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeletePluginAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plugin = await GetPluginAsync(id, cancellationToken);
        
        if (plugin == null)
        {
            throw new EntityNotFoundException("Plugin", id);
        }

        if (plugin.IsCorePlugin)
        {
            throw new BusinessRuleException(
                $"Cannot delete core plugin '{plugin.Name}'.",
                "CANNOT_DELETE_CORE_PLUGIN"
            ).WithContext("PluginId", plugin.PluginId)
             .WithContext("PluginName", plugin.Name);
        }

        var pluginId = plugin.PluginId;
        var pluginName = plugin.Name;

        _context.Plugins.Remove(plugin);
        await _context.SaveChangesAsync(cancellationToken);

        LogInfo("Deleted plugin: {PluginId}", pluginId);

        // Audit log
        await LogAuditAsync(
            id,
            pluginId,
            pluginName,
            PluginOperation.Delete,
            true,
            "Plugin deleted",
            cancellationToken);
    }

    /// <summary>
    /// Helper method to log audit entries
    /// </summary>
    private async Task LogAuditAsync(
        Guid pluginId,
        string pluginIdentifier,
        string pluginName,
        PluginOperation operation,
        bool success,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userIdString = _userContext.UserId;
            Guid? userId = null;
            if (!string.IsNullOrEmpty(userIdString) && Guid.TryParse(userIdString, out var parsedUserId))
            {
                userId = parsedUserId;
            }

            var username = _userContext.Username ?? "System";
            var ipAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
            var userAgent = _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString();

            await _auditService.LogOperationAsync(
                pluginId,
                pluginIdentifier,
                pluginName,
                operation,
                userId,
                username,
                success,
                changes: null,
                errorMessage: success ? null : notes,
                ipAddress: ipAddress,
                userAgent: userAgent,
                notes: success ? notes : null,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            // Don't fail the operation if audit logging fails
            LogError(ex, "Failed to log audit entry for plugin {PluginId}", pluginId);
        }
    }
}
