using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Security;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Service implementation for menu management operations
/// </summary>
public class MenuService : IMenuService
{
    private readonly IMenuRepository _menuRepository;
    private readonly IPageRepository _pageRepository;
    private readonly ICategoryService _categoryService;
    private readonly IMarkdownService _markdownService;
    private readonly IDistributedCache _cache;
    private readonly IUserContext _userContext;
    private readonly ILogger<MenuService> _logger;
    private const string CacheKeyPrefix = "menu:";
    private const string CacheKeyActive = "menu:active";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(30);

    // Menu tokens that can be used in menu markup
    private static readonly Dictionary<string, string> URL_TOKENS = new(StringComparer.OrdinalIgnoreCase)
    {
        { "%ALLPAGES%", "/Pages/AllPages" },
        { "%ALLTAGS%", "/Pages/AllTags" },
        { "%ALLCATEGORIES%", "/Pages/Category" },
        { "%SEARCH%", "/Search" },
        { "%NEWPAGE%", "/Pages/New" },
        { "%HOME%", "/Home" },
        { "%CATEGORIES%", "/Categories" },
        { "%SITEMAP%", "/Sitemap" },
        { "%RECENTLYUPDATED%", "/Pages/RecentlyUpdated" },
        { "%ADMIN%", "/Admin" }
    };

    public MenuService(
        IMenuRepository menuRepository,
        IPageRepository pageRepository,
        ICategoryService categoryService,
        IMarkdownService markdownService,
        IDistributedCache cache,
        IUserContext userContext,
        ILogger<MenuService> logger)
    {
        _menuRepository = menuRepository;
        _pageRepository = pageRepository;
        _categoryService = categoryService;
        _markdownService = markdownService;
        _cache = cache;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task<MenuDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{id}";
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            return JsonSerializer.Deserialize<MenuDto>(cached);
        }

        var menu = await _menuRepository.GetByIdAsync(id, cancellationToken);
        if (menu == null)
        {
            return null;
        }

        var dto = MapToDto(menu);
        
        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(dto),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheExpiration },
            cancellationToken);

        return dto;
    }

    public async Task<MenuDto?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var menu = await _menuRepository.GetByNameAsync(name, cancellationToken);
        return menu != null ? MapToDto(menu) : null;
    }

    public async Task<MenuDto?> GetActiveMenuByTypeAsync(MenuType menuType, CancellationToken cancellationToken = default)
    {
        var menu = await _menuRepository.GetActiveByTypeAsync(menuType, cancellationToken);
        return menu != null ? MapToDto(menu) : null;
    }

    public async Task<IEnumerable<MenuDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var menus = await _menuRepository.GetAllAsync(cancellationToken);
        return menus.Select(MapToDto).OrderBy(m => m.DisplayOrder);
    }

    public async Task<IEnumerable<MenuDto>> GetActiveMenusAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetStringAsync(CacheKeyActive, cancellationToken);
        
        if (cached != null)
        {
            return JsonSerializer.Deserialize<List<MenuDto>>(cached) ?? new List<MenuDto>();
        }

        var menus = await _menuRepository.GetAllAsync(cancellationToken);
        var dtos = menus
            .Where(m => m.IsEnabled)
            .Select(MapToDto)
            .OrderBy(m => m.DisplayOrder)
            .ToList();
        
        await _cache.SetStringAsync(
            CacheKeyActive,
            JsonSerializer.Serialize(dtos),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheExpiration },
            cancellationToken);

        return dtos;
    }

    public async Task<MenuDto> CreateAsync(MenuCreateDto createDto, bool forceActivation = false, CancellationToken cancellationToken = default)
    {
        var menuType = (MenuType)createDto.MenuType;
        var isEnabled = createDto.IsEnabled;
        
        // Check if activating and another menu of same type is already active
        if (createDto.IsEnabled)
        {
            var hasActive = await _menuRepository.HasActiveMenuOfTypeAsync(menuType, null, cancellationToken);
            if (hasActive)
            {
                // Automatically set the new menu to inactive instead of throwing an error
                isEnabled = false;
                _logger.LogInformation("Another menu of type '{MenuType}' is already active. Creating new menu '{MenuName}' as inactive.", 
                    menuType, createDto.Name);
            }
        }

        var menu = new Menu
        {
                Name = createDto.Name,
                MenuType = menuType,
                Description = createDto.Description,
                Markup = createDto.MenuMarkup,
                FooterLeftZone = createDto.FooterLeftZone,
                FooterRightZone = createDto.FooterRightZone,
                DisplayOrder = createDto.DisplayOrder,
            IsEnabled = isEnabled,
            ModifiedOn = DateTime.UtcNow,
            ModifiedBy = createDto.ModifiedBy
        };

        await _menuRepository.AddAsync(menu, cancellationToken);

        _logger.LogInformation("Created menu {MenuName} (Type: {MenuType}) with ID {MenuId}, Active: {IsEnabled}", 
            menu.Name, menu.MenuType, menu.Id, menu.IsEnabled);

        await InvalidateCacheAsync(cancellationToken);

        return MapToDto(menu);
    }

    public async Task<MenuDto> UpdateAsync(int id, MenuUpdateDto updateDto, bool forceActivation = false, CancellationToken cancellationToken = default)
    {
        var menu = await _menuRepository.GetByIdAsync(id, cancellationToken);
        if (menu == null)
        {
            throw new InvalidOperationException($"Menu with ID {id} not found.");
        }

        var menuType = (MenuType)updateDto.MenuType;
        var isEnabled = updateDto.IsEnabled;
        
        // Check if activating and another menu of same type is already active
        if (updateDto.IsEnabled && !menu.IsEnabled)
        {
            var hasActive = await _menuRepository.HasActiveMenuOfTypeAsync(menuType, id, cancellationToken);
            if (hasActive)
            {
                // Automatically set the menu to inactive instead of throwing an error
                isEnabled = false;
                _logger.LogInformation("Another menu of type '{MenuType}' is already active. Keeping menu '{MenuName}' inactive.", 
                    menuType, updateDto.Name);
            }
        }

            menu.Name = updateDto.Name;
            menu.MenuType = menuType;
            menu.Description = updateDto.Description;
            menu.Markup = updateDto.MenuMarkup;
            menu.FooterLeftZone = updateDto.FooterLeftZone;
            menu.FooterRightZone = updateDto.FooterRightZone;
            menu.DisplayOrder = updateDto.DisplayOrder;
        menu.IsEnabled = isEnabled;
        menu.ModifiedOn = DateTime.UtcNow;
        menu.ModifiedBy = updateDto.ModifiedBy;

        await _menuRepository.UpdateAsync(menu, cancellationToken);

        _logger.LogInformation("Updated menu {MenuName} (Type: {MenuType}, ID: {MenuId}), Active: {IsEnabled}", 
            menu.Name, menu.MenuType, menu.Id, menu.IsEnabled);

        await InvalidateMenuCacheAsync(id, cancellationToken);

        return MapToDto(menu);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var menu = await _menuRepository.GetByIdAsync(id, cancellationToken);
        if (menu == null)
        {
            throw new InvalidOperationException($"Menu with ID {id} not found.");
        }

        await _menuRepository.DeleteAsync(menu, cancellationToken);

        _logger.LogInformation("Deleted menu {MenuName} (ID: {MenuId})", menu.Name, menu.Id);

        await InvalidateCacheAsync(cancellationToken);
    }

    public async Task ReorderAsync(Dictionary<int, int> menuOrders, CancellationToken cancellationToken = default)
    {
        foreach (var kvp in menuOrders)
        {
            var menu = await _menuRepository.GetByIdAsync(kvp.Key, cancellationToken);
            if (menu != null)
            {
                menu.DisplayOrder = kvp.Value;
                menu.ModifiedOn = DateTime.UtcNow;
                menu.ModifiedBy = "System"; // TODO: Get from current user context
                await _menuRepository.UpdateAsync(menu, cancellationToken);
            }
        }

        _logger.LogInformation("Reordered {Count} menus", menuOrders.Count);

        await InvalidateCacheAsync(cancellationToken);
    }

    public async Task<MenuStructureDto> ParseMenuMarkupAsync(string menuMarkup, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(menuMarkup))
        {
            return new MenuStructureDto();
        }

        // Convert markdown to HTML
        var html = await _markdownService.ToHtmlAsync(menuMarkup, cancellationToken);

        // Parse HTML into menu structure
        var structure = ParseHtmlToMenuStructure(html);

        return structure;
    }

    public async Task<MenuStructureDto> ParseMenuMarkupWithoutResolvingAsync(string menuMarkup, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(menuMarkup))
        {
            return new MenuStructureDto();
        }

        // Parse menu markup directly without converting to HTML or resolving URLs
        var items = ParseMenuMarkup(menuMarkup);

        // Convert to MenuStructureDto
        var structure = new MenuStructureDto
        {
            Items = ConvertToMenuItemDtos(items)
        };

        return await Task.FromResult(structure);
    }

    private List<MenuItemDto> ConvertToMenuItemDtos(List<MenuMarkupItem> items)
    {
        var result = new List<MenuItemDto>();

        foreach (var item in items)
        {
            var dto = new MenuItemDto
            {
                Text = item.Text,
                Url = item.Url ?? string.Empty,
                Children = ConvertToMenuItemDtos(item.Children)
            };
            result.Add(dto);
        }

        return result;
    }

    public async Task<string> RenderMenuAsync(int menuId, CancellationToken cancellationToken = default)
    {
        var menu = await _menuRepository.GetByIdAsync(menuId, cancellationToken);
        if (menu == null || !menu.IsEnabled)
        {
            return string.Empty;
        }

        return await RenderMenuMarkupAsNavbarAsync(menu.Markup, cancellationToken);
    }

    public async Task<string> RenderMenuMarkupAsync(string menuMarkup, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(menuMarkup))
        {
            return string.Empty;
        }

        // Parse and process URLs
        var items = ParseMenuMarkup(menuMarkup);
        await ProcessMenuItemUrlsAsync(items, cancellationToken);

        // Convert to HTML
        var html = await _markdownService.ToHtmlAsync(menuMarkup, cancellationToken);

        // Clean up HTML (remove empty elements, etc.)
        html = CleanupMenuHtml(html);

        return html;
    }

    /// <summary>
    /// Renders menu markup as Bootstrap navbar-compatible HTML
    /// </summary>
    public async Task<string> RenderMenuMarkupAsNavbarAsync(string menuMarkup, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(menuMarkup))
        {
            return string.Empty;
        }

        // Parse MenuMarkup into structured items
        var items = ParseMenuMarkup(menuMarkup);

        // Process URLs (resolve tokens and slugs)
        await ProcessMenuItemUrlsAsync(items, cancellationToken);

        // Render as Bootstrap navbar HTML
        return RenderNavbarItems(items);
    }

    private List<MenuMarkupItem> ParseMenuMarkup(string menuMarkup)
    {
        var items = new List<MenuMarkupItem>();
        var lines = menuMarkup.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var stack = new Stack<MenuMarkupItem>();

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimEnd();
            if (string.IsNullOrWhiteSpace(trimmedLine))
                continue;

            // Count asterisks to determine level
            var level = 0;
            while (level < trimmedLine.Length && trimmedLine[level] == '*')
            {
                level++;
            }

            if (level == 0)
                continue;

            // Extract content after asterisks
            var content = trimmedLine.Substring(level).Trim();
            
            // Try to match [text](url) first
            var linkMatch = Regex.Match(content, @"\[([^\]]+)\]\(([^\)]+)\)");
            
            string text;
            string? url = null;
            
            if (linkMatch.Success)
            {
                // Has URL - regular link
                text = linkMatch.Groups[1].Value;
                url = linkMatch.Groups[2].Value;
            }
            else
            {
                // Try to match [text] without URL - dropdown header
                var headerMatch = Regex.Match(content, @"\[([^\]]+)\]");
                if (!headerMatch.Success)
                    continue;
                    
                text = headerMatch.Groups[1].Value;
                // url remains null - will be determined by whether it has children
            }

            var item = new MenuMarkupItem
            {
                Text = text,
                Url = url,
                Level = level,
                Children = new List<MenuMarkupItem>()
            };

            // Handle nesting
            while (stack.Count > 0 && stack.Peek().Level >= level)
            {
                stack.Pop();
            }

            if (stack.Count > 0)
            {
                stack.Peek().Children.Add(item);
            }
            else
            {
                items.Add(item);
            }

            stack.Push(item);
        }

        return items;
    }

    private string RenderNavbarItems(List<MenuMarkupItem> items)
    {
        var html = new System.Text.StringBuilder();

        foreach (var item in items)
        {
            // Check for %EMBEDDED_SEARCH% token
            if (item.Url != null && item.Url.Equals("%EMBEDDED_SEARCH%", StringComparison.OrdinalIgnoreCase))
            {
                // Render embedded search box in navbar
                html.AppendLine($"<li class=\"nav-item\">");
                html.AppendLine($"    <form action=\"/Search\" method=\"get\" class=\"d-flex ms-2\">");
                html.AppendLine($"        <input type=\"text\" name=\"query\" class=\"form-control form-control-sm me-2\" placeholder=\"{System.Net.WebUtility.HtmlEncode(item.Text)}\" aria-label=\"Search\" style=\"width: 200px;\">");
                html.AppendLine($"        <button class=\"btn btn-outline-secondary btn-sm\" type=\"submit\"><i class=\"bi bi-search\"></i></button>");
                html.AppendLine($"    </form>");
                html.AppendLine($"</li>");
                continue;
            }
            
            // Item without URL and with children = dropdown
            if (item.Url == null && item.Children.Any())
            {
                // Render as dropdown with non-clickable header
                var dropdownId = $"dropdown-{Guid.NewGuid():N}";
                html.AppendLine($"<li class=\"nav-item dropdown\">");
                html.AppendLine($"    <a class=\"nav-link dropdown-toggle\" href=\"#\" id=\"{dropdownId}\" role=\"button\" data-bs-toggle=\"dropdown\" aria-expanded=\"false\">");
                html.AppendLine($"        {System.Net.WebUtility.HtmlEncode(item.Text)}");
                html.AppendLine($"    </a>");
                html.AppendLine($"    <ul class=\"dropdown-menu\" aria-labelledby=\"{dropdownId}\">");
                
                foreach (var child in item.Children)
                {
                    if (child.Url != null)
                    {
                        html.AppendLine($"        <li><a class=\"dropdown-item\" href=\"{System.Net.WebUtility.HtmlEncode(child.Url)}\">{System.Net.WebUtility.HtmlEncode(child.Text)}</a></li>");
                    }
                }
                
                html.AppendLine($"    </ul>");
                html.AppendLine($"</li>");
            }
            else if (item.Url != null)
            {
                // Render as simple nav item (has URL)
                html.AppendLine($"<li class=\"nav-item\">");
                html.AppendLine($"    <a class=\"nav-link\" href=\"{System.Net.WebUtility.HtmlEncode(item.Url)}\">{System.Net.WebUtility.HtmlEncode(item.Text)}</a>");
                html.AppendLine($"</li>");
            }
            // Items without URL and without children are ignored
        }

        return html.ToString();
    }

    private class MenuMarkupItem
    {
        public string Text { get; set; } = string.Empty;
        public string? Url { get; set; }
        public int Level { get; set; }
        public List<MenuMarkupItem> Children { get; set; } = new();
    }

    public async Task<MenuValidationResult> ValidateMenuMarkupAsync(string menuMarkup, CancellationToken cancellationToken = default)
    {
        var result = new MenuValidationResult { IsValid = true };

        if (string.IsNullOrWhiteSpace(menuMarkup))
        {
            result.Warnings.Add("Menu markup is empty.");
            return result;
        }

        try
        {
            // Try to parse menu markup
            var items = ParseMenuMarkup(menuMarkup);

            // Check for common issues
            if (menuMarkup.Length > 10000)
            {
                result.Warnings.Add("Menu markup is very large (>10KB). Consider simplifying.");
            }

            // Check for valid token usage
            foreach (var tokenKey in URL_TOKENS.Keys)
            {
                if (menuMarkup.Contains(tokenKey, StringComparison.OrdinalIgnoreCase) && !menuMarkup.Contains(tokenKey))
                {
                    result.Warnings.Add($"Token '{tokenKey}' has incorrect casing. Tokens are case-insensitive but use uppercase for consistency.");
                }
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Failed to parse menu markup: {ex.Message}");
        }

        return result;
    }

    public async Task<MenuDto?> GetMainNavigationMenuAsync(CancellationToken cancellationToken = default)
    {
        // Look for a menu specifically named "main-navigation"
        var menu = await _menuRepository.GetByNameAsync("main-navigation", cancellationToken);
        
        // Only return if it exists and is enabled
        if (menu != null && menu.IsEnabled)
        {
            return MapToDto(menu);
        }
        
        return null;
    }

    public async Task ActivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var menu = await _menuRepository.GetByIdAsync(id, cancellationToken);
        if (menu == null)
        {
            throw new InvalidOperationException($"Menu with ID {id} not found.");
        }

        // Check if another menu of same type is already active
        var hasActive = await _menuRepository.HasActiveMenuOfTypeAsync(menu.MenuType, id, cancellationToken);
        if (hasActive)
        {
            // Deactivate other menus of the same type
            var otherMenus = await _menuRepository.GetByTypeAsync(menu.MenuType, cancellationToken);
            foreach (var otherMenu in otherMenus.Where(m => m.Id != id && m.IsEnabled))
            {
                otherMenu.IsEnabled = false;
                otherMenu.ModifiedOn = DateTime.UtcNow;
                otherMenu.ModifiedBy = _userContext.Username ?? "System";
                await _menuRepository.UpdateAsync(otherMenu, cancellationToken);
                _logger.LogInformation("Auto-deactivated menu {MenuName} (ID: {MenuId}) to activate {NewMenuName}", 
                    otherMenu.Name, otherMenu.Id, menu.Name);
            }
        }

        menu.IsEnabled = true;
        menu.ModifiedOn = DateTime.UtcNow;
        menu.ModifiedBy = _userContext.Username ?? "System";
        await _menuRepository.UpdateAsync(menu, cancellationToken);

        _logger.LogInformation("Activated menu {MenuName} (Type: {MenuType}, ID: {MenuId})", menu.Name, menu.MenuType, menu.Id);

        await InvalidateMenuCacheAsync(id, cancellationToken);
    }

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var menu = await _menuRepository.GetByIdAsync(id, cancellationToken);
        if (menu == null)
        {
            throw new InvalidOperationException($"Menu with ID {id} not found.");
        }

        menu.IsEnabled = false;
        menu.ModifiedOn = DateTime.UtcNow;
        menu.ModifiedBy = "System"; // TODO: Get from current user context
        await _menuRepository.UpdateAsync(menu, cancellationToken);

        _logger.LogInformation("Deactivated menu {MenuName} (ID: {MenuId})", menu.Name, menu.Id);

        await InvalidateMenuCacheAsync(id, cancellationToken);
    }

    /// <summary>
    /// Process URLs in menu items - resolve tokens and page slugs
    /// </summary>
    private async Task ProcessMenuItemUrlsAsync(List<MenuMarkupItem> items, CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            if (item.Url != null)
            {
                item.Url = await ResolveUrlAsync(item.Url, cancellationToken);
            }

            if (item.Children.Any())
            {
                await ProcessMenuItemUrlsAsync(item.Children, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Resolve a URL - handle tokens, category/tag links, and page slugs
    /// PHASE 8.2: Enhanced to support nested category paths (e.g., category:documentation:gettingstarted:installation)
    /// </summary>
    private async Task<string> ResolveUrlAsync(string url, CancellationToken cancellationToken)
    {
        // Check if it's a token (e.g., %ALLPAGES%)
        if (URL_TOKENS.TryGetValue(url, out var tokenUrl))
        {
            return tokenUrl;
        }

        // Check if it's already a full path (starts with / or http)
        if (url.StartsWith("/") || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        // PHASE 8.2: Enhanced category: prefix support for nested paths
        // Supports: category:documentation (simple)
        //           category:documentation:gettingstarted (nested)
        //           category:documentation:gettingstarted:installation (deeply nested)
        if (url.StartsWith("category:", StringComparison.OrdinalIgnoreCase))
        {
            var categoryPath = url.Substring("category:".Length).Trim();
            
            try
            {
                // Try to resolve the category by path using the new Phase 8.1 service method
                var category = await _categoryService.GetByPathAsync(categoryPath, cancellationToken);
                
                if (category != null)
                {
                    // Use category ID for reliable navigation
                    _logger.LogDebug("Resolved category path '{CategoryPath}' (ID: {CategoryId}) to URL '/Pages/Category?id={CategoryId}'", 
                        categoryPath, category.Id, category.Id);
                    
                    return $"/Pages/Category?id={category.Id}";
                }
                else
                {
                    // Category not found - log warning and return fallback URL using category name
                    _logger.LogWarning("Category path '{CategoryPath}' not found in menu markup. Using fallback URL with category name.", categoryPath);
                    
                    // Use the last segment of the path as the category name for fallback
                    var categoryName = categoryPath.Split(':', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? categoryPath;
                    return $"/Pages/Category?categoryName={Uri.EscapeDataString(categoryName)}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve category path '{CategoryPath}' in menu", categoryPath);
                
                // Return fallback URL on error using category name
                var categoryName = categoryPath.Split(':', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? categoryPath;
                return $"/Pages/Category?categoryName={Uri.EscapeDataString(categoryName)}";
            }
        }

        // Check for tag: prefix (e.g., tag:tutorial)
        if (url.StartsWith("tag:", StringComparison.OrdinalIgnoreCase))
        {
            var tagName = url.Substring("tag:".Length).Trim();
            return $"/Pages/Tag?tagName={Uri.EscapeDataString(tagName)}";
        }

        // Assume it's a page slug - try to resolve it
        try
        {
            var page = await _pageRepository.GetBySlugAsync(url, cancellationToken);
            if (page != null)
            {
                return $"/wiki/{page.Id}/{page.Slug}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve page slug '{Slug}' in menu", url);
        }

        // If we can't resolve it, return as-is (might be an external link or typo)
        return url;
    }

    private string CleanupMenuHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        // Remove empty list items
        html = Regex.Replace(html, @"<li>\s*</li>", "", RegexOptions.IgnoreCase);
        
        // Remove empty unordered lists
        html = Regex.Replace(html, @"<ul>\s*</ul>", "", RegexOptions.IgnoreCase);
        
        // Remove empty paragraphs
        html = Regex.Replace(html, @"<p>\s*</p>", "", RegexOptions.IgnoreCase);

        // Trim whitespace
        html = html.Trim();

        return html;
    }

    private MenuStructureDto ParseHtmlToMenuStructure(string html)
    {
        var structure = new MenuStructureDto();

        // Simple parsing - look for <ul> and <li> tags
        // This is a basic implementation; a more robust solution would use an HTML parser
        var ulMatch = Regex.Match(html, @"<ul>(.*?)</ul>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (ulMatch.Success)
        {
            var ulContent = ulMatch.Groups[1].Value;
            var liMatches = Regex.Matches(ulContent, @"<li>(.*?)</li>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match liMatch in liMatches)
            {
                var liContent = liMatch.Groups[1].Value;
                var item = ParseMenuItem(liContent);
                if (item != null)
                {
                    structure.Items.Add(item);
                }
            }
        }

        return structure;
    }

    private MenuItemDto? ParseMenuItem(string content)
    {
        // Extract link and text from <a href="url">text</a>
        var linkMatch = Regex.Match(content, @"<a\s+href=[""']([^""']+)[""']>([^<]+)</a>", RegexOptions.IgnoreCase);
        if (linkMatch.Success)
        {
            return new MenuItemDto
            {
                Url = linkMatch.Groups[1].Value,
                Text = linkMatch.Groups[2].Value
            };
        }

        return null;
    }

    private async Task InvalidateCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Remove active menus cache
            await _cache.RemoveAsync(CacheKeyActive, cancellationToken);
            
            // Note: We can't easily remove all individual menu caches (menu:{id})
            // because distributed cache doesn't support pattern-based removal.
            // Individual menu caches will expire naturally after 30 minutes,
            // or we could track menu IDs and remove them individually.
            // For now, we'll clear the active cache which is most important.
            
            _logger.LogDebug("Invalidated menu cache");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate menu cache");
        }
    }

    private async Task InvalidateMenuCacheAsync(int menuId, CancellationToken cancellationToken)
    {
        try
        {
            // Remove specific menu cache
            var cacheKey = $"{CacheKeyPrefix}{menuId}";
            await _cache.RemoveAsync(cacheKey, cancellationToken);
            
            // Also remove active menus cache
            await _cache.RemoveAsync(CacheKeyActive, cancellationToken);
            
            _logger.LogDebug("Invalidated cache for menu {MenuId}", menuId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate cache for menu {MenuId}", menuId);
        }
    }

    public async Task<bool> HasActiveMenuOfTypeAsync(MenuType menuType, int? excludeMenuId = null, CancellationToken cancellationToken = default)
    {
        return await _menuRepository.HasActiveMenuOfTypeAsync(menuType, excludeMenuId, cancellationToken);
    }

    private static MenuDto MapToDto(Menu menu)
    {
        return new MenuDto
        {
            Id = menu.Id,
            Name = menu.Name,
            MenuType = (int)menu.MenuType,
            Description = menu.Description,
            MenuMarkup = menu.Markup,
            FooterLeftZone = menu.FooterLeftZone,
            FooterRightZone = menu.FooterRightZone,
            DisplayOrder = menu.DisplayOrder,
            IsEnabled = menu.IsEnabled,
            ModifiedOn = menu.ModifiedOn,
            ModifiedBy = menu.ModifiedBy
        };
    }
}
