using Microsoft.AspNetCore.Authorization;

namespace Squirrel.Wiki.Core.Security.Authorization;

/// <summary>
/// Authorization requirement for page access operations
/// </summary>
public class PageAccessRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// The type of access being requested
    /// </summary>
    public PageAccessType AccessType { get; }

    /// <summary>
    /// Creates a new page access requirement
    /// </summary>
    /// <param name="accessType">The type of access required</param>
    public PageAccessRequirement(PageAccessType accessType)
    {
        AccessType = accessType;
    }
}

/// <summary>
/// Types of page access operations
/// </summary>
public enum PageAccessType
{
    /// <summary>
    /// View/read access to a page
    /// </summary>
    View,

    /// <summary>
    /// Edit/modify access to a page
    /// </summary>
    Edit,

    /// <summary>
    /// Delete access to a page
    /// </summary>
    Delete
}
