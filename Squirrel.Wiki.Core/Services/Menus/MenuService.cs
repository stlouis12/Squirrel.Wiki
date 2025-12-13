using AutoMapper;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Events;
using Squirrel.Wiki.Core.Events.Menus;
using Squirrel.Wiki.Core.Exceptions;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Services.Categories;
using Squirrel.Wiki.Core.Services.Content;
using Squirrel.Wiki.Core.Services.Caching;
using static Squirrel.Wiki.Core.Constants.SystemUserConstants;

namespace Squirrel.Wiki.Core.Services.Menus;

/// <summary>
/// Service implementation for menu management operations
/// </summary>
public class MenuService : BaseService, IMenuService
{
    private readonly IMenuRepository _menuRepository;
    private readonly IMarkdownService _markdownService;
    private readonly IUserContext _userContext;
    private readonly IUrlTokenResolver _urlTokenResolver;
    
    
    private const string CacheKeyPrefix = "menu:";
    private const string CacheKeyActive = "menu:active";

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
        { "%RECENTLYUPDATED%", "/Pages/AllPages?recent=10" },
        { "%ADMIN%", "/Admin" },
        { "%FILEBROWSER%", "/Files" }
    };

    public MenuService(
        IMenuRepository menuRepository,
        IMarkdownService markdownService,
        IUserContext userContext,
        IUrlTokenResolver urlTokenResolver,
        IMapper mapper,
        ILogger<MenuService> logger,
        ICacheService cache,
        IEventPublisher eventPublisher,
        IConfigurationService configuration)
        : base(logger, cache, eventPublisher, mapper, configuration)
    {
        _menuRepository = menuRepository;
        _markdownService = markdownService;
        _userContext = userContext;
        _urlTokenResolver = urlTokenResolver;
        
    }

    public async Task<MenuDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{id}";
        var cached = await Cache.GetAsync<MenuDto>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            LogDebug("Menu cache hit for key: {CacheKey}", cacheKey);
            return cached;
        }

        LogDebug("Menu cache miss for key: {CacheKey}", cacheKey);
        var menu = await _menuRepository.GetByIdAsync(id, cancellationToken);
        if (menu == null)
        {
            return null;
        }

        var dto = Mapper.Map<MenuDto>(menu);
        await Cache.SetAsync(cacheKey, dto, null, cancellationToken);

        return dto;
    }

    public async Task<MenuDto?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}name:{name}";
        var cached = await Cache.GetAsync<MenuDto>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            LogDebug("Menu cache hit for name: {MenuName}", name);
            return cached;
        }

        LogDebug("Menu cache miss for name: {MenuName}", name);
        var menu = await _menuRepository.GetByNameAsync(name, cancellationToken);
        
        if (menu == null)
        {
            return null;
        }

        var dto = Mapper.Map<MenuDto>(menu);
        await Cache.SetAsync(cacheKey, dto, null, cancellationToken);

        return dto;
    }

    public async Task<MenuDto?> GetActiveMenuByTypeAsync(MenuType menuType, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}type:{(int)menuType}";
        var cached = await Cache.GetAsync<MenuDto>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            LogDebug("Menu cache hit for type: {MenuType}", menuType);
            return cached;
        }

        LogDebug("Menu cache miss for type: {MenuType}", menuType);
        var menu = await _menuRepository.GetActiveByTypeAsync(menuType, cancellationToken);
        
        if (menu == null)
        {
            return null;
        }

        var dto = Mapper.Map<MenuDto>(menu);
        await Cache.SetAsync(cacheKey, dto, null, cancellationToken);

        return dto;
    }

    public async Task<IEnumerable<MenuDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var menus = await _menuRepository.GetAllAsync(cancellationToken);
        return Mapper.Map<IEnumerable<MenuDto>>(menus).OrderBy(m => m.DisplayOrder);
    }

    public async Task<IEnumerable<MenuDto>> GetActiveMenusAsync(CancellationToken cancellationToken = default)
    {
        var cached = await Cache.GetAsync<List<MenuDto>>(CacheKeyActive, cancellationToken);
        
        if (cached != null)
        {
            LogDebug("Active menus cache hit");
            return cached;
        }

        LogDebug("Active menus cache miss");
        var menus = await _menuRepository.GetAllAsync(cancellationToken);
        var dtos = Mapper.Map<List<MenuDto>>(menus.Where(m => m.IsEnabled))
            .OrderBy(m => m.DisplayOrder)
            .ToList();
        
        await Cache.SetAsync(CacheKeyActive, dtos, null, cancellationToken);

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
                LogInfo("Another menu of type '{MenuType}' is already active. Creating new menu '{MenuName}' as inactive.", 
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

        LogInfo("Created menu {MenuName} (Type: {MenuType}) with ID {MenuId}, Active: {IsEnabled}", 
            menu.Name, menu.MenuType, menu.Id, menu.IsEnabled);

        await InvalidateCacheAsync(cancellationToken);

        return Mapper.Map<MenuDto>(menu);
    }

    public async Task<MenuDto> UpdateAsync(int id, MenuUpdateDto updateDto, bool forceActivation = false, CancellationToken cancellationToken = default)
    {
        var menu = await _menuRepository.GetByIdAsync(id, cancellationToken);
        if (menu == null)
        {
            throw new EntityNotFoundException("Menu", id);
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
                LogInfo("Another menu of type '{MenuType}' is already active. Keeping menu '{MenuName}' inactive.", 
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

        LogInfo("Updated menu {MenuName} (Type: {MenuType}, ID: {MenuId}), Active: {IsEnabled}", 
            menu.Name, menu.MenuType, menu.Id, menu.IsEnabled);

        await InvalidateMenuCacheAsync(id, cancellationToken);

        return Mapper.Map<MenuDto>(menu);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var menu = await _menuRepository.GetByIdAsync(id, cancellationToken);
        if (menu == null)
        {
            throw new EntityNotFoundException("Menu", id);
        }

        await _menuRepository.DeleteAsync(menu, cancellationToken);

        LogInfo("Deleted menu {MenuName} (ID: {MenuId})", menu.Name, menu.Id);

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
                menu.ModifiedBy = _userContext.Username ?? SYSTEM_USERNAME;
                await _menuRepository.UpdateAsync(menu, cancellationToken);
            }
        }

        LogInfo("Reordered {Count} menus", menuOrders.Count);

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

            var level = CountLeadingAsterisks(trimmedLine);
            if (level == 0)
                continue;

            var content = trimmedLine.Substring(level).Trim();
            var (text, url) = ExtractTextAndUrl(content);
            
            if (text == null)
                continue;

            var item = new MenuMarkupItem
            {
                Text = text,
                Url = url,
                Level = level,
                Children = new List<MenuMarkupItem>()
            };

            AddItemToHierarchy(item, items, stack);
        }

        return items;
    }

    private static int CountLeadingAsterisks(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == '*')
        {
            count++;
        }
        return count;
    }

    private static (string? text, string? url) ExtractTextAndUrl(string content)
    {
        // Try to match [text](url) first - regular link
        var linkMatch = Regex.Match(content, @"\[([^\]]+)\]\(([^\)]+)\)");
        if (linkMatch.Success)
        {
            return (linkMatch.Groups[1].Value, linkMatch.Groups[2].Value);
        }

        // Try to match [text] without URL - dropdown header
        var headerMatch = Regex.Match(content, @"\[([^\]]+)\]");
        if (headerMatch.Success)
        {
            return (headerMatch.Groups[1].Value, null);
        }

        return (null, null);
    }

    private static void AddItemToHierarchy(MenuMarkupItem item, List<MenuMarkupItem> items, Stack<MenuMarkupItem> stack)
    {
        // Pop items from stack that are at same or deeper level
        while (stack.Count > 0 && stack.Peek().Level >= item.Level)
        {
            stack.Pop();
        }

        // Add to parent if exists, otherwise add to root
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

    private static string RenderNavbarItems(List<MenuMarkupItem> items)
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
                html.AppendLine($"    <ul class=\"dropdown-menu\" aria-labelledby=\"{dropdownId}\" style=\"z-index: 1060;\">");
                
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
            return Mapper.Map<MenuDto>(menu);
        }
        
        return null;
    }

    public async Task ActivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var menu = await _menuRepository.GetByIdAsync(id, cancellationToken);
        if (menu == null)
        {
            throw new EntityNotFoundException("Menu", id);
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
                otherMenu.ModifiedBy = _userContext.Username ?? SYSTEM_USERNAME;
                await _menuRepository.UpdateAsync(otherMenu, cancellationToken);
                LogInfo("Auto-deactivated menu {MenuName} (ID: {MenuId}) to activate {NewMenuName}", 
                    otherMenu.Name, otherMenu.Id, menu.Name);
            }
        }

        menu.IsEnabled = true;
        menu.ModifiedOn = DateTime.UtcNow;
        menu.ModifiedBy = _userContext.Username ?? SYSTEM_USERNAME;
        await _menuRepository.UpdateAsync(menu, cancellationToken);

        LogInfo("Activated menu {MenuName} (Type: {MenuType}, ID: {MenuId})", menu.Name, menu.MenuType, menu.Id);

        await InvalidateMenuCacheAsync(id, cancellationToken);
    }

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var menu = await _menuRepository.GetByIdAsync(id, cancellationToken);
        if (menu == null)
        {
            throw new EntityNotFoundException("Menu", id);
        }

        menu.IsEnabled = false;
        menu.ModifiedOn = DateTime.UtcNow;
        menu.ModifiedBy = _userContext.Username ?? SYSTEM_USERNAME;
        await _menuRepository.UpdateAsync(menu, cancellationToken);

        LogInfo("Deactivated menu {MenuName} (ID: {MenuId})", menu.Name, menu.Id);

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
    /// Resolve a URL - delegates to shared UrlTokenResolver service
    /// </summary>
    private async Task<string> ResolveUrlAsync(string url, CancellationToken cancellationToken)
    {
        var resolved = await _urlTokenResolver.ResolveTokenAsync(url, cancellationToken);
        return resolved ?? url;
    }

    private static string CleanupMenuHtml(string html)
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
        await EventPublisher.PublishAsync(
            new MenuChangedEvent(0, "*"),
            cancellationToken);
    }

    private async Task InvalidateMenuCacheAsync(int menuId, CancellationToken cancellationToken)
    {
        var menu = await _menuRepository.GetByIdAsync(menuId, cancellationToken);
        var menuName = menu?.Name ?? "*";
        
        await EventPublisher.PublishAsync(
            new MenuChangedEvent(menuId, menuName),
            cancellationToken);
    }

    public async Task<bool> HasActiveMenuOfTypeAsync(MenuType menuType, int? excludeMenuId = null, CancellationToken cancellationToken = default)
    {
        return await _menuRepository.HasActiveMenuOfTypeAsync(menuType, excludeMenuId, cancellationToken);
    }
}
