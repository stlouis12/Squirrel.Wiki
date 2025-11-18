namespace Squirrel.Wiki.Web.Models.Admin;

/// <summary>
/// View model for settings management
/// </summary>
public class SettingsViewModel
{
    public List<SettingGroup> Groups { get; set; } = new();
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Group of related settings
/// </summary>
public class SettingGroup
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-gear";
    public List<SettingItem> Settings { get; set; } = new();
}

/// <summary>
/// Individual setting item
/// </summary>
public class SettingItem
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public SettingType Type { get; set; } = SettingType.Text;
    public bool IsRequired { get; set; }
    public string? ValidationPattern { get; set; }
    public List<string>? Options { get; set; } // For dropdown/radio
}

/// <summary>
/// Setting input type
/// </summary>
public enum SettingType
{
    Text,
    Number,
    Boolean,
    Email,
    Url,
    TextArea,
    Dropdown,
    Radio,
    Color,
    Date
}

/// <summary>
/// View model for editing a single setting
/// </summary>
public class EditSettingViewModel
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public SettingType Type { get; set; } = SettingType.Text;
    public bool IsRequired { get; set; }
    public string? ValidationPattern { get; set; }
    public List<string>? Options { get; set; }
}
