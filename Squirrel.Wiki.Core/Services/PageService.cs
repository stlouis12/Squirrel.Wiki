using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Exceptions;
using Squirrel.Wiki.Core.Models;
using System.Text.RegularExpressions;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Service implementation for page management operations
/// </summary>
public class PageService : BaseService, IPageService
{
    private readonly IPageRepository _pageRepository;
    private readonly ITagRepository _tagRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IMarkdownService _markdownService;
    private readonly ISettingsService _settingsService;
    private readonly ITagService _tagService;
    private readonly ISlugGenerator _slugGenerator;

    private const string CacheKeyPrefix = "page:";
    private const string CacheKeyAllPages = "pages:all";

    public PageService(
        IPageRepository pageRepository,
        ITagRepository tagRepository,
        ICategoryRepository categoryRepository,
        IMarkdownService markdownService,
        ISettingsService settingsService,
        ICacheService cacheService,
        ITagService tagService,
        ISlugGenerator slugGenerator,
        ILogger<PageService> logger,
        ICacheInvalidationService cacheInvalidation)
        : base(logger, cacheService, cacheInvalidation, null)
    {
        _pageRepository = pageRepository;
        _tagRepository = tagRepository;
        _categoryRepository = categoryRepository;
        _markdownService = markdownService;
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

        // Create initial content version
        var pageContent = new PageContent
        {
            PageId = page.Id,
            Text = createDto.Content,
            VersionNumber = 1,
            EditedOn = DateTime.UtcNow,
            EditedBy = username
        };

        await _pageRepository.AddContentVersionAsync(pageContent, cancellationToken);

        // Handle tags
        await UpdatePageTagsAsync(page.Id, createDto.Tags, cancellationToken);

        // Invalidate cache
        await InvalidatePageCacheAsync(page.Id, cancellationToken);

        LogInfo("Page created successfully: {PageId}", page.Id);

        return await MapToPageDtoAsync(page, pageContent, cancellationToken);
    }

    public async Task<PageDto> UpdateAsync(int id, PageUpdateDto updateDto, string username, CancellationToken cancellationToken = default)
    {
        LogInfo("Updating page: {PageId} by {Username}", id, username);

        var page = await _pageRepository.GetByIdAsync(id, cancellationToken);
        if (page == null)
            throw new EntityNotFoundException("Page", id);

        var oldTitle = page.Title;
        var oldSlug = page.Slug;

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
        
        PageContent pageContent;
        
        if (enableVersioning)
        {
            // Versioning enabled: Create new version
            LogDebug("Page versioning enabled - creating new version for page {PageId}", id);
            
            var allVersions = await _pageRepository.GetAllContentVersionsAsync(id, cancellationToken);
            var maxVersion = allVersions.Any() ? allVersions.Max(v => v.VersionNumber) : 0;

            pageContent = new PageContent
            {
                PageId = page.Id,
                Text = updateDto.Content,
                VersionNumber = maxVersion + 1,
                EditedOn = DateTime.UtcNow,
                EditedBy = username,
                ChangeComment = updateDto.ChangeComment
            };

            await _pageRepository.AddContentVersionAsync(pageContent, cancellationToken);
        }
        else
        {
            // Versioning disabled: Update existing version
            LogDebug("Page versioning disabled - updating existing version for page {PageId}", id);
            
            var existingContent = await _pageRepository.GetLatestContentAsync(id, cancellationToken);
            
            if (existingContent != null)
            {
                // Update the existing content
                existingContent.Text = updateDto.Content;
                existingContent.EditedOn = DateTime.UtcNow;
                existingContent.EditedBy = username;
                existingContent.ChangeComment = updateDto.ChangeComment;
                
                await _pageRepository.UpdateContentVersionAsync(existingContent, cancellationToken);
                pageContent = existingContent;
            }
            else
            {
                // No existing content found, create initial version
                pageContent = new PageContent
                {
                    PageId = page.Id,
                    Text = updateDto.Content,
                    VersionNumber = 1,
                    EditedOn = DateTime.UtcNow,
                    EditedBy = username,
                    ChangeComment = updateDto.ChangeComment
                };
                
                await _pageRepository.AddContentVersionAsync(pageContent, cancellationToken);
            }
        }

        // Handle tags
        await UpdatePageTagsAsync(page.Id, updateDto.Tags, cancellationToken);

        // Update links if title changed
        if (oldTitle != page.Title)
        {
            await UpdateLinksToPageAsync(oldTitle, page.Title, cancellationToken);
        }

        // Invalidate cache
        await InvalidatePageCacheAsync(page.Id, cancellationToken);

        LogInfo("Page updated successfully: {PageId}", page.Id);

        return await MapToPageDtoAsync(page, pageContent, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        LogInfo("Soft deleting page: {PageId}", id);

        await _pageRepository.SoftDeleteAsync(id, cancellationToken);
        await InvalidatePageCacheAsync(id, cancellationToken);

        LogInfo("Page soft deleted successfully: {PageId}", id);
    }

    public async Task<PageDto> RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        LogInfo("Restoring page: {PageId}", id);

        await _pageRepository.RestoreAsync(id, cancellationToken);
        await InvalidatePageCacheAsync(id, cancellationToken);

        LogInfo("Page restored successfully: {PageId}", id);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<PageContentDto> GetLatestContentAsync(int pageId, CancellationToken cancellationToken = default)
    {
        var content = await _pageRepository.GetLatestContentAsync(pageId, cancellationToken);
        if (content == null)
            throw new EntityNotFoundException("PageContent", pageId);

        return MapToPageContentDto(content);
    }

    public async Task<IEnumerable<PageContentDto>> GetContentHistoryAsync(int pageId, CancellationToken cancellationToken = default)
    {
        var versions = await _pageRepository.GetAllContentVersionsAsync(pageId, cancellationToken);
        return versions.Select(MapToPageContentDto).OrderByDescending(v => v.Version);
    }

    public async Task<PageContentDto?> GetContentByVersionAsync(int pageId, int version, CancellationToken cancellationToken = default)
    {
        var content = await _pageRepository.GetContentByVersionAsync(pageId, version, cancellationToken);
        return content != null ? MapToPageContentDto(content) : null;
    }

    public async Task<PageDto> RevertToVersionAsync(int pageId, int version, string username, CancellationToken cancellationToken = default)
    {
        LogInfo("Reverting page {PageId} to version {Version}", pageId, version);

        var oldContent = await _pageRepository.GetContentByVersionAsync(pageId, version, cancellationToken);
        if (oldContent == null)
            throw new EntityNotFoundException("PageContent", $"Page {pageId}, Version {version}");

        var page = await _pageRepository.GetByIdAsync(pageId, cancellationToken);
        if (page == null)
            throw new EntityNotFoundException("Page", pageId);

        // Get current max version
        var allVersions = await _pageRepository.GetAllContentVersionsAsync(pageId, cancellationToken);
        var maxVersion = allVersions.Max(v => v.VersionNumber);

        // Create new version with old content
        var newContent = new PageContent
        {
            PageId = pageId,
            Text = oldContent.Text,
            VersionNumber = maxVersion + 1,
            EditedOn = DateTime.UtcNow,
            EditedBy = username,
            ChangeComment = $"Reverted to version {version}"
        };

        await _pageRepository.AddContentVersionAsync(newContent, cancellationToken);

        page.ModifiedOn = DateTime.UtcNow;
        page.ModifiedBy = username;
        await _pageRepository.UpdateAsync(page, cancellationToken);

        await InvalidatePageCacheAsync(pageId, cancellationToken);

        LogInfo("Page reverted successfully: {PageId}", pageId);

        return await MapToPageDtoAsync(page, newContent, cancellationToken);
    }

    public async Task<string> RenderContentAsync(string markdownContent, CancellationToken cancellationToken = default)
    {
        var html = await _markdownService.ToHtmlAsync(markdownContent, cancellationToken);
        return await _markdownService.ProcessTokensAsync(html, cancellationToken);
    }

    public async Task<string> RenderPageAsync(int pageId, CancellationToken cancellationToken = default)
    {
        var content = await _pageRepository.GetLatestContentAsync(pageId, cancellationToken);
        if (content == null)
            throw new EntityNotFoundException("PageContent", pageId);

        return await RenderContentAsync(content.Text, cancellationToken);
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

    // Additional methods for controller compatibility
    public async Task<IEnumerable<PageDto>> GetAllPagesAsync(CancellationToken cancellationToken = default)
    {
        return await GetAllAsync(cancellationToken);
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

    public async Task<IEnumerable<PageDto>> GetPagesByAuthorAsync(string username, CancellationToken cancellationToken = default)
    {
        return await GetByAuthorAsync(username, cancellationToken);
    }

    public async Task<IEnumerable<PageDto>> GetPagesByTagAsync(string tagName, CancellationToken cancellationToken = default)
    {
        return await GetByTagAsync(tagName, cancellationToken);
    }

    public async Task<PageDto> CreatePageAsync(CreatePageDto dto, CancellationToken cancellationToken = default)
    {
        var createDto = new PageCreateDto
        {
            Title = dto.Title,
            Content = dto.Content,
            Slug = dto.Slug,
            CategoryId = dto.CategoryId,
            Visibility = dto.Visibility,
            Tags = dto.Tags,
            IsLocked = dto.IsLocked
        };

        return await CreateAsync(createDto, dto.CreatedBy ?? "Anonymous", cancellationToken);
    }

    public async Task UpdatePageAsync(UpdatePageDto dto, CancellationToken cancellationToken = default)
    {
        var updateDto = new PageUpdateDto
        {
            Title = dto.Title,
            Content = dto.Content,
            Slug = dto.Slug,
            CategoryId = dto.CategoryId,
            Visibility = dto.Visibility,
            Tags = dto.Tags,
            IsLocked = dto.IsLocked,
            ChangeComment = dto.ChangeComment
        };

        await UpdateAsync(dto.Id, updateDto, dto.ModifiedBy ?? "Anonymous", cancellationToken);
    }

    public async Task DeletePageAsync(int pageId, CancellationToken cancellationToken = default)
    {
        await DeleteAsync(pageId, cancellationToken);
    }

    public async Task<IEnumerable<PageContentDto>> GetPageHistoryAsync(int pageId, CancellationToken cancellationToken = default)
    {
        return await GetContentHistoryAsync(pageId, cancellationToken);
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
            dto.RenderedContent = await RenderContentAsync(content.Text, cancellationToken);
        }

        return dto;
    }

    private PageContentDto MapToPageContentDto(PageContent content)
    {
        return new PageContentDto
        {
            Id = content.Id,
            PageId = content.PageId,
            Content = content.Text,
            Version = content.VersionNumber,
            CreatedOn = content.EditedOn,
            CreatedBy = content.EditedBy,
            ChangeComment = content.ChangeComment
        };
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

    private async Task UpdateLinksToPageAsync(string oldTitle, string newTitle, CancellationToken cancellationToken)
    {
        LogInfo("Updating links from '{OldTitle}' to '{NewTitle}'", oldTitle, newTitle);

        var allPages = await _pageRepository.GetAllAsync(cancellationToken);
        
        foreach (var page in allPages)
        {
            var allVersions = await _pageRepository.GetAllContentVersionsAsync(page.Id, cancellationToken);
            var latestContent = allVersions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
            
            if (latestContent != null)
            {
                var updatedContent = await _markdownService.UpdatePageLinksAsync(
                    latestContent.Text, oldTitle, newTitle, cancellationToken);

                if (updatedContent != latestContent.Text)
                {
                    // Content was updated, create new version
                    var newVersion = new PageContent
                    {
                        PageId = page.Id,
                        Text = updatedContent,
                        VersionNumber = latestContent.VersionNumber + 1,
                        EditedOn = DateTime.UtcNow,
                        EditedBy = "System",
                        ChangeComment = $"Auto-updated link from '{oldTitle}' to '{newTitle}'"
                    };

                    await _pageRepository.AddContentVersionAsync(newVersion, cancellationToken);
                    await InvalidatePageCacheAsync(page.Id, cancellationToken);
                }
            }
        }
    }


    private async Task InvalidatePageCacheAsync(int pageId, CancellationToken cancellationToken)
    {
        // Use the centralized cache invalidation service
        await CacheInvalidation.InvalidatePageAsync(pageId, cancellationToken);
    }
}

