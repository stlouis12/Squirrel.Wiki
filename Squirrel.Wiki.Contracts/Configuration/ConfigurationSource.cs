namespace Squirrel.Wiki.Contracts.Configuration;

/// <summary>
/// Indicates where a configuration value comes from
/// </summary>
public enum ConfigurationSource
{
    /// <summary>
    /// Value comes from an environment variable (highest priority, read-only)
    /// </summary>
    EnvironmentVariable,

    /// <summary>
    /// Value comes from the database (medium priority, user-configurable)
    /// </summary>
    Database,

    /// <summary>
    /// Value comes from hardcoded defaults (lowest priority, read-only)
    /// </summary>
    Default
}
