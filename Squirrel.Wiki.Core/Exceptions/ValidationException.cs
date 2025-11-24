namespace Squirrel.Wiki.Core.Exceptions;

/// <summary>
/// Exception thrown when validation fails
/// </summary>
public class ValidationException : SquirrelWikiException
{
    public List<ValidationError> Errors { get; }
    
    public ValidationException(List<ValidationError> errors)
        : base(
            "One or more validation errors occurred",
            "VALIDATION_ERROR",
            statusCode: 400,
            shouldLog: false) // Validation errors are expected
    {
        Errors = errors ?? new List<ValidationError>();
        Context["ErrorCount"] = Errors.Count;
        Context["Errors"] = Errors;
    }
    
    public ValidationException(string field, string message)
        : this(new List<ValidationError> { new ValidationError(field, message) })
    {
    }
    
    public ValidationException(string message)
        : this(new List<ValidationError> { new ValidationError(string.Empty, message) })
    {
    }
    
    public override string GetUserMessage()
    {
        if (Errors.Count == 1)
        {
            return Errors[0].Message;
        }
        
        return $"Validation failed with {Errors.Count} error(s): " +
               string.Join("; ", Errors.Select(e => e.Message));
    }
}

public class ValidationError
{
    public string Field { get; }
    public string Message { get; }
    public string? ErrorCode { get; }
    
    public ValidationError(string field, string message, string? errorCode = null)
    {
        Field = field;
        Message = message;
        ErrorCode = errorCode;
    }
}
