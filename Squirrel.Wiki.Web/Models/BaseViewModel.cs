namespace Squirrel.Wiki.Web.Models;

/// <summary>
/// Base view model providing common properties for all view models
/// </summary>
public abstract class BaseViewModel
{
    /// <summary>
    /// Page title to be displayed in the browser tab and page header
    /// </summary>
    public string? PageTitle { get; set; }

    /// <summary>
    /// Breadcrumb navigation items (key = display text, value = URL)
    /// </summary>
    public Dictionary<string, string> Breadcrumbs { get; set; } = new();

    /// <summary>
    /// Additional metadata for the page (e.g., for SEO)
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Indicates if the current user can perform administrative actions
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// Indicates if the current user can edit content
    /// </summary>
    public bool IsEditor { get; set; }

    /// <summary>
    /// Current user's display name
    /// </summary>
    public string? CurrentUserDisplayName { get; set; }

    /// <summary>
    /// Indicates if the user is authenticated
    /// </summary>
    public bool IsAuthenticated { get; set; }
}
