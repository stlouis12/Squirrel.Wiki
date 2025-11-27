namespace Squirrel.Wiki.Contracts.Plugins;

/// <summary>
/// Represents a custom action that a plugin can expose
/// </summary>
public interface IPluginAction
{
    /// <summary>
    /// Unique identifier for the action
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display name for the action
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of what the action does
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Icon class for the action (e.g., "fas fa-sync" for Font Awesome)
    /// </summary>
    string? IconClass { get; }

    /// <summary>
    /// Whether this action requires confirmation before execution
    /// </summary>
    bool RequiresConfirmation { get; }

    /// <summary>
    /// Confirmation message to display if RequiresConfirmation is true
    /// </summary>
    string? ConfirmationMessage { get; }

    /// <summary>
    /// Whether this action is a long-running operation
    /// </summary>
    bool IsLongRunning { get; }

    /// <summary>
    /// Executes the action
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving dependencies</param>
    /// <param name="parameters">Optional parameters for the action</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the action execution</returns>
    Task<PluginActionResult> ExecuteAsync(IServiceProvider serviceProvider, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a plugin action execution
/// </summary>
public class PluginActionResult
{
    /// <summary>
    /// Whether the action succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Message describing the result
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Additional data returned by the action
    /// </summary>
    public Dictionary<string, object>? Data { get; set; }

    /// <summary>
    /// Error details if the action failed
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static PluginActionResult Successful(string message, Dictionary<string, object>? data = null)
    {
        return new PluginActionResult
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    /// <summary>
    /// Creates a failed result
    /// </summary>
    public static PluginActionResult Failed(string message, string? errorDetails = null)
    {
        return new PluginActionResult
        {
            Success = false,
            Message = message,
            ErrorDetails = errorDetails
        };
    }
}
