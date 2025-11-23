using System.ComponentModel.DataAnnotations;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Plugins;

using Squirrel.Wiki.Contracts.Authentication;

namespace Squirrel.Wiki.Web.Models.Admin;

/// <summary>
/// View model for user list page
/// </summary>
public class UserListViewModel : BaseViewModel
{
    public List<UserDto> Users { get; set; } = new();
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int PageSize { get; set; }
    public string? SearchTerm { get; set; }
    public string? RoleFilter { get; set; }
    public string? ProviderFilter { get; set; }
    public bool? IsActiveFilter { get; set; }
}

/// <summary>
/// View model for user details page
/// </summary>
public class UserDetailsViewModel : BaseViewModel
{
    public UserDto User { get; set; } = null!;
}

/// <summary>
/// View model for creating/editing users
/// </summary>
public class UserEditViewModel
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Username is required")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Display name is required")]
    [StringLength(100, ErrorMessage = "Display name cannot exceed 100 characters")]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
    public string? FirstName { get; set; }

    [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
    public string? LastName { get; set; }

    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
    public string? ConfirmPassword { get; set; }

    public bool IsActive { get; set; } = true;

    public List<string> Roles { get; set; } = new();

    public AuthenticationProvider Provider { get; set; }

    // Helper properties for view
    public bool IsNewUser => Id == Guid.Empty;
    public bool IsLocalUser => Provider == AuthenticationProvider.Local;
}

/// <summary>
/// View model for password reset
/// </summary>
public class ResetPasswordViewModel
{
    public Guid UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm the password")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
