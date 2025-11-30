namespace Squirrel.Wiki.Core.Exceptions;

/// <summary>
/// Base exception for all Squirrel.Wiki domain exceptions
/// </summary>
public abstract class SquirrelWikiException : Exception
{
    /// <summary>
    /// Machine-readable error code for API responses and logging
    /// </summary>
    public string ErrorCode { get; }
    
    /// <summary>
    /// Additional context information about the error
    /// </summary>
    public Dictionary<string, object> Context { get; }
    
    /// <summary>
    /// HTTP status code to return (if applicable)
    /// </summary>
    public int StatusCode { get; protected set; }
    
    /// <summary>
    /// Whether this error should be logged as an error (vs warning/info)
    /// </summary>
    public bool ShouldLog { get; protected set; }
    
    protected SquirrelWikiException(
        string message, 
        string errorCode,
        int statusCode = 500,
        bool shouldLog = true) 
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
        ShouldLog = shouldLog;
        Context = new Dictionary<string, object>();
    }
    
    protected SquirrelWikiException(
        string message, 
        string errorCode, 
        Exception innerException,
        int statusCode = 500,
        bool shouldLog = true) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
        ShouldLog = shouldLog;
        Context = new Dictionary<string, object>();
    }
    
    /// <summary>
    /// Add context information to the exception
    /// </summary>
    public SquirrelWikiException WithContext(string key, object value)
    {
        Context[key] = value;
        return this;
    }
    
    /// <summary>
    /// Get a user-friendly error message (can be overridden)
    /// </summary>
    public virtual string GetUserMessage()
    {
        return Message;
    }
}
