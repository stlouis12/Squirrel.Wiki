namespace Squirrel.Wiki.Web.Middleware;

using Squirrel.Wiki.Core.Exceptions;

public class GlobalExceptionHandler
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;
    
    public GlobalExceptionHandler(
        RequestDelegate next,
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }
    
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        response.ContentType = "application/json";
        
        var errorResponse = new ErrorResponse();
        
        switch (exception)
        {
            case SquirrelWikiException squirrelEx:
                // Our custom exceptions
                response.StatusCode = squirrelEx.StatusCode;
                errorResponse.ErrorCode = squirrelEx.ErrorCode;
                errorResponse.Message = squirrelEx.GetUserMessage();
                errorResponse.Details = _environment.IsDevelopment() ? squirrelEx.Message : null;
                errorResponse.Context = _environment.IsDevelopment() ? squirrelEx.Context : null;
                
                // Log based on ShouldLog flag
                if (squirrelEx.ShouldLog)
                {
                    _logger.LogError(squirrelEx, 
                        "Domain exception occurred: {ErrorCode} - {Message}", 
                        squirrelEx.ErrorCode, 
                        squirrelEx.Message);
                }
                else
                {
                    _logger.LogWarning(
                        "Expected exception occurred: {ErrorCode} - {Message}", 
                        squirrelEx.ErrorCode, 
                        squirrelEx.Message);
                }
                break;
            
            case UnauthorizedAccessException:
                response.StatusCode = StatusCodes.Status401Unauthorized;
                errorResponse.ErrorCode = "UNAUTHORIZED";
                errorResponse.Message = "You must be logged in to perform this action.";
                _logger.LogWarning("Unauthorized access attempt");
                break;
            
            default:
                // Unhandled exceptions
                response.StatusCode = StatusCodes.Status500InternalServerError;
                errorResponse.ErrorCode = "INTERNAL_ERROR";
                errorResponse.Message = "An unexpected error occurred. Please try again later.";
                errorResponse.Details = _environment.IsDevelopment() ? exception.Message : null;
                
                _logger.LogError(exception, "Unhandled exception occurred");
                break;
        }
        
        await response.WriteAsJsonAsync(errorResponse);
    }
}

public class ErrorResponse
{
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public Dictionary<string, object>? Context { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

// Extension method for registration
public static class GlobalExceptionHandlerExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandler>();
    }
}
