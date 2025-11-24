namespace Squirrel.Wiki.Core.Exceptions;

/// <summary>
/// Exception thrown when a business rule is violated
/// </summary>
public class BusinessRuleException : SquirrelWikiException
{
    public string RuleCode { get; }
    
    public BusinessRuleException(string message, string ruleCode)
        : base(message, ruleCode, statusCode: 422, shouldLog: false)
    {
        RuleCode = ruleCode;
        Context["RuleCode"] = ruleCode;
    }
    
    public BusinessRuleException(string message, string ruleCode, Exception innerException)
        : base(message, ruleCode, innerException, statusCode: 422, shouldLog: true)
    {
        RuleCode = ruleCode;
        Context["RuleCode"] = ruleCode;
    }
    
    // Common business rule exceptions
    public static BusinessRuleException SlugAlreadyExists(string slug)
    {
        return new BusinessRuleException(
            $"A page with slug '{slug}' already exists",
            "SLUG_EXISTS"
        ).WithContext("Slug", slug) as BusinessRuleException;
    }
    
    public static BusinessRuleException UsernameAlreadyExists(string username)
    {
        return new BusinessRuleException(
            $"Username '{username}' is already taken",
            "USERNAME_EXISTS"
        ).WithContext("Username", username) as BusinessRuleException;
    }
    
    public static BusinessRuleException EmailAlreadyExists(string email)
    {
        return new BusinessRuleException(
            $"Email '{email}' is already registered",
            "EMAIL_EXISTS"
        ).WithContext("Email", email) as BusinessRuleException;
    }
    
    public static BusinessRuleException MaxDepthExceeded(int maxDepth, int currentDepth)
    {
        return new BusinessRuleException(
            $"Maximum nesting depth of {maxDepth} levels would be exceeded (current: {currentDepth})",
            "MAX_DEPTH_EXCEEDED"
        )
        .WithContext("MaxDepth", maxDepth)
        .WithContext("CurrentDepth", currentDepth) as BusinessRuleException;
    }
    
    public static BusinessRuleException CircularReferenceDetected(string entityType)
    {
        return new BusinessRuleException(
            $"Circular reference detected in {entityType}",
            "CIRCULAR_REFERENCE"
        ).WithContext("EntityType", entityType) as BusinessRuleException;
    }
}
