namespace Squirrel.Wiki.Core.Database.Entities;

/// <summary>
/// Represents a hierarchical category for organizing pages
/// </summary>
public class Category
{
    public int Id { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public string Slug { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    public int? ParentCategoryId { get; set; }
    
    public int DisplayOrder { get; set; }
    
    public DateTime CreatedOn { get; set; }
    
    public string CreatedBy { get; set; } = string.Empty;
    
    public DateTime ModifiedOn { get; set; }
    
    public string ModifiedBy { get; set; } = string.Empty;
    
    // Navigation properties
    public Category? ParentCategory { get; set; }
    
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();
    
    public ICollection<Page> Pages { get; set; } = new List<Page>();
}
