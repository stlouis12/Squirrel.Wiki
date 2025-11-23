using System.ComponentModel.DataAnnotations;

namespace Squirrel.Wiki.Web.Models.Admin;

/// <summary>
/// View model for category management
/// </summary>
public class CategoryViewModel : BaseViewModel
{
    public List<CategoryTreeNode> Categories { get; set; } = new();
}

/// <summary>
/// Category tree node for hierarchical display
/// </summary>
public class CategoryTreeNode
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string FullPath { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public int Level { get; set; }
    public int PageCount { get; set; }
    public int SubcategoryCount { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime ModifiedOn { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    public List<CategoryTreeNode> Children { get; set; } = new();
    public bool IsExpanded { get; set; } = true;
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
/// Category select item for parent selection dropdown
/// </summary>
public class CategorySelectItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public int Level { get; set; }
    public bool IsDisabled { get; set; }
}

/// <summary>
/// View model for category details
/// </summary>
public class CategoryDetailsViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string FullPath { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public string? ParentName { get; set; }
    public int Level { get; set; }
    public int PageCount { get; set; }
    public int DirectPageCount { get; set; }
    public int SubcategoryCount { get; set; }
    public DateTime CreatedOn { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime ModifiedOn { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    public List<CategoryTreeNode> Subcategories { get; set; } = new();
    public List<CategoryPageItem> Pages { get; set; } = new();
    public List<string> Breadcrumbs { get; set; } = new();
}

/// <summary>
/// Page item in category details
/// </summary>
public class CategoryPageItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public DateTime ModifiedOn { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
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
