using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Models;

/// <summary>
/// Data transfer object for page information
/// </summary>
public class PageDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public List<string> Tags { get; set; } = new();
    public string Content { get; set; } = string.Empty;
    public string RenderedContent { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool IsLocked { get; set; }
    public bool IsDeleted { get; set; }
    public PageVisibility Visibility { get; set; } = PageVisibility.Inherit;
    public DateTime CreatedOn { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime ModifiedOn { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
}

/// <summary>
/// DTO for creating a new page
/// </summary>
public class PageCreateDto
{
    public string Title { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public int? CategoryId { get; set; }
    public PageVisibility Visibility { get; set; } = PageVisibility.Inherit;
    public List<string> Tags { get; set; } = new();
    public string Content { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
}

/// <summary>
/// DTO for updating an existing page
/// </summary>
public class PageUpdateDto
{
    public string Title { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public int? CategoryId { get; set; }
    public PageVisibility Visibility { get; set; } = PageVisibility.Inherit;
    public List<string> Tags { get; set; } = new();
    public string Content { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public string? ChangeComment { get; set; }
}

/// <summary>
/// DTO for page content with version information
/// </summary>
public class PageContentDto
{
    public Guid Id { get; set; }
    public int PageId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime CreatedOn { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime ModifiedOn { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    public string? ChangeComment { get; set; }
}

/// <summary>
/// DTO for page list items (lightweight)
/// </summary>
public class PageListDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public List<string> Tags { get; set; } = new();
    public DateTime ModifiedOn { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
}
