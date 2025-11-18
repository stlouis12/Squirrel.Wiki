using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Squirrel.Wiki.Web.Models.Admin;

/// <summary>
/// View model for menu management
/// </summary>
public class MenuViewModel
{
    public List<MenuListItem> Menus { get; set; } = new();
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Menu list item
/// </summary>
public class MenuListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MenuType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ItemCount { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime ModifiedOn { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
}

/// <summary>
/// View model for editing a menu
/// </summary>
public class EditMenuViewModel
{
    public int Id { get; set; }
    
    [Required(ErrorMessage = "Menu name is required")]
    [StringLength(100, ErrorMessage = "Menu name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Menu type is required")]
    [Display(Name = "Menu Type")]
    public int MenuType { get; set; }
    
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }
    
    [Display(Name = "Menu Markup")]
    public string? MenuMarkup { get; set; }
    
    [Display(Name = "Footer Left Zone")]
    public string? FooterLeftZone { get; set; }
    
    [Display(Name = "Footer Right Zone")]
    public string? FooterRightZone { get; set; }
    
    [Display(Name = "Active")]
    public bool IsEnabled { get; set; }
    
    public List<SelectListItem> MenuTypes { get; set; } = new();
    
    public bool IsNew => Id == 0;
    
    public bool IsFooterMenu => MenuType == 2; // MenuType.Footer = 2
}

/// <summary>
/// View model for menu preview
/// </summary>
public class MenuPreviewViewModel
{
    public string MenuMarkup { get; set; } = string.Empty;
    public string RenderedHtml { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Request model for checking active menu conflicts
/// </summary>
public class CheckConflictRequest
{
    public int MenuId { get; set; }
    public int MenuType { get; set; }
    public bool IsActive { get; set; }
}
