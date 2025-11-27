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

        // Check for environment variable configuration and auto-configure plugins
        await AutoConfigurePluginsFromEnvironmentAsync(cancellationToken);

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
                    
                    // Load and set plugin configuration before initialization
                    var pluginConfig = await GetPluginConfigurationAsync(dbPlugin.Id, cancellationToken);
                    if (loadedPlugin is Squirrel.Wiki.Plugins.PluginBase pluginBase)
                    {
                        pluginBase.SetPluginConfiguration(pluginConfig);
                    }
                    
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

        // Check if plugin enable/disable is locked by environment variable
        if (IsPluginEnabledLockedByEnvironment(plugin.PluginId))
        {
            throw new BusinessRuleException(
                $"Plugin '{plugin.Name}' enable/disable state is controlled by environment variable and cannot be changed via UI.",
                "PLUGIN_ENABLED_LOCKED"
            ).WithContext("PluginId", plugin.PluginId)
             .WithContext("PluginName", plugin.Name);
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

        // Ensure plugin settings are populated in database for visibility
        await EnsurePluginSettingsPopulatedAsync(plugin, loadedPlugin, cancellationToken);

        // Load and set plugin configuration before initialization
        var pluginConfig = await GetPluginConfigurationAsync(plugin.Id, cancellationToken);
        if (loadedPlugin is Squirrel.Wiki.Plugins.PluginBase pluginBase)
        {
            pluginBase.SetPluginConfiguration(pluginConfig);
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

        // Check if plugin enable/disable is locked by environment variable
        if (IsPluginEnabledLockedByEnvironment(plugin.PluginId))
        {
            throw new BusinessRuleException(
                $"Plugin '{plugin.Name}' enable/disable state is controlled by environment variable and cannot be changed via UI.",
                "PLUGIN_ENABLED_LOCKED"
            ).WithContext("PluginId", plugin.PluginId)
             .WithContext("PluginName", plugin.Name);
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
    /// Auto-configure plugins from environment variables
    /// </summary>
    /// <remarks>
    /// Checks for environment variables using the pattern: PLUGIN_{PLUGINID}_{CONFIGKEY}
    /// For example: PLUGIN_SQUIRREL_WIKI_PLUGINS_LUCENE_INDEXPATH
    /// 
    /// Process:
    /// 1. Check for PLUGIN_{PLUGINID}_ENABLED=true environment variable
    /// 2. If ENABLED=true, check if plugin has all required configuration
    /// 3. If configuration is available via environment variables, create PluginSetting records
    /// 4. Mark plugin as configured if all requirements are met
    /// 5. Enable the plugin if ENABLED=true and plugin is configured
    /// </remarks>
    private async Task AutoConfigurePluginsFromEnvironmentAsync(CancellationToken cancellationToken = default)
    {
        LogInfo("Checking for plugin configuration from environment variables");

        var allPlugins = await _context.Plugins
            .Include(p => p.Settings)
            .ToListAsync(cancellationToken);

        foreach (var dbPlugin in allPlugins)
        {
            var loadedPlugin = GetLoadedPlugin<IPlugin>(dbPlugin.PluginId);
            if (loadedPlugin == null)
            {
                continue;
            }

            // Build environment variable prefix: PLUGIN_{PLUGINID}_
            // Replace both hyphens and dots with underscores for environment variable compatibility
            var envPrefix = $"PLUGIN_{dbPlugin.PluginId.ToUpperInvariant().Replace("-", "_").Replace(".", "_")}_";
            
            // STEP 1: Check if plugin should be enabled/disabled via environment variable
            // Read directly from environment to avoid configuration registry warnings
            var enabledEnvVar = $"{envPrefix}ENABLED";
            bool? shouldEnable = null; // null = not set, true = enable, false = disable
            
            var enabledValue = Environment.GetEnvironmentVariable(enabledEnvVar);
            if (!string.IsNullOrEmpty(enabledValue))
            {
                if (enabledValue.Equals("true", StringComparison.OrdinalIgnoreCase) || 
                    enabledValue.Equals("1", StringComparison.OrdinalIgnoreCase))
                {
                    shouldEnable = true;
                    LogInfo("Found {EnvVar}=true for plugin {PluginId}", enabledEnvVar, dbPlugin.PluginId);
                }
                else if (enabledValue.Equals("false", StringComparison.OrdinalIgnoreCase) || 
                         enabledValue.Equals("0", StringComparison.OrdinalIgnoreCase))
                {
                    shouldEnable = false;
                    LogInfo("Found {EnvVar}=false for plugin {PluginId}", enabledEnvVar, dbPlugin.PluginId);
                }
            }

            // Handle explicit disable
            if (shouldEnable == false)
            {
                if (dbPlugin.IsEnabled)
                {
                    dbPlugin.IsEnabled = false;
                    dbPlugin.UpdatedAt = DateTime.UtcNow;
                    LogInfo("Plugin {PluginId} disabled via environment variable {EnvVar}", 
                        dbPlugin.PluginId, enabledEnvVar);
                }
                continue;
            }

            // If not marked for enabling, skip configuration check
            if (shouldEnable != true)
            {
                continue;
            }

            // STEP 2: Check configuration requirements
            var schema = loadedPlugin.GetConfigurationSchema().ToList();
            
            // Check if all required configuration is available via environment variables
            var envConfig = new Dictionary<string, string>();
            var envVarNames = new Dictionary<string, string>();
            var allRequiredPresent = true;

            foreach (var configItem in schema)
            {
                var envVarName = $"{envPrefix}{configItem.Key.ToUpperInvariant()}";
                
                // Read directly from environment to avoid configuration registry warnings
                var value = Environment.GetEnvironmentVariable(envVarName);
                
                if (!string.IsNullOrEmpty(value))
                {
                    envConfig[configItem.Key] = value;
                    envVarNames[configItem.Key] = envVarName;
                    LogInfo("Found environment variable {EnvVar} for plugin {PluginId}", envVarName, dbPlugin.PluginId);
                }
                else if (configItem.IsRequired)
                {
                    allRequiredPresent = false;
                    LogWarning("Required environment variable {EnvVar} not found for plugin {PluginId}", envVarName, dbPlugin.PluginId);
                }
            }

            // STEP 3: Set up configuration from environment variables if present
            if (envConfig.Any())
            {
                // Validate the configuration
                var isValid = await ValidatePluginConfigurationAsync(dbPlugin.PluginId, envConfig, cancellationToken);
                
                if (!isValid)
                {
                    LogWarning("Environment variable configuration for plugin {PluginId} is invalid, cannot enable", dbPlugin.PluginId);
                    continue;
                }

                // Remove ALL existing settings (environment variables take precedence)
                _context.PluginSettings.RemoveRange(dbPlugin.Settings);

                // Create settings pointing to environment variables
                var secretKeys = schema.Where(c => c.IsSecret).Select(c => c.Key).ToHashSet();
                
                foreach (var kvp in envConfig)
                {
                    var setting = new PluginSetting
                    {
                        Id = Guid.NewGuid(),
                        PluginId = dbPlugin.Id,
                        Key = kvp.Key,
                        Value = null, // Value comes from environment
                        IsFromEnvironment = true,
                        EnvironmentVariableName = envVarNames[kvp.Key],
                        IsSecret = secretKeys.Contains(kvp.Key),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.PluginSettings.Add(setting);
                }

                LogInfo("Plugin {PluginId} configured from environment variables", dbPlugin.PluginId);
            }

            // STEP 4: Ensure plugin settings are populated in database (including defaults)
            await EnsurePluginSettingsPopulatedAsync(dbPlugin, loadedPlugin, cancellationToken);
            
            // STEP 5: Mark as configured if all required fields are present (or no required fields exist)
            if (allRequiredPresent)
            {
                if (!dbPlugin.IsConfigured)
                {
                    dbPlugin.IsConfigured = true;
                    dbPlugin.UpdatedAt = DateTime.UtcNow;
                    LogInfo("Plugin {PluginId} marked as configured", dbPlugin.PluginId);
                }
                
                // STEP 6: Enable the plugin
                dbPlugin.IsEnabled = true;
                dbPlugin.UpdatedAt = DateTime.UtcNow;
                LogInfo("Plugin {PluginId} auto-enabled via environment variable {EnvVar}", 
                    dbPlugin.PluginId, enabledEnvVar);
            }
            else
            {
                LogWarning("Cannot enable plugin {PluginId}: missing required configuration fields", dbPlugin.PluginId);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        LogInfo("Completed environment variable configuration check");
    }

    /// <summary>
    /// Checks if a plugin's enabled state is locked by an environment variable
    /// </summary>
    /// <param name="pluginId">The plugin ID to check</param>
    /// <returns>True if the ENABLED environment variable is set for this plugin, false otherwise</returns>
    private bool IsPluginEnabledLockedByEnvironment(string pluginId)
    {
        var envPrefix = $"PLUGIN_{pluginId.ToUpperInvariant().Replace("-", "_").Replace(".", "_")}_";
        var enabledEnvVar = $"{envPrefix}ENABLED";
        var enabledValue = Environment.GetEnvironmentVariable(enabledEnvVar);
        
        // If the ENABLED environment variable is set (to any value), the plugin is locked
        return !string.IsNullOrEmpty(enabledValue);
    }

    /// <summary>
    /// Ensures plugin settings are populated in the database for visibility and synced with environment variables
    /// </summary>
    /// <remarks>
    /// This method populates/updates the plugin settings table with:
    /// 1. Environment variable references (if configured via env vars) - marked as IsFromEnvironment=true
    /// 2. Default values (if no env vars and no existing settings) - stored as regular database values
    /// 
    /// This method ALWAYS syncs with current environment variable state:
    /// - If an env var is detected, the setting is updated to IsFromEnvironment=true
    /// - If an env var is removed, the setting is updated to IsFromEnvironment=false (preserving existing value)
    /// 
    /// This ensures operators can see what configuration is being used, even if it comes from environment variables.
    /// Settings from environment variables are locked in the UI (similar to main wiki settings).
    /// </remarks>
    private async Task EnsurePluginSettingsPopulatedAsync(
        Plugin plugin,
        IPlugin loadedPlugin,
        CancellationToken cancellationToken = default)
    {
        var schema = loadedPlugin.GetConfigurationSchema().ToList();
        if (!schema.Any())
        {
            LogDebug("Plugin {PluginId} has no configuration schema", plugin.PluginId);
            return;
        }

        // Build environment variable prefix
        var envPrefix = $"PLUGIN_{plugin.PluginId.ToUpperInvariant().Replace("-", "_").Replace(".", "_")}_";
        
        // Check which settings come from environment variables
        var envVarNames = new Dictionary<string, string>();
        var defaultValues = new Dictionary<string, string>();
        
        foreach (var configItem in schema)
        {
            var envVarName = $"{envPrefix}{configItem.Key.ToUpperInvariant()}";
            var envValue = Environment.GetEnvironmentVariable(envVarName);
            
            if (!string.IsNullOrEmpty(envValue))
            {
                // Setting comes from environment variable
                envVarNames[configItem.Key] = envVarName;
                LogDebug("Plugin {PluginId} setting {Key} will use environment variable {EnvVar}", 
                    plugin.PluginId, configItem.Key, envVarName);
            }
            else if (!string.IsNullOrEmpty(configItem.DefaultValue))
            {
                // Use default value
                defaultValues[configItem.Key] = configItem.DefaultValue;
                LogDebug("Plugin {PluginId} setting {Key} will use default value", 
                    plugin.PluginId, configItem.Key);
            }
        }

        var secretKeys = schema.Where(c => c.IsSecret).Select(c => c.Key).ToHashSet();
        var existingSettings = plugin.Settings.ToDictionary(s => s.Key, s => s);
        var settingsToUpdate = new List<PluginSetting>();
        var settingsToAdd = new List<PluginSetting>();
        
        foreach (var configItem in schema)
        {
            var hasEnvVar = envVarNames.ContainsKey(configItem.Key);
            var existingSetting = existingSettings.GetValueOrDefault(configItem.Key);
            
            if (existingSetting != null)
            {
                // Setting exists - sync with environment variable state
                var needsUpdate = false;
                
                if (hasEnvVar && !existingSetting.IsFromEnvironment)
                {
                    // Environment variable was added - switch to env var mode
                    existingSetting.IsFromEnvironment = true;
                    existingSetting.EnvironmentVariableName = envVarNames[configItem.Key];
                    existingSetting.Value = null; // Value comes from environment
                    existingSetting.UpdatedAt = DateTime.UtcNow;
                    needsUpdate = true;
                    LogInfo("Plugin {PluginId} setting {Key} switched to environment variable {EnvVar}", 
                        plugin.PluginId, configItem.Key, envVarNames[configItem.Key]);
                }
                else if (!hasEnvVar && existingSetting.IsFromEnvironment)
                {
                    // Environment variable was removed - switch to database mode
                    existingSetting.IsFromEnvironment = false;
                    existingSetting.EnvironmentVariableName = null;
                    // Keep existing value or use default
                    if (string.IsNullOrEmpty(existingSetting.Value) && defaultValues.ContainsKey(configItem.Key))
                    {
                        var value = defaultValues[configItem.Key];
                        if (secretKeys.Contains(configItem.Key) && !string.IsNullOrEmpty(value))
                        {
                            value = _encryptionService.EncryptIfNeeded(value);
                        }
                        existingSetting.Value = value;
                    }
                    existingSetting.UpdatedAt = DateTime.UtcNow;
                    needsUpdate = true;
                    LogInfo("Plugin {PluginId} setting {Key} switched from environment variable to database", 
                        plugin.PluginId, configItem.Key);
                }
                else if (hasEnvVar && existingSetting.IsFromEnvironment)
                {
                    // Still using env var - ensure env var name is correct
                    if (existingSetting.EnvironmentVariableName != envVarNames[configItem.Key])
                    {
                        existingSetting.EnvironmentVariableName = envVarNames[configItem.Key];
                        existingSetting.UpdatedAt = DateTime.UtcNow;
                        needsUpdate = true;
                    }
                }
                
                if (needsUpdate)
                {
                    settingsToUpdate.Add(existingSetting);
                }
            }
            else
            {
                // Setting doesn't exist - create it
                PluginSetting setting;
                
                if (hasEnvVar)
                {
                    // Setting from environment variable
                    setting = new PluginSetting
                    {
                        Id = Guid.NewGuid(),
                        PluginId = plugin.Id,
                        Key = configItem.Key,
                        Value = null, // Value comes from environment
                        IsFromEnvironment = true,
                        EnvironmentVariableName = envVarNames[configItem.Key],
                        IsSecret = secretKeys.Contains(configItem.Key),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                }
                else if (defaultValues.ContainsKey(configItem.Key))
                {
                    // Setting with default value
                    var value = defaultValues[configItem.Key];
                    
                    // Encrypt secret values
                    if (secretKeys.Contains(configItem.Key) && !string.IsNullOrEmpty(value))
                    {
                        value = _encryptionService.EncryptIfNeeded(value);
                    }
                    
                    setting = new PluginSetting
                    {
                        Id = Guid.NewGuid(),
                        PluginId = plugin.Id,
                        Key = configItem.Key,
                        Value = value,
                        IsFromEnvironment = false,
                        IsSecret = secretKeys.Contains(configItem.Key),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                }
                else
                {
                    // No value available, but create record for visibility
                    setting = new PluginSetting
                    {
                        Id = Guid.NewGuid(),
                        PluginId = plugin.Id,
                        Key = configItem.Key,
                        Value = string.Empty,
                        IsFromEnvironment = false,
                        IsSecret = secretKeys.Contains(configItem.Key),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                }
                
                settingsToAdd.Add(setting);
                _context.PluginSettings.Add(setting);
            }
        }

        if (settingsToAdd.Any() || settingsToUpdate.Any())
        {
            await _context.SaveChangesAsync(cancellationToken);
            LogInfo("Synced {AddCount} new and {UpdateCount} updated settings for plugin {PluginId}", 
                settingsToAdd.Count, settingsToUpdate.Count, plugin.PluginId);
        }
        else
        {
            LogDebug("Plugin {PluginId} settings already in sync", plugin.PluginId);
        }
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
