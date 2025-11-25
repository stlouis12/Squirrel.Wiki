using Squirrel.Wiki.Plugins;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Contracts.Authentication;

namespace Squirrel.Wiki.Core.Models;

// ============================================================================
// USER DTOs
// ============================================================================

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsEditor { get; set; }
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public AuthenticationProvider Provider { get; set; }
    public List<string> Roles { get; set; } = new();
    public DateTime CreatedOn { get; set; }
    public DateTime? LastLoginOn { get; set; }
    public DateTime? LastPasswordChangeOn { get; set; }
}

public class UserCreateDto
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsEditor { get; set; }
}

public class UserUpdateDto
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsEditor { get; set; }
}

public class UserStatsDto
{
    public Guid UserId { get; set; }
    public int PagesCreated { get; set; }
    public int PagesEdited { get; set; }
    public int TotalEdits { get; set; }
    public DateTime? LastEditDate { get; set; }
}

// ============================================================================
// CATEGORY DTOs
// ============================================================================

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentId { get; set; }
    public int? ParentCategoryId { get; set; } // Deprecated - use ParentId
    public string? ParentName { get; set; }
    public int PageCount { get; set; }
    public int Level { get; set; }
    public int Depth { get; set; } // Deprecated - use Level
    public string FullPath { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty; // Deprecated - use FullPath
    public int DisplayOrder { get; set; }
    public DateTime CreatedOn { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime ModifiedOn { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
}

public class CategoryCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentId { get; set; }
    public int? ParentCategoryId { get; set; } // Deprecated - use ParentId
    public string CreatedBy { get; set; } = string.Empty;
}

public class CategoryUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentId { get; set; }
    public int? ParentCategoryId { get; set; } // Deprecated - use ParentId
    public string ModifiedBy { get; set; } = string.Empty;
}

public class CategoryTreeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PageCount { get; set; }
    public List<CategoryTreeDto> Children { get; set; } = new();
}

// ============================================================================
// TAG DTOs
// ============================================================================

public class TagDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class TagWithCountDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PageCount { get; set; }
}

public class TagCloudItemDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public int Weight { get; set; } // 1-10 for sizing
}

public class TagStatsDto
{
    public int TotalTags { get; set; }
    public int TotalTaggedPages { get; set; }
    public int UnusedTags { get; set; }
    public double AverageTagsPerPage { get; set; }
}

// ============================================================================
// MENU DTOs
// ============================================================================

public class MenuDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MenuType { get; set; }
    public string? Description { get; set; }
    public string? MenuMarkup { get; set; }
    public string? FooterLeftZone { get; set; }
    public string? FooterRightZone { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime ModifiedOn { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
}

public class MenuCreateDto
{
    public string Name { get; set; } = string.Empty;
    public int MenuType { get; set; }
    public string? Description { get; set; }
    public string? MenuMarkup { get; set; }
    public string? FooterLeftZone { get; set; }
    public string? FooterRightZone { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string ModifiedBy { get; set; } = string.Empty;
}

public class MenuUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public int MenuType { get; set; }
    public string? Description { get; set; }
    public string? MenuMarkup { get; set; }
    public string? FooterLeftZone { get; set; }
    public string? FooterRightZone { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsEnabled { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
}

public class MenuStructureDto
{
    public List<MenuItemDto> Items { get; set; } = new();
}

public class MenuItemDto
{
    public string Text { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public List<MenuItemDto> Children { get; set; } = new();
}

public class MenuValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

// ============================================================================
// SEARCH DTOs
// ============================================================================

public class SearchResultsDto
{
    public string Query { get; set; } = string.Empty;
    public int TotalResults { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<SearchResultItemDto> Results { get; set; } = new();
    public Dictionary<string, int> Facets { get; set; } = new();
}

public class SearchResultItemDto
{
    public int PageId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public float Score { get; set; }
    public DateTime ModifiedOn { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string? CategoryName { get; set; }
}

public class SearchQueryDto
{
    public string Query { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public List<string>? Tags { get; set; }
    public int? CategoryId { get; set; }
    public string? Author { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IncludeContent { get; set; } = false;
}

public class SearchIndexStatsDto
{
    public int TotalDocuments { get; set; }
    public long IndexSizeBytes { get; set; }
    public DateTime? LastOptimized { get; set; }
    public DateTime? LastIndexed { get; set; }
    public bool IsValid { get; set; }
}

// ============================================================================
// PASSWORD VALIDATION DTOs
// ============================================================================

/// <summary>
/// Result of password validation
/// </summary>
public class PasswordValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();

    public static PasswordValidationResult Success()
    {
        return new PasswordValidationResult { IsValid = true };
    }

    public static PasswordValidationResult Failed(params string[] errors)
    {
        return new PasswordValidationResult
        {
            IsValid = false,
            Errors = errors.ToList()
        };
    }
}
