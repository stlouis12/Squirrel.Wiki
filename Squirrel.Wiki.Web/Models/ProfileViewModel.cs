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

    public bool IsAdmin { get; set; }

    public bool IsEditor { get; set; }

    public DateTime LastLoginOn { get; set; }

    public DateTime CreatedOn { get; set; }

    public IEnumerable<string> Roles { get; set; } = new List<string>();
}
