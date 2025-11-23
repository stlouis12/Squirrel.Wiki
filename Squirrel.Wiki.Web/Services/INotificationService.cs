namespace Squirrel.Wiki.Web.Services;

/// <summary>
/// Service for managing user notifications across requests using TempData.
/// Provides a type-safe, consistent way to display success, error, warning, and info messages.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Adds a success notification to be displayed to the user.
    /// </summary>
    /// <param name="message">The success message to display</param>
    void AddSuccess(string message);

    /// <summary>
    /// Adds a success notification with advanced options.
    /// </summary>
    /// <param name="message">The success message to display</param>
    /// <param name="isToast">Whether to display as a toast notification</param>
    /// <param name="autoDismissMs">Auto-dismiss timeout in milliseconds (0 = no auto-dismiss, default 5000 for success)</param>
    void AddSuccess(string message, bool isToast, int autoDismissMs = 5000);

    /// <summary>
    /// Adds a localized success notification.
    /// </summary>
    /// <param name="localizationKey">The localization key</param>
    /// <param name="args">Optional localization arguments</param>
    void AddLocalizedSuccess(string localizationKey, params object[] args);

    /// <summary>
    /// Adds an error notification to be displayed to the user.
    /// </summary>
    /// <param name="message">The error message to display</param>
    void AddError(string message);

    /// <summary>
    /// Adds an error notification with advanced options.
    /// </summary>
    /// <param name="message">The error message to display</param>
    /// <param name="isToast">Whether to display as a toast notification</param>
    /// <param name="autoDismissMs">Auto-dismiss timeout in milliseconds (0 = no auto-dismiss)</param>
    void AddError(string message, bool isToast, int autoDismissMs = 0);

    /// <summary>
    /// Adds a localized error notification.
    /// </summary>
    /// <param name="localizationKey">The localization key</param>
    /// <param name="args">Optional localization arguments</param>
    void AddLocalizedError(string localizationKey, params object[] args);

    /// <summary>
    /// Adds a warning notification to be displayed to the user.
    /// </summary>
    /// <param name="message">The warning message to display</param>
    void AddWarning(string message);

    /// <summary>
    /// Adds a warning notification with advanced options.
    /// </summary>
    /// <param name="message">The warning message to display</param>
    /// <param name="isToast">Whether to display as a toast notification</param>
    /// <param name="autoDismissMs">Auto-dismiss timeout in milliseconds (0 = no auto-dismiss, default 7000 for warnings)</param>
    void AddWarning(string message, bool isToast, int autoDismissMs = 7000);

    /// <summary>
    /// Adds a localized warning notification.
    /// </summary>
    /// <param name="localizationKey">The localization key</param>
    /// <param name="args">Optional localization arguments</param>
    void AddLocalizedWarning(string localizationKey, params object[] args);

    /// <summary>
    /// Adds an informational notification to be displayed to the user.
    /// </summary>
    /// <param name="message">The info message to display</param>
    void AddInfo(string message);

    /// <summary>
    /// Adds an informational notification with advanced options.
    /// </summary>
    /// <param name="message">The info message to display</param>
    /// <param name="isToast">Whether to display as a toast notification</param>
    /// <param name="autoDismissMs">Auto-dismiss timeout in milliseconds (0 = no auto-dismiss, default 5000 for info)</param>
    void AddInfo(string message, bool isToast, int autoDismissMs = 5000);

    /// <summary>
    /// Adds a localized informational notification.
    /// </summary>
    /// <param name="localizationKey">The localization key</param>
    /// <param name="args">Optional localization arguments</param>
    void AddLocalizedInfo(string localizationKey, params object[] args);

    /// <summary>
    /// Retrieves all notifications for the current request.
    /// This is typically called from the layout view to display notifications.
    /// </summary>
    /// <returns>Collection of notifications to display</returns>
    IEnumerable<Notification> GetNotifications();

    /// <summary>
    /// Checks if there are any notifications pending.
    /// </summary>
    /// <returns>True if notifications exist, false otherwise</returns>
    bool HasNotifications();
}

/// <summary>
/// Represents a user notification with a type and message.
/// </summary>
public class Notification
{
    /// <summary>
    /// The type of notification (Success, Error, Warning, Info)
    /// </summary>
    public NotificationType Type { get; set; }

    /// <summary>
    /// The message to display to the user
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional title for the notification
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Whether the notification can be dismissed by the user
    /// </summary>
    public bool Dismissible { get; set; } = true;

    /// <summary>
    /// Whether to display as a toast notification (non-blocking)
    /// </summary>
    public bool IsToast { get; set; } = false;

    /// <summary>
    /// Auto-dismiss timeout in milliseconds (0 = no auto-dismiss)
    /// </summary>
    public int AutoDismissMs { get; set; } = 0;

    /// <summary>
    /// Whether the message should be localized
    /// </summary>
    public bool IsLocalized { get; set; } = false;

    /// <summary>
    /// Localization key if IsLocalized is true
    /// </summary>
    public string? LocalizationKey { get; set; }

    /// <summary>
    /// Localization arguments for parameterized messages
    /// </summary>
    public object[]? LocalizationArgs { get; set; }
}

/// <summary>
/// Types of notifications that can be displayed to users.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Success notification (green) - for successful operations
    /// </summary>
    Success,

    /// <summary>
    /// Error notification (red) - for errors and failures
    /// </summary>
    Error,

    /// <summary>
    /// Warning notification (yellow) - for warnings and cautions
    /// </summary>
    Warning,

    /// <summary>
    /// Info notification (blue) - for informational messages
    /// </summary>
    Info
}
