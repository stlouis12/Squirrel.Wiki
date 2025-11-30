using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Squirrel.Wiki.Core.Security;

/// <summary>
/// Implementation of IUserContext that retrieves user information from HttpContext
/// </summary>
public class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public string? Username => User?.FindFirst(ClaimTypes.Name)?.Value;

    public string? Email => User?.FindFirst(ClaimTypes.Email)?.Value;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool IsAdmin => User?.IsInRole("Admin") ?? false;

    public bool IsEditor => (User?.IsInRole("Editor") ?? false) || IsAdmin;

    public IEnumerable<string> Roles
    {
        get
        {
            if (User == null)
                return Enumerable.Empty<string>();

            return User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();
        }
    }
}
