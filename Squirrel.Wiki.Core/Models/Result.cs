namespace Squirrel.Wiki.Core.Models;

/// <summary>
/// Represents the result of an operation that can succeed or fail
/// </summary>
/// <typeparam name="T">The type of the value returned on success</typeparam>
public class Result<T>
{
    /// <summary>
    /// Indicates whether the operation was successful
    /// </summary>
    public bool IsSuccess { get; }
    
    /// <summary>
    /// Indicates whether the operation failed
    /// </summary>
    public bool IsFailure => !IsSuccess;
    
    /// <summary>
    /// The value returned on success (null on failure)
    /// </summary>
    public T? Value { get; }
    
    /// <summary>
    /// The error message on failure (null on success)
    /// </summary>
    public string? Error { get; }
    
    /// <summary>
    /// The error code on failure (null on success)
    /// </summary>
    public string? ErrorCode { get; }
    
    /// <summary>
    /// Additional context information about the result
    /// </summary>
    public Dictionary<string, object>? Context { get; private set; }
    
    private Result(bool isSuccess, T? value, string? error, string? errorCode)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        ErrorCode = errorCode;
    }
    
    /// <summary>
    /// Creates a successful result with a value
    /// </summary>
    public static Result<T> Success(T value)
    {
        return new Result<T>(true, value, null, null);
    }
    
    /// <summary>
    /// Creates a failed result with an error message and code
    /// </summary>
    public static Result<T> Failure(string error, string errorCode)
    {
        return new Result<T>(false, default, error, errorCode);
    }
    
    /// <summary>
    /// Creates a failed result from an exception
    /// </summary>
    public static Result<T> FromException(Exception exception, string? errorCode = null)
    {
        return new Result<T>(
            false, 
            default, 
            exception.Message, 
            errorCode ?? "EXCEPTION"
        );
    }
    
    /// <summary>
    /// Adds context information to the result
    /// </summary>
    public Result<T> WithContext(string key, object value)
    {
        Context ??= new Dictionary<string, object>();
        Context[key] = value;
        return this;
    }
    
    /// <summary>
    /// Maps the value to a new type if successful
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        if (IsFailure)
        {
            return Result<TNew>.Failure(Error!, ErrorCode!);
        }
        
        try
        {
            var newValue = mapper(Value!);
            return Result<TNew>.Success(newValue);
        }
        catch (Exception ex)
        {
            return Result<TNew>.FromException(ex);
        }
    }
    
    /// <summary>
    /// Maps the value to a new type asynchronously if successful
    /// </summary>
    public async Task<Result<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper)
    {
        if (IsFailure)
        {
            return Result<TNew>.Failure(Error!, ErrorCode!);
        }
        
        try
        {
            var newValue = await mapper(Value!);
            return Result<TNew>.Success(newValue);
        }
        catch (Exception ex)
        {
            return Result<TNew>.FromException(ex);
        }
    }
    
    /// <summary>
    /// Executes an action if the result is successful
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess)
        {
            action(Value!);
        }
        return this;
    }
    
    /// <summary>
    /// Executes an action if the result is a failure
    /// </summary>
    public Result<T> OnFailure(Action<string, string> action)
    {
        if (IsFailure)
        {
            action(Error!, ErrorCode!);
        }
        return this;
    }
    
    /// <summary>
    /// Matches the result to one of two functions based on success/failure
    /// </summary>
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<string, string, TResult> onFailure)
    {
        return IsSuccess 
            ? onSuccess(Value!) 
            : onFailure(Error!, ErrorCode!);
    }
}

/// <summary>
/// Represents the result of an operation that doesn't return a value
/// </summary>
public class Result
{
    /// <summary>
    /// Indicates whether the operation was successful
    /// </summary>
    public bool IsSuccess { get; }
    
    /// <summary>
    /// Indicates whether the operation failed
    /// </summary>
    public bool IsFailure => !IsSuccess;
    
    /// <summary>
    /// The error message on failure (null on success)
    /// </summary>
    public string? Error { get; }
    
    /// <summary>
    /// The error code on failure (null on success)
    /// </summary>
    public string? ErrorCode { get; }
    
    /// <summary>
    /// Additional context information about the result
    /// </summary>
    public Dictionary<string, object>? Context { get; private set; }
    
    private Result(bool isSuccess, string? error, string? errorCode)
    {
        IsSuccess = isSuccess;
        Error = error;
        ErrorCode = errorCode;
    }
    
    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static Result Success()
    {
        return new Result(true, null, null);
    }
    
    /// <summary>
    /// Creates a failed result with an error message and code
    /// </summary>
    public static Result Failure(string error, string errorCode)
    {
        return new Result(false, error, errorCode);
    }
    
    /// <summary>
    /// Creates a failed result from an exception
    /// </summary>
    public static Result FromException(Exception exception, string? errorCode = null)
    {
        return new Result(false, exception.Message, errorCode ?? "EXCEPTION");
    }
    
    /// <summary>
    /// Adds context information to the result
    /// </summary>
    public Result WithContext(string key, object value)
    {
        Context ??= new Dictionary<string, object>();
        Context[key] = value;
        return this;
    }
    
    /// <summary>
    /// Executes an action if the result is successful
    /// </summary>
    public Result OnSuccess(Action action)
    {
        if (IsSuccess)
        {
            action();
        }
        return this;
    }
    
    /// <summary>
    /// Executes an action if the result is a failure
    /// </summary>
    public Result OnFailure(Action<string, string> action)
    {
        if (IsFailure)
        {
            action(Error!, ErrorCode!);
        }
        return this;
    }
    
    /// <summary>
    /// Matches the result to one of two functions based on success/failure
    /// </summary>
    public TResult Match<TResult>(
        Func<TResult> onSuccess,
        Func<string, string, TResult> onFailure)
    {
        return IsSuccess 
            ? onSuccess() 
            : onFailure(Error!, ErrorCode!);
    }
    
    /// <summary>
    /// Converts a Result to Result&lt;T&gt; with a value
    /// </summary>
    public Result<T> ToResult<T>(T value)
    {
        return IsSuccess 
            ? Result<T>.Success(value) 
            : Result<T>.Failure(Error!, ErrorCode!);
    }
}
