using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Runtime.Loader;
using Squirrel.Wiki.Contracts.Plugins;

namespace Squirrel.Wiki.Plugins;

/// <summary>
/// Loads and manages plugins from disk
/// </summary>
public class PluginLoader : IPluginLoader
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly Dictionary<string, (IPlugin Plugin, PluginLoadContext Context)> _loadedPlugins = new();
    private readonly object _lock = new();

    public PluginLoader(ILogger<PluginLoader> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IPlugin>> LoadPluginsAsync(
        string pluginsPath,
        CancellationToken cancellationToken = default)
    {
        var plugins = new List<IPlugin>();

        _logger.LogInformation("Starting plugin discovery in: {Path}", pluginsPath);

        if (!Directory.Exists(pluginsPath))
        {
            _logger.LogWarning("Plugins directory does not exist: {Path}", pluginsPath);
            return plugins;
        }

        // Find all plugin directories
        var pluginDirs = Directory.GetDirectories(pluginsPath);
        _logger.LogInformation("Found {Count} subdirectories in plugins path", pluginDirs.Length);

        foreach (var pluginDir in pluginDirs)
        {
            try
            {
                var pluginId = Path.GetFileName(pluginDir);
                _logger.LogInformation("Attempting to load plugin from directory: {Dir} (ID: {Id})", pluginDir, pluginId);
                
                var plugin = await LoadPluginAsync(pluginsPath, pluginId, cancellationToken);
                
                if (plugin != null)
                {
                    plugins.Add(plugin);
                    _logger.LogInformation("Successfully loaded plugin: {Id}", pluginId);
                }
                else
                {
                    _logger.LogWarning("Failed to load plugin from directory: {Dir} - LoadPluginAsync returned null", pluginDir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin from directory: {Dir}", pluginDir);
            }
        }

        _logger.LogInformation("Plugin discovery complete. Loaded {Count} plugins out of {Total} directories", plugins.Count, pluginDirs.Length);
        return plugins;
    }

    /// <inheritdoc/>
    public async Task<IPlugin?> LoadPluginAsync(
        string pluginsPath,
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        var pluginDir = Path.Combine(pluginsPath, pluginId);
        
        _logger.LogDebug("LoadPluginAsync called for plugin: {Id} in path: {Path}", pluginId, pluginsPath);
        
        if (!Directory.Exists(pluginDir))
        {
            _logger.LogWarning("Plugin directory does not exist: {Dir}", pluginDir);
            return null;
        }

        // Find the main plugin DLL (should match directory name or be the only DLL)
        var dllFiles = Directory.GetFiles(pluginDir, "*.dll");
        
        _logger.LogInformation("Found {Count} DLL files in plugin directory: {Dir}", dllFiles.Length, pluginDir);
        foreach (var dll in dllFiles)
        {
            _logger.LogDebug("  - {DllName}", Path.GetFileName(dll));
        }
        
        if (dllFiles.Length == 0)
        {
            _logger.LogWarning("No DLL files found in plugin directory: {Dir}", pluginDir);
            return null;
        }

        // Prefer DLL that matches the plugin ID
        var pluginDll = dllFiles.FirstOrDefault(f => 
            Path.GetFileNameWithoutExtension(f).Equals(pluginId, StringComparison.OrdinalIgnoreCase))
            ?? dllFiles[0];

        _logger.LogInformation("Selected plugin DLL: {Dll}", pluginDll);

        try
        {
            _logger.LogInformation("Creating load context for plugin DLL: {Dll}", pluginDll);
            
            // Create a new load context for this plugin
            var loadContext = new PluginLoadContext(pluginDll, isCollectible: true);
            
            // Load the assembly
            _logger.LogInformation("Loading assembly from: {Dll}", pluginDll);
            var assembly = loadContext.LoadFromAssemblyPath(pluginDll);
            _logger.LogInformation("Assembly loaded: {Name} v{Version}", assembly.GetName().Name, assembly.GetName().Version);
            
            // Find types that implement IPlugin
            _logger.LogDebug("Searching for IPlugin implementations in assembly");
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                .ToList();

            _logger.LogInformation("Found {Count} plugin type(s) in assembly", pluginTypes.Count);
            foreach (var type in pluginTypes)
            {
                _logger.LogDebug("  - {TypeName}", type.FullName);
            }

            if (pluginTypes.Count == 0)
            {
                _logger.LogWarning("No plugin types found in assembly: {Assembly}", pluginDll);
                loadContext.Unload();
                return null;
            }

            if (pluginTypes.Count > 1)
            {
                _logger.LogWarning("Multiple plugin types found in assembly: {Assembly}. Using first one.", pluginDll);
            }

            // Create an instance of the plugin
            var pluginType = pluginTypes[0];
            _logger.LogInformation("Creating instance of plugin type: {Type}", pluginType.FullName);
            var plugin = Activator.CreateInstance(pluginType) as IPlugin;

            if (plugin == null)
            {
                _logger.LogError("Failed to create instance of plugin type: {Type}", pluginType.FullName);
                loadContext.Unload();
                return null;
            }

            _logger.LogInformation("Plugin instance created successfully: {Id} v{Version}", plugin.Metadata.Id, plugin.Metadata.Version);

            // Check if plugin is already loaded and unload it if necessary
            bool needsUnload = false;
            lock (_lock)
            {
                if (_loadedPlugins.ContainsKey(plugin.Metadata.Id))
                {
                    _logger.LogWarning("Plugin {Id} is already loaded. Unloading previous version.", plugin.Metadata.Id);
                    needsUnload = true;
                }
            }

            if (needsUnload)
            {
                await UnloadPluginAsync(plugin.Metadata.Id, cancellationToken);
            }

            // Store the plugin and its load context
            lock (_lock)
            {
                _loadedPlugins[plugin.Metadata.Id] = (plugin, loadContext);
            }

            _logger.LogInformation(
                "Loaded plugin: {Id} v{Version} from {Path}",
                plugin.Metadata.Id,
                plugin.Metadata.Version,
                pluginDll);

            return plugin;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin from: {Path}", pluginDll);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task UnloadPluginAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_loadedPlugins.TryGetValue(pluginId, out var entry))
            {
                _logger.LogWarning("Plugin {Id} is not loaded", pluginId);
                return;
            }

            try
            {
                // Shutdown the plugin
                entry.Plugin.ShutdownAsync(cancellationToken).GetAwaiter().GetResult();

                // Unload the context
                entry.Context.Unload();

                // Remove from dictionary
                _loadedPlugins.Remove(pluginId);

                _logger.LogInformation("Unloaded plugin: {Id}", pluginId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unloading plugin: {Id}", pluginId);
            }
        }

        // Force garbage collection to clean up the unloaded context
        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<IPlugin?> ReloadPluginAsync(
        string pluginsPath,
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reloading plugin: {Id}", pluginId);

        // Unload the existing plugin
        await UnloadPluginAsync(pluginId, cancellationToken);

        // Load the plugin again
        return await LoadPluginAsync(pluginsPath, pluginId, cancellationToken);
    }

    /// <inheritdoc/>
    public IEnumerable<IPlugin> GetLoadedPlugins()
    {
        lock (_lock)
        {
            return _loadedPlugins.Values.Select(v => v.Plugin).ToList();
        }
    }

    /// <inheritdoc/>
    public IPlugin? GetLoadedPlugin(string pluginId)
    {
        lock (_lock)
        {
            return _loadedPlugins.TryGetValue(pluginId, out var entry) ? entry.Plugin : null;
        }
    }
}
