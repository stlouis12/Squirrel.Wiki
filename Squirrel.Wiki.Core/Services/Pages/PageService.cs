using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Exceptions;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Services.Caching;
using Squirrel.Wiki.Core.Services.Configuration;
using Squirrel.Wiki.Core.Services.Content;
using Squirrel.Wiki.Core.Services.Tags;

namespace Squirrel.Wiki.Core.Services.Pages;

/// <summary>
/// Service implementation for core page management operations (CRUD)
/// </summary>
public class PageService : BaseService, IPageService
{
    private readonly IPageRepository _pageRepository;
    private readonly ITagRepository _tagRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IPageContentService _pageContentService;
    private readonly IPageRenderingService _pageRenderingService;
    private readonly IPageLinkService _pageLinkService;
    private readonly ISettingsService _settingsService;
    private readonly ITagService _tagService;
    private readonly ISlugGenerator _slugGenerator;

    private const string CacheKeyPrefix = "page:";
    private const string CacheKeyAllPages = "pages:all";

    public PageService(
        IPageRepository pageRepository,
        ITagRepository tagRepository,
        ICategoryRepository categoryRepository,
        IPageContentService pageContentService,
        IPageRenderingService pageRenderingService,
        IPageLinkService pageLinkService,
        ISettingsService settingsService,
        ITagService tagService,
        ISlugGenerator slugGenerator,
        ICacheService cacheService,
        ILogger<PageService> logger,
        ICacheInvalidationService cacheInvalidation,
        IConfigurationService configuration)
        : base(logger, cacheService, cacheInvalidation, null, configuration)
    {
        _pageRepository = pageRepository;
        _tagRepository = tagRepository;
        _categoryRepository = categoryRepository;
        _pageContentService = pageContentService;
        _pageRenderingService = pageRenderingService;
        _pageLinkService = pageLinkService;
        _settingsService = settingsService;
        _tagService = tagService;
        _slugGenerator = slugGenerator;
    }

    public async Task<PageDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        // Try cache first
        var cacheKey = $"{CacheKeyPrefix}{id}";
        var cachedPage = await Cache.GetAsync<PageDto>(cacheKey, cancellationToken);
        if (cachedPage != null)
        {
            LogDebug("Page cache hit for key: {CacheKey}", cacheKey);
            return cachedPage;
        }

        LogDebug("Page cache miss for key: {CacheKey}", cacheKey);
        var page = await _pageRepository.GetByIdAsync(id, cancellationToken);
        if (page == null)
            throw new EntityNotFoundException("Page", id);

        var content = await _pageRepository.GetLatestContentAsync(id, cancellationToken);
        var pageDto = await MapToPageDtoAsync(page, content, cancellationToken);

        // Cache the result
        await Cache.SetAsync(cacheKey, pageDto, null, cancellationToken);

        return pageDto;
    }

    public async Task<PageDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var page = await _pageRepository.GetBySlugAsync(slug, cancellationToken);
        if (page == null)
            return null;

        var content = await _pageRepository.GetLatestContentAsync(page.Id, cancellationToken);
        return await MapToPageDtoAsync(page, content, cancellationToken);
    }

    public async Task<PageDto?> GetByTitleAsync(string title, CancellationToken cancellationToken = default)
    {
        var page = await _pageRepository.GetByTitleAsync(title, cancellationToken);
        if (page == null)
            return null;

        var content = await _pageRepository.GetLatestContentAsync(page.Id, cancellationToken);
        return await MapToPageDtoAsync(page, content, cancellationToken);
    }

    public async Task<IEnumerable<PageDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeyAllPages;
        var cached = await Cache.GetAsync<List<PageDto>>(cacheKey, cancellationToken);

        if (cached != null)
        {
            LogDebug("All pages cache hit");
            return cached;
        }

        LogDebug("All pages cache miss");
        var pages = await _pageRepository.GetAllActiveAsync(cancellationToken);
        var pageDtos = new List<PageDto>();

        // Load all categories once to avoid N+1 queries
        var categoryCache = await LoadCategoryCacheAsync(cancellationToken);

        foreach (var page in pages)
        {
            var content = await _pageRepository.GetLatestContentAsync(page.Id, cancellationToken);
            pageDtos.Add(await MapToPageDtoAsync(page, content, categoryCache, cancellationToken));
        }

        await Cache.SetAsync(cacheKey, pageDtos, null, cancellationToken);
        return pageDtos;
    }

    public async Task<IEnumerable<PageDto>> GetByCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"pages:category:{categoryId}";
        var cached = await Cache.GetAsync<List<PageDto>>(cacheKey, cancellationToken);

        if (cached != null)
        {
            LogDebug("Pages by category cache hit for category: {CategoryId}", categoryId);
            return cached;
        }

        LogDebug("Pages by category cache miss for category: {CategoryId}", categoryId);
        var pages = await _pageRepository.GetByCategoryIdAsync(categoryId, cancellationToken);
        var pageDtos = new List<PageDto>();

        // Load all categories once to avoid N+1 queries
        var categoryCache = await LoadCategoryCacheAsync(cancellationToken);

        foreach (var page in pages)
        {
            var content = await _pageRepository.GetLatestContentAsync(page.Id, cancellationToken);
            pageDtos.Add(await MapToPageDtoAsync(page, content, categoryCache, cancellationToken));
        }

        await Cache.SetAsync(cacheKey, pageDtos, null, cancellationToken);
        return pageDtos;
    }

    public async Task<IEnumerable<PageDto>> GetByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"pages:tag:{tag.ToLowerInvariant()}";
        var cached = await Cache.GetAsync<List<PageDto>>(cacheKey, cancellationToken);

        if (cached != null)
        {
            LogDebug("Pages by tag cache hit for tag: {Tag}", tag);
            return cached;
        }

        LogDebug("Pages by tag cache miss for tag: {Tag}", tag);
        var pages = await _pageRepository.GetByTagAsync(tag, cancellationToken);
        var pageDtos = new List<PageDto>();

        // Load all categories once to avoid N+1 queries
        var categoryCache = await LoadCategoryCacheAsync(cancellationToken);

        foreach (var page in pages)
        {
            var content = await _pageRepository.GetLatestContentAsync(page.Id, cancellationToken);
            pageDtos.Add(await MapToPageDtoAsync(page, content, categoryCache, cancellationToken));
        }

        await Cache.SetAsync(cacheKey, pageDtos, null, cancellationToken);
        return pageDtos;
    }

    public async Task<IEnumerable<PageDto>> GetByAuthorAsync(string username, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"pages:author:{username.ToLowerInvariant()}";
        var cached = await Cache.GetAsync<List<PageDto>>(cacheKey, cancellationToken);

        if (cached != null)
        {
            LogDebug("Pages by author cache hit for author: {Username}", username);
            return cached;
        }

        LogDebug("Pages by author cache miss for author: {Username}", username);
        var pages = await _pageRepository.GetByCreatedByAsync(username, cancellationToken);
        var pageDtos = new List<PageDto>();

        // Load all categories once to avoid N+1 queries
        var categoryCache = await LoadCategoryCacheAsync(cancellationToken);

        foreach (var page in pages)
        {
            var content = await _pageRepository.GetLatestContentAsync(page.Id, cancellationToken);
            pageDtos.Add(await MapToPageDtoAsync(page, content, categoryCache, cancellationToken));
        }

        await Cache.SetAsync(cacheKey, pageDtos, null, cancellationToken);
        return pageDtos;
    }

    public async Task<PageDto> CreateAsync(PageCreateDto createDto, string username, CancellationToken cancellationToken = default)
    {
        LogInfo("Creating new page: {Title} by {Username}", createDto.Title, username);

        // Generate slug if not provided
        var slug = string.IsNullOrWhiteSpace(createDto.Slug)
            ? await GenerateSlugAsync(createDto.Title, null, cancellationToken)
            : createDto.Slug;

        // Create page entity
        var page = new Page
        {
            Title = createDto.Title,
            Slug = slug,
            CategoryId = createDto.CategoryId,
            Visibility = createDto.Visibility,
            IsLocked = createDto.IsLocked,
            IsDeleted = false,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = username,
            ModifiedOn = DateTime.UtcNow,
            ModifiedBy = username
        };

        page = await _pageRepository.AddAsync(page, cancellationToken);

        // Create initial content version using PageContentService
        await _pageContentService.CreateContentVersionAsync(page.Id, createDto.Content, username, null, cancellationToken);

        // Handle tags
        await UpdatePageTagsAsync(page.Id, createDto.Tags, cancellationToken);

        // Invalidate cache
        await CacheInvalidation.InvalidatePageAsync(page.Id, cancellationToken);

        LogInfo("Page created successfully: {PageId}", page.Id);

        // Get the content we just created
        var content = await _pageRepository.GetLatestContentAsync(page.Id, cancellationToken);
        return await MapToPageDtoAsync(page, content, cancellationToken);
    }

    public async Task<PageDto> UpdateAsync(int id, PageUpdateDto updateDto, string username, CancellationToken cancellationToken = default)
    {
        LogInfo("Updating page: {PageId} by {Username}", id, username);

        var page = await _pageRepository.GetByIdAsync(id, cancellationToken);
        if (page == null)
            throw new EntityNotFoundException("Page", id);

        var oldTitle = page.Title;

        // Update page properties
        page.Title = updateDto.Title;
        page.Slug = string.IsNullOrWhiteSpace(updateDto.Slug)
            ? await GenerateSlugAsync(updateDto.Title, id, cancellationToken)
            : updateDto.Slug;
        page.CategoryId = updateDto.CategoryId;
        page.Visibility = updateDto.Visibility;
        page.IsLocked = updateDto.IsLocked;
        page.ModifiedOn = DateTime.UtcNow;
        page.ModifiedBy = username;

        await _pageRepository.UpdateAsync(page, cancellationToken);

        // Check if page versioning is enabled
        var enableVersioning = await _settingsService.GetSettingAsync<bool>("EnablePageVersioning", cancellationToken);

        if (enableVersioning)
        {
            // Versioning enabled: Create new version
            LogDebug("Page versioning enabled - creating new version for page {PageId}", id);
            await _pageContentService.CreateContentVersionAsync(id, updateDto.Content, username, updateDto.ChangeComment, cancellationToken);
        }
        else
        {
            // Versioning disabled: Update existing version
            LogDebug("Page versioning disabled - updating existing version for page {PageId}", id);
            await _pageContentService.UpdateContentVersionAsync(id, updateDto.Content, username, updateDto.ChangeComment, cancellationToken);
        }

        // Handle tags
        await UpdatePageTagsAsync(page.Id, updateDto.Tags, cancellationToken);

        // Update links if title changed
        if (oldTitle != page.Title)
        {
            await _pageLinkService.UpdateLinksToPageAsync(oldTitle, page.Title, cancellationToken);
        }

        // Invalidate cache
        await CacheInvalidation.InvalidatePageAsync(page.Id, cancellationToken);

        LogInfo("Page updated successfully: {PageId}", page.Id);

        var content = await _pageRepository.GetLatestContentAsync(page.Id, cancellationToken);
        return await MapToPageDtoAsync(page, content, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        LogInfo("Soft deleting page: {PageId}", id);

        await _pageRepository.SoftDeleteAsync(id, cancellationToken);
        await CacheInvalidation.InvalidatePageAsync(id, cancellationToken);

        LogInfo("Page soft deleted successfully: {PageId}", id);
    }

    public async Task<PageDto> RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        LogInfo("Restoring page: {PageId}", id);

        await _pageRepository.RestoreAsync(id, cancellationToken);
        await CacheInvalidation.InvalidatePageAsync(id, cancellationToken);

        LogInfo("Page restored successfully: {PageId}", id);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<string> GenerateSlugAsync(string title, int? excludePageId = null, CancellationToken cancellationToken = default)
    {
        return await _slugGenerator.GenerateUniqueSlugAsync(
            title,
            async (slug, ct) =>
            {
                var existingPage = await _pageRepository.GetBySlugAsync(slug, ct);
                // Return true if slug exists AND it's not the page we're updating
                return existingPage != null && (!excludePageId.HasValue || existingPage.Id != excludePageId.Value);
            },
            cancellationToken);
    }

    public async Task<IEnumerable<PageDto>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var pages = await _pageRepository.SearchAsync(query, cancellationToken);
        var pageDtos = new List<PageDto>();

        // Load all categories once to avoid N+1 queries
        var categoryCache = await LoadCategoryCacheAsync(cancellationToken);

        foreach (var page in pages)
        {
            var content = await _pageRepository.GetLatestContentAsync(page.Id, cancellationToken);
            pageDtos.Add(await MapToPageDtoAsync(page, content, categoryCache, cancellationToken));
        }

        return pageDtos;
    }

    public async Task<PageDto?> GetHomePageAsync(CancellationToken cancellationToken = default)
    {
        // Look for a page tagged with "homepage"
        var pages = await _pageRepository.GetByTagAsync("homepage", cancellationToken);
        var homePage = pages.FirstOrDefault();

        if (homePage == null)
            return null;

        var content = await _pageRepository.GetLatestContentAsync(homePage.Id, cancellationToken);
        return await MapToPageDtoAsync(homePage, content, cancellationToken);
    }

    public async Task<IEnumerable<TagDto>> GetPageTagsAsync(int pageId, CancellationToken cancellationToken = default)
    {
        var tags = await _tagRepository.GetTagsForPageAsync(pageId, cancellationToken);
        return tags.Select(t => new TagDto { Id = t.Id, Name = t.Name });
    }

    public async Task<CategoryDto?> GetCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId, cancellationToken);
        if (category == null)
            return null;

        var pageCount = await _categoryRepository.GetPageCountAsync(categoryId, cancellationToken);
        var path = await _categoryRepository.GetCategoryPathAsync(categoryId, cancellationToken);
        var depth = path.Count() - 1;

        string? parentName = null;
        if (category.ParentCategoryId.HasValue)
        {
            var parent = await _categoryRepository.GetByIdAsync(category.ParentCategoryId.Value, cancellationToken);
            parentName = parent?.Name;
        }

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            ParentCategoryId = category.ParentCategoryId,
            ParentName = parentName,
            PageCount = pageCount,
            Depth = depth,
            Path = string.Join(" > ", path.Select(c => c.Name))
        };
    }

    // Private helper methods

    /// <summary>
    /// Load all categories into a dictionary for efficient lookups
    /// </summary>
    private async Task<Dictionary<int, string>> LoadCategoryCacheAsync(CancellationToken cancellationToken)
    {
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);
        return categories.ToDictionary(c => c.Id, c => c.Name);
    }

    /// <summary>
    /// Map page entity to DTO without category cache (single page operations)
    /// </summary>
    private async Task<PageDto> MapToPageDtoAsync(Page page, PageContent? content, CancellationToken cancellationToken)
    {
        // For single page operations, load category individually
        return await MapToPageDtoAsync(page, content, null, cancellationToken);
    }

    /// <summary>
    /// Map page entity to DTO with optional category cache (batch operations)
    /// </summary>
    private async Task<PageDto> MapToPageDtoAsync(
        Page page,
        PageContent? content,
        Dictionary<int, string>? categoryCache,
        CancellationToken cancellationToken)
    {
        var dto = new PageDto
        {
            Id = page.Id,
            Title = page.Title,
            Slug = page.Slug,
            CategoryId = page.CategoryId,
            Visibility = page.Visibility,
            IsLocked = page.IsLocked,
            IsDeleted = page.IsDeleted,
            CreatedOn = page.CreatedOn,
            CreatedBy = page.CreatedBy,
            ModifiedOn = page.ModifiedOn,
            ModifiedBy = page.ModifiedBy
        };

        // Get category name if applicable
        if (page.CategoryId.HasValue)
        {
            if (categoryCache != null && categoryCache.TryGetValue(page.CategoryId.Value, out var categoryName))
            {
                // Use cached category name
                dto.CategoryName = categoryName;
            }
            else
            {
                // Fall back to individual lookup
                var category = await _categoryRepository.GetByIdAsync(page.CategoryId.Value, cancellationToken);
                dto.CategoryName = category?.Name;
            }
        }

        // Get tags
        dto.Tags = page.PageTags?.Select(pt => pt.Tag.Name).ToList() ?? new List<string>();

        // Add content if available
        if (content != null)
        {
            dto.Content = content.Text;
            dto.Version = content.VersionNumber;
            dto.RenderedContent = await _pageRenderingService.RenderContentAsync(content.Text, cancellationToken);
        }

        return dto;
    }

    private async Task UpdatePageTagsAsync(int pageId, List<string> tagNames, CancellationToken cancellationToken)
    {
        var page = await _pageRepository.GetByIdAsync(pageId, cancellationToken);
        if (page == null)
            return;

        // Remove existing tags
        page.PageTags.Clear();

        // Add new tags
        foreach (var tagName in tagNames.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            var tag = await _tagRepository.GetOrCreateAsync(tagName.Trim(), cancellationToken);
            page.PageTags.Add(new PageTag { PageId = pageId, TagId = tag.Id });
        }

        await _pageRepository.UpdateAsync(page, cancellationToken);

        // Invalidate tag caches when page tags change
        if (_tagService is TagService tagService)
        {
            await tagService.InvalidateTagCachesForPageAsync(pageId, cancellationToken);
            await tagService.InvalidateTagCountCachesAsync(cancellationToken);
        }
    }
}
