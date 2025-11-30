using Microsoft.Extensions.Logging;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Minimal base service class providing logging functionality for infrastructure services
/// Use this for services that don't require caching, event publishing, or mapping capabilities
/// </summary>
public abstract class MinimalBaseService
{
    /// <summary>
    /// Logger instance for the derived service
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the MinimalBaseService class
    /// </summary>
    /// <param name="logger">Logger instance for the derived service</param>
    protected MinimalBaseService(ILogger logger)
    {
        Logger = logger;
    }

    /// <summary>
    /// Logs an informational message
    /// </summary>
    protected void LogInfo(string message, params object[] args)
    {
        Logger.LogInformation(message, args);
    }

    /// <summary>
    /// Logs a debug message
    /// </summary>
    protected void LogDebug(string message, params object[] args)
    {
        Logger.LogDebug(message, args);
    }

    /// <summary>
    /// Logs a warning message
    /// </summary>
    protected void LogWarning(string message, params object[] args)
    {
        Logger.LogWarning(message, args);
    }

    /// <summary>
    /// Logs an error message
    /// </summary>
    protected void LogError(Exception ex, string message, params object[] args)
    {
        Logger.LogError(ex, message, args);
    }
}
