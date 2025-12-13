namespace Squirrel.Wiki.Core.Constants;

/// <summary>
/// Constants for user role names used throughout the application
/// </summary>
public static class UserRoles
{
    /// <summary>
    /// Administrator role - has full access to all features
    /// </summary>
    public const string ADMIN_ROLE = "Admin";

    /// <summary>
    /// Editor role - can create and edit content
    /// </summary>
    public const string EDITOR_ROLE = "Editor";

    /// <summary>
    /// Combined Admin and Editor roles for authorization attributes (comma-separated)
    /// </summary>
    public const string ADMIN_OR_EDITOR_ROLES = "Admin,Editor";
}
