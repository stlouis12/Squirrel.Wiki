using System.ComponentModel.DataAnnotations;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Web.Models;

/// <summary>
/// View model for displaying and editing wiki pages
/// </summary>
public class PageViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Title is required")]
    [StringLength(255, ErrorMessage = "Title cannot exceed 255 characters")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Content is required")]
    public string Content { get; set; } = string.Empty;

    public string HtmlContent { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    [Display(Name = "Tags (comma-separated)")]
    public string? RawTags { get; set; }

    public List<string> Tags { get; set; } = new();

    [Display(Name = "Category")]
    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }
    
    public List<CategoryViewModel> CategoryPath { get; set; } = new();

    [Display(Name = "Lock page (admin only)")]
    public bool IsLocked { get; set; }

    [Display(Name = "Visibility")]
    public PageVisibility Visibility { get; set; } = PageVisibility.Inherit;

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedOn { get; set; }

    public string ModifiedBy { get; set; } = string.Empty;

    public DateTime ModifiedOn { get; set; }

    // For editing
    public List<TagViewModel> AllTags { get; set; } = new();
    public List<CategoryViewModel> AllCategories { get; set; } = new();

    // Settings
    public int MaxTitleLength { get; set; } = 200;

    // For display
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool IsNew => Id == 0;
    public bool IsHomePage { get; set; }
}

/// <summary>
/// View model for tag display
/// </summary>
public class TagViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PageCount { get; set; }
}

/// <summary>
/// View model for category display
/// </summary>
public class CategoryViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentCategoryId { get; set; }
    public string? ParentCategoryName { get; set; }
    public int PageCount { get; set; }
    public int Level { get; set; }
    public string FullPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name with indentation for hierarchical dropdowns
    /// </summary>
    public string DisplayName => Level > 0 ? new string('—', Level) + " " + Name : Name;
}

/// <summary>
/// View model for page history
/// </summary>
public class PageHistoryViewModel
{
    public Guid VersionId { get; set; }
    public int PageId { get; set; }
    public string PageTitle { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string EditedBy { get; set; } = string.Empty;
    public DateTime EditedOn { get; set; }
    public string? ChangeComment { get; set; }
}

/// <summary>
/// View model for search results
/// </summary>
public class SearchResultsViewModel
{
    public string Query { get; set; } = string.Empty;
    public List<SearchResultItemViewModel> Results { get; set; } = new();
    public int TotalResults { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalPages { get; set; }
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

/// <summary>
/// View model for individual search result
/// </summary>
public class SearchResultItemViewModel
{
    public int PageId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public string ModifiedBy { get; set; } = string.Empty;
    public DateTime ModifiedOn { get; set; }
    public float Score { get; set; }
}

/// <summary>
/// View model for page list
/// </summary>
public class PageListViewModel
{
    public List<PageSummaryViewModel> Pages { get; set; } = new();
    public string? FilterBy { get; set; }
    public string? FilterValue { get; set; }
}

/// <summary>
/// View model for page summary in lists
/// </summary>
public class PageSummaryViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public List<string> Tags { get; set; } = new();
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    public DateTime ModifiedOn { get; set; }
    public bool IsLocked { get; set; }
}
