using System.ComponentModel.DataAnnotations;

namespace Squirrel.Wiki.Web.Models;

public class ProfileViewModel
{
    public string UserId { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Display Name")]
    public string DisplayName { get; set; } = string.Empty;

    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }

    public bool IsEditor { get; set; }

    [Display(Name = "Authentication Provider")]
    public string Provider { get; set; } = string.Empty;

    [Display(Name = "Last Login")]
    public DateTime? LastLoginOn { get; set; }

    [Display(Name = "Account Created")]
    public DateTime CreatedOn { get; set; }

    [Display(Name = "Last Password Change")]
    public DateTime? LastPasswordChangeOn { get; set; }

    public IEnumerable<string> Roles { get; set; } = new List<string>();
}
