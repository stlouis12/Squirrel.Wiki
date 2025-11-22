using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Services;
using Squirrel.Wiki.Web.Models;

namespace Squirrel.Wiki.Web.ViewComponents;

/// <summary>
/// View component for rendering sidebar navigation with enhanced support for special tokens
/// </summary>
public class SidebarNavigationViewComponent : ViewComponent
{
    private readonly IMenuService _menuService;
    private readonly ITagService _tagService;
    private readonly ICategoryService _categoryService;
    private readonly IPageService _pageService;
    private readonly Squirrel.Wiki.Core.Security.IAuthorizationService _authorizationService;
    private readonly IPageRepository _pageRepository;
    private readonly IUrlTokenResolver _urlTokenResolver;
    private readonly ILogger<SidebarNavigationViewComponent> _logger;

    public SidebarNavigationViewComponent(
        IMenuService menuService,
        ITagService tagService,
        ICategoryService categoryService,
        IPageService pageService,
        Squirrel.Wiki.Core.Security.IAuthorizationService authorizationService,
        IPageRepository pageRepository,
        IUrlTokenResolver urlTokenResolver,
        ILogger<SidebarNavigationViewComponent> logger)
    {
        _menuService = menuService;
        _tagService = tagService;
        _categoryService = categoryService;
        _pageService = pageService;
        _authorizationService = authorizationService;
        _pageRepository = pageRepository;
        _urlTokenResolver = urlTokenResolver;
        _logger = logger;
    }

    /// <summary>
    /// Renders the active sidebar navigation menu
    /// </summary>
    public async Task<IViewComponentResult> InvokeAsync()
    {
        try
        {
            // Get the active sidebar menu
            var sidebar = await _menuService.GetActiveMenuByTypeAsync(MenuType.Sidebar);
            
            // If no sidebar is active or enabled, return empty content
            if (sidebar == null || !sidebar.IsEnabled || string.IsNullOrWhiteSpace(sidebar.MenuMarkup))
            {
                return Content(string.Empty);
            }

            // Parse the menu markup into menu structure WITHOUT resolving URLs
            // This preserves special tokens like %ALLTAGS% and %ALLCATEGORIES% for us to handle
            var menuStructure = await _menuService.ParseMenuMarkupWithoutResolvingAsync(sidebar.MenuMarkup);
            
            // Convert to enhanced sidebar view model
            var viewModel = await BuildSidebarViewModelAsync(menuStructure.Items);
            
            // Return the default view with the enhanced view model
            return View("Default", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering sidebar navigation");
            return Content(string.Empty);
        }
    }

    /// <summary>
    /// Builds the sidebar view model from menu items, handling special tokens
    /// </summary>
    private async Task<SidebarViewModel> BuildSidebarViewModelAsync(List<MenuItemDto> menuItems)
    {
        var viewModel = new SidebarViewModel();

        foreach (var item in menuItems)
        {
            var sidebarItem = await ConvertToSidebarItemAsync(item);
            if (sidebarItem != null)
            {
                viewModel.Items.Add(sidebarItem);
            }
        }

        return viewModel;
    }

    /// <summary>
    /// Converts a menu item to a sidebar item, handling special tokens
    /// </summary>
    private async Task<SidebarItemViewModel?> ConvertToSidebarItemAsync(MenuItemDto menuItem)
    {
        // Check for special tokens
        if (menuItem.Url != null)
        {
            // Check authorization for restricted tokens
            if (!ShouldIncludeMenuItem(menuItem.Url))
            {
                return null; // Skip this item if user doesn't have permission
            }

            // Handle %ALLTAGS% token
            if (menuItem.Url.Equals("%ALLTAGS%", StringComparison.OrdinalIgnoreCase))
            {
                return await BuildAllTagsItemAsync(menuItem.Text);
            }

            // Handle %ALLCATEGORIES% token
            if (menuItem.Url.Equals("%ALLCATEGORIES%", StringComparison.OrdinalIgnoreCase))
            {
                return await BuildAllCategoriesItemAsync(menuItem.Text);
            }

            // Handle %EMBEDDED_SEARCH% token
            if (menuItem.Url.Equals("%EMBEDDED_SEARCH%", StringComparison.OrdinalIgnoreCase)
                || menuItem.Url.Equals("%SEARCH%", StringComparison.OrdinalIgnoreCase))
            {
                return BuildEmbeddedSearchItemAsync(menuItem.Text);
            }

            // Handle other standard tokens by resolving them
            if ((menuItem.Url.StartsWith("%") && menuItem.Url.EndsWith("%")) || menuItem.Url.StartsWith("tag:") || menuItem.Url.StartsWith("category:"))
            {
                // Resolve standard URL tokens
                var resolvedUrl = ResolveUrlToken(menuItem.Url);
                if (resolvedUrl != null)
                {
                    return new SidebarItemViewModel
                    {
                        Text = menuItem.Text,
                        Url = resolvedUrl,
                        Type = SidebarItemType.Link
                    };
                }
            }
        }

        // Determine item type based on URL and children
        SidebarItemType itemType;
        if (menuItem.Url == null && menuItem.Children.Any())
        {
            // No URL but has children = collapsible header
            itemType = SidebarItemType.Header;
        }
        else if (menuItem.Url != null)
        {
            // Has URL = link (even if it has children, which shouldn't normally happen)
            itemType = SidebarItemType.Link;
        }
        else
        {
            // No URL and no children = treat as non-clickable text (shouldn't normally happen)
            itemType = SidebarItemType.Header;
        }

        var sidebarItem = new SidebarItemViewModel
        {
            Text = menuItem.Text,
            Url = menuItem.Url,
            Type = itemType
        };

        // Process children recursively
        foreach (var child in menuItem.Children)
        {
            var childItem = await ConvertToSidebarItemAsync(child);
            if (childItem != null)
            {
                sidebarItem.Children.Add(childItem);
            }
        }

        return sidebarItem;
    }

    /// <summary>
    /// Builds a sidebar item for the %ALLTAGS% token
    /// </summary>
    private async Task<SidebarItemViewModel> BuildAllTagsItemAsync(string headerText)
    {
        var item = new SidebarItemViewModel
        {
            Text = headerText,
            Type = SidebarItemType.AllTags,
            Url = null
        };

        try
        {
            // Get all tags
            var tags = await _tagService.GetAllTagsAsync();
            
            // For each tag, count only the pages the user can see
            foreach (var tag in tags.OrderBy(t => t.Name))
            {
                var tagPages = await _pageService.GetPagesByTagAsync(tag.Name);
                
                // Filter pages based on authorization
                var authorizedCount = 0;
                foreach (var page in tagPages)
                {
                    var pageEntity = await _pageRepository.GetByIdAsync(page.Id);
                    if (pageEntity != null && await _authorizationService.CanViewPageAsync(pageEntity))
                    {
                        authorizedCount++;
                    }
                }
                
                // Only include tags that have at least one visible page
                if (authorizedCount > 0)
                {
                    item.Children.Add(new SidebarItemViewModel
                    {
                        Text = $"{tag.Name} ({authorizedCount})",
                        Url = $"/Pages/AllPages?tag={Uri.EscapeDataString(tag.Name)}",
                        Type = SidebarItemType.Link
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading tags for sidebar");
        }

        return item;
    }

    /// <summary>
    /// Builds a sidebar item for the %ALLCATEGORIES% token
    /// </summary>
    private async Task<SidebarItemViewModel> BuildAllCategoriesItemAsync(string headerText)
    {
        var item = new SidebarItemViewModel
        {
            Text = headerText,
            Type = SidebarItemType.AllCategories,
            Url = null
        };

        try
        {
            // Get the category tree
            var categoryTree = await _categoryService.GetCategoryTreeAsync();
            
            // Convert category tree to sidebar items with filtered counts
            foreach (var category in categoryTree)
            {
                var categoryItem = await ConvertCategoryTreeToSidebarItemAsync(category);
                item.Children.Add(categoryItem);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading categories for sidebar");
        }

        return item;
    }

    /// <summary>
    /// Recursively converts a category tree to sidebar items with authorization-filtered counts
    /// </summary>
    private async Task<SidebarItemViewModel> ConvertCategoryTreeToSidebarItemAsync(CategoryTreeDto category)
    {
        // Get pages in this category and count only those the user can see
        var categoryPages = await _pageService.GetByCategoryAsync(category.Id);
        var authorizedCount = 0;
        
        foreach (var page in categoryPages)
        {
            var pageEntity = await _pageRepository.GetByIdAsync(page.Id);
            if (pageEntity != null && await _authorizationService.CanViewPageAsync(pageEntity))
            {
                authorizedCount++;
            }
        }
        
        var item = new SidebarItemViewModel
        {
            Text = authorizedCount > 0 
                ? $"{category.Name} ({authorizedCount})" 
                : category.Name,
            Url = $"/Pages/AllPages?categoryId={category.Id}",
            Type = category.Children.Any() ? SidebarItemType.Header : SidebarItemType.Link
        };

        // Process children recursively
        foreach (var child in category.Children)
        {
            item.Children.Add(await ConvertCategoryTreeToSidebarItemAsync(child));
        }

        return item;
    }

    /// <summary>
    /// Builds a sidebar item for the %EMBEDDED_SEARCH% token
    /// </summary>
    private SidebarItemViewModel BuildEmbeddedSearchItemAsync(string headerText)
    {
        return new SidebarItemViewModel
        {
            Text = headerText,
            Type = SidebarItemType.EmbeddedSearch,
            Url = null
        };
    }

    /// <summary>
    /// Resolves standard URL tokens to their actual URLs - delegates to shared UrlTokenResolver service
    /// </summary>
    private string? ResolveUrlToken(string token)
    {
        // Use the shared URL token resolver service
        // Note: This is a synchronous wrapper around an async method
        // In a view component context, we need to handle this carefully
        var resolved = _urlTokenResolver.ResolveTokenAsync(token).GetAwaiter().GetResult();
        return resolved;
    }

    /// <summary>
    /// Determines if a menu item should be included based on user authorization
    /// </summary>
    private bool ShouldIncludeMenuItem(string url)
    {
        var userRole = HttpContext.User.Identity?.IsAuthenticated == true 
            ? HttpContext.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value 
            : null;

        // Check for %ADMIN% token - only for Admin role
        if (url.Equals("%ADMIN%", StringComparison.OrdinalIgnoreCase))
        {
            return userRole?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;
        }

        // Check for %NEWPAGE% token - only for Admin and Editor roles
        if (url.Equals("%NEWPAGE%", StringComparison.OrdinalIgnoreCase))
        {
            return userRole?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
                   userRole?.Equals("Editor", StringComparison.OrdinalIgnoreCase) == true;
        }

        // All other items are visible to everyone
        return true;
    }
}
