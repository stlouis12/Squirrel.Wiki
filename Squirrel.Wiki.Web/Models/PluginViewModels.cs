namespace Squirrel.Wiki.Web.Models;

/// <summary>
/// View model for the plugin list page
/// </summary>
public class PluginListViewModel : BaseViewModel
{
    public List<PluginItemViewModel> Plugins { get; set; } = new();
}

/// <summary>
/// View model for a single plugin in the list
/// </summary>
public class PluginItemViewModel
{
    public Guid Id { get; set; }
    public string PluginId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsConfigured { get; set; }
    public bool IsCorePlugin { get; set; }
    public bool IsEnabledLockedByEnvironment { get; set; }
    
    // Computed properties for UI logic
    public bool CanDelete => !IsCorePlugin;
    public bool CanEnable => IsConfigured && !IsEnabled && !IsEnabledLockedByEnvironment;
    public bool CanDisable => IsEnabled && !IsEnabledLockedByEnvironment;
    public bool CanConfigure => true;
}

/// <summary>
/// View model for plugin details page
/// </summary>
public class PluginDetailsViewModel
{
    public Guid Id { get; set; }
    public string PluginId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsConfigured { get; set; }
    public bool IsCorePlugin { get; set; }
    public bool IsEnabledLockedByEnvironment { get; set; }
    public List<PluginConfigurationItemViewModel> ConfigurationSchema { get; set; } = new();
    public Dictionary<string, string> CurrentValues { get; set; } = new();
    public List<PluginActionViewModel> Actions { get; set; } = new();
    
    // Computed properties
    public bool CanDelete => !IsCorePlugin;
    public bool CanEnable => IsConfigured && !IsEnabled && !IsEnabledLockedByEnvironment;
    public bool CanDisable => IsEnabled && !IsEnabledLockedByEnvironment;
}

/// <summary>
/// View model for a plugin action
/// </summary>
public class PluginActionViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconClass { get; set; }
    public bool RequiresConfirmation { get; set; }
    public string? ConfirmationMessage { get; set; }
    public bool IsLongRunning { get; set; }
}

/// <summary>
/// View model for a configuration item in the schema
/// </summary>
public class PluginConfigurationItemViewModel
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsSecret { get; set; }
    public string DefaultValue { get; set; } = string.Empty;
    public string ValidationPattern { get; set; } = string.Empty;
    public string ValidationMessage { get; set; } = string.Empty;
}

/// <summary>
/// View model for plugin configuration form
/// </summary>
public class PluginConfigureViewModel
{
    public Guid Id { get; set; }
    public string PluginId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<PluginConfigurationFieldViewModel> Fields { get; set; } = new();
}

/// <summary>
/// View model for a single configuration field
/// </summary>
public class PluginConfigurationFieldViewModel
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsSecret { get; set; }
    public bool IsFromEnvironment { get; set; }
    public string? EnvironmentVariableName { get; set; }
    public string DefaultValue { get; set; } = string.Empty;
    public string ValidationPattern { get; set; } = string.Empty;
    public string ValidationMessage { get; set; } = string.Empty;
    public string Placeholder { get; set; } = string.Empty;
}

/// <summary>
/// Form model for submitting plugin configuration
/// </summary>
public class PluginConfigurationFormModel
{
    public Guid Id { get; set; }
    public Dictionary<string, string> Configuration { get; set; } = new();
}
