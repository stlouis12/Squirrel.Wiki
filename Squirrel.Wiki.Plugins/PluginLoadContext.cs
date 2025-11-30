using System.Reflection;
using System.Runtime.Loader;

namespace Squirrel.Wiki.Plugins;

/// <summary>
/// Assembly load context for plugin isolation and hot-reload support
/// </summary>
public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginPath;

    /// <summary>
    /// Creates a new plugin load context
    /// </summary>
    /// <param name="pluginPath">Path to the plugin assembly</param>
    /// <param name="isCollectible">Whether this context can be unloaded (for hot-reload)</param>
    public PluginLoadContext(string pluginPath, bool isCollectible = true) 
        : base(name: Path.GetFileNameWithoutExtension(pluginPath), isCollectible: isCollectible)
    {
        _pluginPath = pluginPath;
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    /// <summary>
    /// Load an assembly by name
    /// </summary>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Try to resolve the assembly path
        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Let the default context handle it (for shared assemblies)
        return null;
    }

    /// <summary>
    /// Load an unmanaged library
    /// </summary>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Get the plugin path
    /// </summary>
    public string PluginPath => _pluginPath;
}
