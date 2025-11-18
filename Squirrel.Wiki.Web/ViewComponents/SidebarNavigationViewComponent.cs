using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Models;
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
    private readonly ILogger<SidebarNavigationViewComponent> _logger;

    public SidebarNavigationViewComponent(
        IMenuService menuService,
        ITagService tagService,
        ICategoryService categoryService,
        ILogger<SidebarNavigationViewComponent> logger)
    {
        _menuService = menuService;
        _tagService = tagService;
        _categoryService = categoryService;
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
            if (menuItem.Url.Equals("%EMBEDDED_SEARCH%", StringComparison.OrdinalIgnoreCase))
            {
                return BuildEmbeddedSearchItemAsync(menuItem.Text);
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
            // Get all tags with counts
            var tags = await _tagService.GetAllWithCountsAsync();
            
            // Only include tags that have pages
            foreach (var tag in tags.Where(t => t.PageCount > 0).OrderBy(t => t.Name))
            {
                item.Children.Add(new SidebarItemViewModel
                {
                    Text = $"{tag.Name} ({tag.PageCount})",
                    Url = $"/Pages/Tag?tagName={Uri.EscapeDataString(tag.Name)}",
                    Type = SidebarItemType.Link
                });
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
            
            // Convert category tree to sidebar items
            foreach (var category in categoryTree)
            {
                var categoryItem = ConvertCategoryTreeToSidebarItem(category);
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
    /// Recursively converts a category tree to sidebar items
    /// </summary>
    private SidebarItemViewModel ConvertCategoryTreeToSidebarItem(CategoryTreeDto category)
    {
        var item = new SidebarItemViewModel
        {
            Text = category.PageCount > 0 
                ? $"{category.Name} ({category.PageCount})" 
                : category.Name,
            Url = $"/Pages/Category?id={category.Id}",
            Type = category.Children.Any() ? SidebarItemType.Header : SidebarItemType.Link
        };

        // Process children recursively
        foreach (var child in category.Children)
        {
            item.Children.Add(ConvertCategoryTreeToSidebarItem(child));
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
}
