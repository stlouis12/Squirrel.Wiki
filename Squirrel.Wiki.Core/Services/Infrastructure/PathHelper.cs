namespace Squirrel.Wiki.Core.Services.Infrastructure;

/// <summary>
/// Centralized helper for resolving application paths, particularly for App_Data and related directories.
/// This ensures consistent path resolution across the application, whether running from the project directory
/// during development or from the published executable directory in production.
/// </summary>
public static class PathHelper
{
    /// <summary>
    /// Resolves a path that may be relative or absolute, with special handling for App_Data paths.
    /// </summary>
    /// <param name="configuredPath">The path from configuration (may be relative or absolute)</param>
    /// <param name="appDataPath">The App_Data path from configuration (optional, defaults to "App_Data")</param>
    /// <returns>An absolute path resolved relative to the application's base directory</returns>
    /// <remarks>
    /// Resolution logic:
    /// 1. If the path is already absolute (rooted), return it as-is
    /// 2. If the path starts with "App_Data/", resolve it relative to the resolved App_Data directory
    /// 3. Otherwise, resolve it relative to AppContext.BaseDirectory (the executable directory)
    /// 
    /// This ensures that paths work correctly whether the app is running from:
    /// - Development: d:\Users\urmom\Source\Repos\Squirrel.Wiki\Squirrel.Wiki.Web\bin\Debug\net8.0\
    /// - Production: /app/ (in a container) or C:\Program Files\SquirrelWiki\
    /// </remarks>
    public static string ResolvePath(string configuredPath, string? appDataPath = null)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new ArgumentException("Path cannot be null or whitespace", nameof(configuredPath));
        }

        // If path is already absolute, return it as-is
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        // Check if this is an App_Data relative path
        if (configuredPath.StartsWith("App_Data/", StringComparison.OrdinalIgnoreCase) ||
            configuredPath.StartsWith("App_Data\\", StringComparison.OrdinalIgnoreCase))
        {
            // Resolve App_Data path first
            var resolvedAppDataPath = ResolveAppDataPath(appDataPath);
            
            // Extract the relative portion after "App_Data/"
            var relativePath = configuredPath.Substring("App_Data/".Length);
            
            return Path.Combine(resolvedAppDataPath, relativePath);
        }

        // For other relative paths, make them relative to the executable directory
        return Path.Combine(AppContext.BaseDirectory, configuredPath);
    }

    /// <summary>
    /// Resolves the App_Data directory path.
    /// </summary>
    /// <param name="appDataPath">The App_Data path from configuration (optional, defaults to "App_Data")</param>
    /// <returns>An absolute path to the App_Data directory</returns>
    /// <remarks>
    /// If appDataPath is null or empty, defaults to "App_Data".
    /// If appDataPath is relative, resolves it relative to AppContext.BaseDirectory.
    /// If appDataPath is absolute, returns it as-is.
    /// </remarks>
    public static string ResolveAppDataPath(string? appDataPath = null)
    {
        // Default to "App_Data" if not specified
        if (string.IsNullOrWhiteSpace(appDataPath))
        {
            appDataPath = "App_Data";
        }

        // If already absolute, return as-is
        if (Path.IsPathRooted(appDataPath))
        {
            return appDataPath;
        }

        // Make relative to executable directory
        return Path.Combine(AppContext.BaseDirectory, appDataPath);
    }

    /// <summary>
    /// Ensures a directory exists, creating it if necessary.
    /// </summary>
    /// <param name="path">The directory path to ensure exists</param>
    /// <returns>The same path that was passed in (for chaining)</returns>
    public static string EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }

    /// <summary>
    /// Resolves a path and ensures the directory exists.
    /// </summary>
    /// <param name="configuredPath">The path from configuration (may be relative or absolute)</param>
    /// <param name="appDataPath">The App_Data path from configuration (optional)</param>
    /// <returns>An absolute path with the directory guaranteed to exist</returns>
    public static string ResolveAndEnsurePath(string configuredPath, string? appDataPath = null)
    {
        var resolvedPath = ResolvePath(configuredPath, appDataPath);
        return EnsureDirectoryExists(resolvedPath);
    }
}
