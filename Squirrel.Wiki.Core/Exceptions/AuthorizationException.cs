namespace Squirrel.Wiki.Core.Exceptions;

/// <summary>
/// Exception thrown when user is not authorized to perform an action
/// </summary>
public class AuthorizationException : SquirrelWikiException
{
    public string? Username { get; }
    public string? RequiredRole { get; }
    public string? Resource { get; }
    
    public AuthorizationException(string message)
        : base(message, "AUTHORIZATION_FAILED", statusCode: 403, shouldLog: false)
    {
    }
    
    public AuthorizationException(string message, string username, string requiredRole)
        : base(
            $"{message}. User '{username}' requires role '{requiredRole}'",
            "AUTHORIZATION_FAILED",
            statusCode: 403,
            shouldLog: false)
    {
        Username = username;
        RequiredRole = requiredRole;
        
        Context["Username"] = username;
        Context["RequiredRole"] = requiredRole;
    }
    
    public AuthorizationException(string message, string username, string resource, string action)
        : base(
            $"{message}. User '{username}' cannot {action} {resource}",
            "AUTHORIZATION_FAILED",
            statusCode: 403,
            shouldLog: false)
    {
        Username = username;
        Resource = resource;
        
        Context["Username"] = username;
        Context["Resource"] = resource;
        Context["Action"] = action;
    }
    
    public override string GetUserMessage()
    {
        return "You do not have permission to perform this action.";
    }
}
