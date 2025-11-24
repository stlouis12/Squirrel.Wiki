using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Squirrel.Wiki.Core.Exceptions;
using Squirrel.Wiki.Core.Services;
using Squirrel.Wiki.Web.Models;
using Squirrel.Wiki.Web.Resources;
using Squirrel.Wiki.Web.Services;

namespace Squirrel.Wiki.Web.Controllers;

/// <summary>
/// Base controller providing common functionality for all controllers.
/// Includes notification service support and error handling helpers.
/// </summary>
public abstract class BaseController : Controller
{
    protected readonly ILogger _logger;
    protected readonly INotificationService _notifications;
    protected readonly ITimezoneService? _timezoneService;
    protected readonly IStringLocalizer<SharedResources>? _localizer;

    protected BaseController(ILogger logger, INotificationService notifications)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
    }

    protected BaseController(
        ILogger logger, 
        INotificationService notifications, 
        ITimezoneService? timezoneService = null,
        IStringLocalizer<SharedResources>? localizer = null)
        : this(logger, notifications)
    {
        _timezoneService = timezoneService;
        _localizer = localizer;
    }

    /// <summary>
    /// Populates common properties for a BaseViewModel, including timezone service and localizer
    /// </summary>
    /// <typeparam name="T">Type of the view model (must inherit from BaseViewModel)</typeparam>
    /// <param name="viewModel">The view model to populate</param>
    protected void PopulateBaseViewModel<T>(T viewModel) where T : BaseViewModel
    {
        if (_timezoneService != null)
        {
            viewModel.TimezoneService = _timezoneService;
        }
        
        if (_localizer != null)
        {
            viewModel.Localizer = _localizer;
        }
    }

    #region Notification Helpers

    /// <summary>
    /// Adds a success notification to be displayed to the user.
    /// </summary>
    protected void NotifySuccess(string message)
    {
        _notifications.AddSuccess(message);
    }

    /// <summary>
    /// Adds a success toast notification (non-blocking).
    /// </summary>
    protected void NotifySuccessToast(string message, int autoDismissMs = 5000)
    {
        _notifications.AddSuccess(message, isToast: true, autoDismissMs);
    }

    /// <summary>
    /// Adds a localized success notification.
    /// </summary>
    protected void NotifyLocalizedSuccess(string localizationKey, params object[] args)
    {
        _notifications.AddLocalizedSuccess(localizationKey, args);
    }

    /// <summary>
    /// Adds an error notification to be displayed to the user.
    /// </summary>
    protected void NotifyError(string message)
    {
        _notifications.AddError(message);
    }

    /// <summary>
    /// Adds an error toast notification (non-blocking).
    /// </summary>
    protected void NotifyErrorToast(string message, int autoDismissMs = 0)
    {
        _notifications.AddError(message, isToast: true, autoDismissMs);
    }

    /// <summary>
    /// Adds a localized error notification.
    /// </summary>
    protected void NotifyLocalizedError(string localizationKey, params object[] args)
    {
        _notifications.AddLocalizedError(localizationKey, args);
    }

    /// <summary>
    /// Adds a warning notification to be displayed to the user.
    /// </summary>
    protected void NotifyWarning(string message)
    {
        _notifications.AddWarning(message);
    }

    /// <summary>
    /// Adds a warning toast notification (non-blocking).
    /// </summary>
    protected void NotifyWarningToast(string message, int autoDismissMs = 7000)
    {
        _notifications.AddWarning(message, isToast: true, autoDismissMs);
    }

    /// <summary>
    /// Adds a localized warning notification.
    /// </summary>
    protected void NotifyLocalizedWarning(string localizationKey, params object[] args)
    {
        _notifications.AddLocalizedWarning(localizationKey, args);
    }

    /// <summary>
    /// Adds an informational notification to be displayed to the user.
    /// </summary>
    protected void NotifyInfo(string message)
    {
        _notifications.AddInfo(message);
    }

    /// <summary>
    /// Adds an informational toast notification (non-blocking).
    /// </summary>
    protected void NotifyInfoToast(string message, int autoDismissMs = 5000)
    {
        _notifications.AddInfo(message, isToast: true, autoDismissMs);
    }

    /// <summary>
    /// Adds a localized informational notification.
    /// </summary>
    protected void NotifyLocalizedInfo(string localizationKey, params object[] args)
    {
        _notifications.AddLocalizedInfo(localizationKey, args);
    }

    #endregion

    #region Error Handling Helpers

    /// <summary>
    /// Handles an error by logging it, adding an error notification, and redirecting to the specified action.
    /// </summary>
    /// <param name="ex">The exception that occurred</param>
    /// <param name="userMessage">User-friendly error message to display</param>
    /// <param name="logContext">Additional context for logging (optional)</param>
    /// <param name="actionName">Action to redirect to (defaults to Index)</param>
    /// <param name="controllerName">Controller to redirect to (defaults to current controller)</param>
    /// <returns>RedirectToAction result</returns>
    protected IActionResult HandleError(
        Exception ex, 
        string userMessage, 
        string? logContext = null,
        string actionName = "Index",
        string? controllerName = null)
    {
        _logger.LogError(ex, logContext ?? "An error occurred");
        _notifications.AddError(userMessage);
        
        if (string.IsNullOrEmpty(controllerName))
        {
            return RedirectToAction(actionName);
        }
        
        return RedirectToAction(actionName, controllerName);
    }

    /// <summary>
    /// Handles an error by logging it, adding an error notification, and returning a view with the specified model.
    /// </summary>
    /// <typeparam name="T">Type of the view model</typeparam>
    /// <param name="ex">The exception that occurred</param>
    /// <param name="userMessage">User-friendly error message to display</param>
    /// <param name="viewModel">View model to pass to the view</param>
    /// <param name="logContext">Additional context for logging (optional)</param>
    /// <param name="viewName">Name of the view to render (optional)</param>
    /// <returns>View result</returns>
    protected IActionResult HandleError<T>(
        Exception ex, 
        string userMessage, 
        T viewModel, 
        string? logContext = null,
        string? viewName = null) where T : class
    {
        _logger.LogError(ex, logContext ?? "An error occurred");
        _notifications.AddError(userMessage);
        
        if (string.IsNullOrEmpty(viewName))
        {
            return View(viewModel);
        }
        
        return View(viewName, viewModel);
    }

    /// <summary>
    /// Handle domain exceptions and return appropriate action result.
    /// This method provides intelligent handling of SquirrelWikiException types.
    /// </summary>
    protected IActionResult HandleDomainException(SquirrelWikiException ex)
    {
        // Add user-friendly notification
        NotifyError(ex.GetUserMessage());
        
        // Log if needed
        if (ex.ShouldLog)
        {
            _logger.LogError(ex, "Domain exception: {ErrorCode}", ex.ErrorCode);
        }
        else
        {
            _logger.LogWarning("Expected exception: {ErrorCode} - {Message}", ex.ErrorCode, ex.Message);
        }
        
        // Return appropriate result based on status code
        return ex.StatusCode switch
        {
            404 => NotFound(),
            400 => BadRequest(),
            403 => Forbid(),
            422 => UnprocessableEntity(),
            _ => StatusCode(ex.StatusCode)
        };
    }

    /// <summary>
    /// Executes an async action with automatic domain exception handling.
    /// SquirrelWikiException types are handled intelligently with proper status codes and user messages.
    /// Other exceptions fall through to be handled by the global exception handler.
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <returns>Result of the action or error response</returns>
    protected async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (SquirrelWikiException ex)
        {
            return HandleDomainException(ex);
        }
        // Other exceptions will be caught by global exception handler
    }

    /// <summary>
    /// Executes an async action with automatic error handling.
    /// If an exception occurs, it will be logged and an error notification will be shown.
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <param name="errorMessage">User-friendly error message to display if an exception occurs</param>
    /// <param name="logContext">Additional context for logging (optional)</param>
    /// <returns>Result of the action or error redirect</returns>
    [Obsolete("Use ExecuteAsync(Func<Task<IActionResult>>) instead for better exception handling")]
    protected async Task<IActionResult> ExecuteAsync(
        Func<Task<IActionResult>> action, 
        string errorMessage, 
        string? logContext = null)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            return HandleError(ex, errorMessage, logContext);
        }
    }

    /// <summary>
    /// Executes an async action with automatic error handling and custom error handler.
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <param name="errorHandler">Custom error handler function</param>
    /// <returns>Result of the action or error handler</returns>
    [Obsolete("Use ExecuteAsync(Func<Task<IActionResult>>) instead for better exception handling")]
    protected async Task<IActionResult> ExecuteAsync(
        Func<Task<IActionResult>> action,
        Func<Exception, IActionResult> errorHandler)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            return errorHandler(ex);
        }
    }

    #endregion

    #region Validation Helpers

    /// <summary>
    /// Checks if the model state is valid. If not, adds an error notification.
    /// </summary>
    /// <param name="errorMessage">Error message to display if model state is invalid</param>
    /// <returns>True if model state is valid, false otherwise</returns>
    protected bool ValidateModelState(string errorMessage = "Please correct the errors and try again.")
    {
        if (!ModelState.IsValid)
        {
            _notifications.AddError(errorMessage);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Checks if an entity exists. If not, adds an error notification.
    /// </summary>
    /// <typeparam name="T">Type of the entity</typeparam>
    /// <param name="entity">The entity to check</param>
    /// <param name="entityName">Name of the entity for the error message</param>
    /// <returns>True if entity exists, false otherwise</returns>
    protected bool ValidateEntityExists<T>(T? entity, string entityName = "Item") where T : class
    {
        if (entity == null)
        {
            _notifications.AddError($"{entityName} not found.");
            return false;
        }
        return true;
    }

    #endregion
}
