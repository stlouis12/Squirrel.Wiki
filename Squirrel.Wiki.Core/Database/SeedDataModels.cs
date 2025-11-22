using YamlDotNet.Serialization;

namespace Squirrel.Wiki.Core.Database;

/// <summary>
/// Root model for seed data YAML file
/// </summary>
public class SeedData
{
    [YamlMember(Alias = "categories")]
    public List<SeedCategory> Categories { get; set; } = new();

    [YamlMember(Alias = "tags")]
    public List<SeedTag> Tags { get; set; } = new();

    [YamlMember(Alias = "pages")]
    public List<SeedPage> Pages { get; set; } = new();

    [YamlMember(Alias = "menus")]
    public List<SeedMenu> Menus { get; set; } = new();

    [YamlMember(Alias = "settings")]
    public List<SeedSetting> Settings { get; set; } = new();
}

/// <summary>
/// Category seed data model
/// </summary>
public class SeedCategory
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "slug")]
    public string Slug { get; set; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string? Description { get; set; }

    [YamlMember(Alias = "displayOrder")]
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Tag seed data model
/// </summary>
public class SeedTag
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "normalizedName")]
    public string NormalizedName { get; set; } = string.Empty;
}

/// <summary>
/// Page seed data model
/// </summary>
public class SeedPage
{
    [YamlMember(Alias = "title")]
    public string Title { get; set; } = string.Empty;

    [YamlMember(Alias = "slug")]
    public string Slug { get; set; } = string.Empty;

    [YamlMember(Alias = "category")]
    public string? Category { get; set; }

    [YamlMember(Alias = "tags")]
    public List<string> Tags { get; set; } = new();

    [YamlMember(Alias = "content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Menu seed data model
/// </summary>
public class SeedMenu
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "type")]
    public string Type { get; set; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string? Description { get; set; }

    [YamlMember(Alias = "isEnabled")]
    public bool IsEnabled { get; set; }

    [YamlMember(Alias = "displayOrder")]
    public int DisplayOrder { get; set; }

    [YamlMember(Alias = "markup")]
    public string? Markup { get; set; }

    [YamlMember(Alias = "footerLeftZone")]
    public string? FooterLeftZone { get; set; }

    [YamlMember(Alias = "footerRightZone")]
    public string? FooterRightZone { get; set; }
}

/// <summary>
/// Setting seed data model
/// </summary>
public class SeedSetting
{
    [YamlMember(Alias = "key")]
    public string Key { get; set; } = string.Empty;

    [YamlMember(Alias = "value")]
    public string Value { get; set; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string? Description { get; set; }
}
