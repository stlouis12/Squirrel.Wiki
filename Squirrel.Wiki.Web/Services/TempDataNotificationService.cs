using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Text.Json;

namespace Squirrel.Wiki.Web.Services;

/// <summary>
/// Implementation of INotificationService that stores notifications in TempData.
/// TempData persists data across a single redirect, making it perfect for PRG (Post-Redirect-Get) pattern.
/// </summary>
public class TempDataNotificationService : INotificationService
{
    private readonly ITempDataDictionaryFactory _tempDataFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string NotificationsKey = "_Notifications";

    public TempDataNotificationService(
        ITempDataDictionaryFactory tempDataFactory,
        IHttpContextAccessor httpContextAccessor)
    {
        _tempDataFactory = tempDataFactory ?? throw new ArgumentNullException(nameof(tempDataFactory));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    private ITempDataDictionary TempData
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext 
                ?? throw new InvalidOperationException("HttpContext is not available");
            return _tempDataFactory.GetTempData(httpContext);
        }
    }

    /// <inheritdoc/>
    public void AddSuccess(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or empty", nameof(message));

        Add(new Notification
        {
            Type = NotificationType.Success,
            Message = message,
            Dismissible = true,
            AutoDismissMs = 5000 // Auto-dismiss success after 5 seconds by default
        });
    }

    /// <inheritdoc/>
    public void AddSuccess(string message, bool isToast, int autoDismissMs = 5000)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or empty", nameof(message));

        Add(new Notification
        {
            Type = NotificationType.Success,
            Message = message,
            Dismissible = true,
            IsToast = isToast,
            AutoDismissMs = autoDismissMs
        });
    }

    /// <inheritdoc/>
    public void AddLocalizedSuccess(string localizationKey, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(localizationKey))
            throw new ArgumentException("Localization key cannot be null or empty", nameof(localizationKey));

        Add(new Notification
        {
            Type = NotificationType.Success,
            Message = localizationKey, // Will be localized in the view
            Dismissible = true,
            IsLocalized = true,
            LocalizationKey = localizationKey,
            LocalizationArgs = args,
            AutoDismissMs = 5000
        });
    }

    /// <inheritdoc/>
    public void AddError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or empty", nameof(message));

        Add(new Notification
        {
            Type = NotificationType.Error,
            Message = message,
            Dismissible = true,
            AutoDismissMs = 0 // Errors don't auto-dismiss by default
        });
    }

    /// <inheritdoc/>
    public void AddError(string message, bool isToast, int autoDismissMs = 0)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or empty", nameof(message));

        Add(new Notification
        {
            Type = NotificationType.Error,
            Message = message,
            Dismissible = true,
            IsToast = isToast,
            AutoDismissMs = autoDismissMs
        });
    }

    /// <inheritdoc/>
    public void AddLocalizedError(string localizationKey, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(localizationKey))
            throw new ArgumentException("Localization key cannot be null or empty", nameof(localizationKey));

        Add(new Notification
        {
            Type = NotificationType.Error,
            Message = localizationKey,
            Dismissible = true,
            IsLocalized = true,
            LocalizationKey = localizationKey,
            LocalizationArgs = args,
            AutoDismissMs = 0
        });
    }

    /// <inheritdoc/>
    public void AddWarning(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or empty", nameof(message));

        Add(new Notification
        {
            Type = NotificationType.Warning,
            Message = message,
            Dismissible = true,
            AutoDismissMs = 7000 // Auto-dismiss warnings after 7 seconds by default
        });
    }

    /// <inheritdoc/>
    public void AddWarning(string message, bool isToast, int autoDismissMs = 7000)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or empty", nameof(message));

        Add(new Notification
        {
            Type = NotificationType.Warning,
            Message = message,
            Dismissible = true,
            IsToast = isToast,
            AutoDismissMs = autoDismissMs
        });
    }

    /// <inheritdoc/>
    public void AddLocalizedWarning(string localizationKey, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(localizationKey))
            throw new ArgumentException("Localization key cannot be null or empty", nameof(localizationKey));

        Add(new Notification
        {
            Type = NotificationType.Warning,
            Message = localizationKey,
            Dismissible = true,
            IsLocalized = true,
            LocalizationKey = localizationKey,
            LocalizationArgs = args,
            AutoDismissMs = 7000
        });
    }

    /// <inheritdoc/>
    public void AddInfo(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or empty", nameof(message));

        Add(new Notification
        {
            Type = NotificationType.Info,
            Message = message,
            Dismissible = true,
            AutoDismissMs = 5000 // Auto-dismiss info after 5 seconds by default
        });
    }

    /// <inheritdoc/>
    public void AddInfo(string message, bool isToast, int autoDismissMs = 5000)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or empty", nameof(message));

        Add(new Notification
        {
            Type = NotificationType.Info,
            Message = message,
            Dismissible = true,
            IsToast = isToast,
            AutoDismissMs = autoDismissMs
        });
    }

    /// <inheritdoc/>
    public void AddLocalizedInfo(string localizationKey, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(localizationKey))
            throw new ArgumentException("Localization key cannot be null or empty", nameof(localizationKey));

        Add(new Notification
        {
            Type = NotificationType.Info,
            Message = localizationKey,
            Dismissible = true,
            IsLocalized = true,
            LocalizationKey = localizationKey,
            LocalizationArgs = args,
            AutoDismissMs = 5000
        });
    }

    /// <inheritdoc/>
    public IEnumerable<Notification> GetNotifications()
    {
        var notifications = GetNotificationsList();
        
        // Clear the notifications after retrieving them
        // This ensures they're only displayed once
        TempData.Remove(NotificationsKey);
        
        return notifications;
    }

    /// <inheritdoc/>
    public bool HasNotifications()
    {
        return TempData.ContainsKey(NotificationsKey);
    }

    /// <summary>
    /// Adds a notification to the collection stored in TempData.
    /// </summary>
    private void Add(Notification notification)
    {
        var notifications = GetNotificationsList();
        notifications.Add(notification);
        
        // Serialize and store back in TempData
        var json = JsonSerializer.Serialize(notifications);
        TempData[NotificationsKey] = json;
    }

    /// <summary>
    /// Retrieves the list of notifications from TempData.
    /// Returns an empty list if no notifications exist.
    /// </summary>
    private List<Notification> GetNotificationsList()
    {
        if (TempData.TryGetValue(NotificationsKey, out var value) && value is string json)
        {
            try
            {
                return JsonSerializer.Deserialize<List<Notification>>(json) ?? new List<Notification>();
            }
            catch (JsonException)
            {
                // If deserialization fails, return empty list and clear corrupted data
                TempData.Remove(NotificationsKey);
                return new List<Notification>();
            }
        }
        
        return new List<Notification>();
    }
}
