namespace Squirrel.Wiki.Core.Exceptions;

/// <summary>
/// Exception thrown when an external service call fails
/// </summary>
public class ExternalServiceException : SquirrelWikiException
{
    public string ServiceName { get; }
    public string? Endpoint { get; private set; }
    
    public ExternalServiceException(string serviceName, string message)
        : base(
            $"External service '{serviceName}' failed: {message}",
            "EXTERNAL_SERVICE_ERROR",
            statusCode: 502,
            shouldLog: true)
    {
        ServiceName = serviceName;
        Context["ServiceName"] = serviceName;
    }
    
    public ExternalServiceException(string serviceName, string message, Exception innerException)
        : base(
            $"External service '{serviceName}' failed: {message}",
            "EXTERNAL_SERVICE_ERROR",
            innerException,
            statusCode: 502,
            shouldLog: true)
    {
        ServiceName = serviceName;
        Context["ServiceName"] = serviceName;
    }
    
    public ExternalServiceException WithEndpoint(string endpoint)
    {
        Endpoint = endpoint;
        Context["Endpoint"] = endpoint;
        return this;
    }
    
    public override string GetUserMessage()
    {
        return $"An error occurred while communicating with {ServiceName}. Please try again later.";
    }
}
