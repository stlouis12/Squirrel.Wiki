namespace Squirrel.Wiki.Core.Exceptions;

/// <summary>
/// Exception thrown when there is a configuration error
/// </summary>
public class ConfigurationException : SquirrelWikiException
{
    public string ConfigKey { get; }
    
    public ConfigurationException(string message, string configKey)
        : base(message, "CONFIGURATION_ERROR", statusCode: 500, shouldLog: true)
    {
        ConfigKey = configKey;
        Context["ConfigKey"] = configKey;
    }
    
    public ConfigurationException(string message, string configKey, Exception innerException)
        : base(message, "CONFIGURATION_ERROR", innerException, statusCode: 500, shouldLog: true)
    {
        ConfigKey = configKey;
        Context["ConfigKey"] = configKey;
    }
    
    public override string GetUserMessage()
    {
        return "A configuration error occurred. Please contact the administrator.";
    }
}
