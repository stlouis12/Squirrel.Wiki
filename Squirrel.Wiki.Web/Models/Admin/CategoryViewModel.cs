using System.ComponentModel.DataAnnotations;
using Squirrel.Wiki.Core.Services;

namespace Squirrel.Wiki.Web.Models.Admin;

/// <summary>
/// View model for category management
/// </summary>
public class CategoryViewModel : BaseViewModel
{
    public List<CategoryTreeNode> Categories { get; set; } = new();
    public int MaxCategoryDepth { get; set; }
}

/// <summary>
/// View model for editing a category
/// </summary>
public class EditCategoryViewModel
{
    public int Id { get; set; }
    
    [Required(ErrorMessage = "Category name is required")]
    [StringLength(100, ErrorMessage = "Category name cannot exceed 100 characters")]
    [RegularExpression(@"^[a-zA-Z0-9\s\-_]+$", ErrorMessage = "Category name can only contain letters, numbers, spaces, hyphens, and underscores")]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }
    
    [Display(Name = "Parent Category")]
    public int? ParentId { get; set; }
    
    public string? CurrentPath { get; set; }
    public string? NewPath { get; set; }
    
    public List<CategorySelectItem> AvailableParents { get; set; } = new();
    
    public bool IsNew => Id == 0;
}


/// <summary>
/// Request model for moving a category
/// </summary>
public class MoveCategoryRequest
{
    public int CategoryId { get; set; }
    public int? NewParentId { get; set; }
}

/// <summary>
/// Request model for reordering categories
/// </summary>
public class ReorderCategoriesRequest
{
    public List<CategoryOrderItem> Categories { get; set; } = new();
}

/// <summary>
/// Category order item
/// </summary>
public class CategoryOrderItem
{
    public int Id { get; set; }
    public int DisplayOrder { get; set; }
    public int? ParentId { get; set; }
}

/// <summary>
/// Response model for category operations
/// </summary>
public class CategoryOperationResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
    public CategoryTreeNode? Category { get; set; }
}
