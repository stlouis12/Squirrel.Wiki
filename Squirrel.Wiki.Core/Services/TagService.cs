using AutoMapper;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Exceptions;
using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Service implementation for tag management operations with integrated caching
/// </summary>
public class TagService : BaseService, ITagService
{
    private readonly ITagRepository _tagRepository;
    private readonly IPageRepository _pageRepository;

    // Cache key constants
    private const string AllTagsKey = "tags:all";
    private const string AllWithCountsKey = "tags:all-with-counts";
    private const string PopularKeyPrefix = "tags:popular:";
    private const string CloudKeyPrefix = "tags:cloud:";
    private const string PageKeyPrefix = "tags:page:";
    private const string ByNameKeyPrefix = "tags:by-name:";

    public TagService(
        ITagRepository tagRepository,
        IPageRepository pageRepository,
        IMapper mapper,
        ILogger<TagService> logger,
        ICacheService cache,
        ICacheInvalidationService cacheInvalidation)
        : base(logger, cache, cacheInvalidation, mapper)
    {
        _tagRepository = tagRepository;
        _pageRepository = pageRepository;
    }

    public async Task<TagDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var tag = await _tagRepository.GetByIdAsync(id, cancellationToken);
        return tag != null ? Mapper.Map<TagDto>(tag) : null;
    }

    public async Task<TagDto?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{ByNameKeyPrefix}{name.ToLowerInvariant()}";
        var cached = await Cache.GetAsync<TagDto>(cacheKey, cancellationToken);
        if (cached != null)
        {
            LogDebug("Tag cache hit for key: {CacheKey}", cacheKey);
            return cached;
        }

        LogDebug("Tag cache miss for key: {CacheKey}", cacheKey);
        var tag = await _tagRepository.GetByNameAsync(name, cancellationToken);
        var result = tag != null ? Mapper.Map<TagDto>(tag) : null;

        if (result != null)
        {
            await Cache.SetAsync(cacheKey, result, null, cancellationToken);
        }

        return result;
    }

    public async Task<IEnumerable<TagDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cached = await Cache.GetAsync<List<TagDto>>(AllTagsKey, cancellationToken);
        if (cached != null)
        {
            LogDebug("Tag cache hit for key: {CacheKey}", AllTagsKey);
            return cached;
        }

        LogDebug("Tag cache miss for key: {CacheKey}", AllTagsKey);
        var tags = await _tagRepository.GetAllAsync(cancellationToken);
        var resultList = Mapper.Map<List<TagDto>>(tags);

        await Cache.SetAsync(AllTagsKey, resultList, null, cancellationToken);

        return resultList;
    }

    public async Task<IEnumerable<TagDto>> GetAllTagsAsync(CancellationToken cancellationToken = default)
    {
        return await GetAllAsync(cancellationToken);
    }

    public async Task<IEnumerable<TagWithCountDto>> GetAllWithCountsAsync(CancellationToken cancellationToken = default)
    {
        var cached = await Cache.GetAsync<List<TagWithCountDto>>(AllWithCountsKey, cancellationToken);
        if (cached != null)
        {
            LogDebug("Tag cache hit for key: {CacheKey}", AllWithCountsKey);
            return cached;
        }

        LogDebug("Tag cache miss for key: {CacheKey}", AllWithCountsKey);
        var tags = await _tagRepository.GetAllAsync(cancellationToken);
        var tagCounts = new List<TagWithCountDto>();

        foreach (var tag in tags)
        {
            var count = await _tagRepository.GetPageCountAsync(tag.Id, cancellationToken);
            tagCounts.Add(new TagWithCountDto
            {
                Id = tag.Id,
                Name = tag.Name,
                PageCount = count
            });
        }

        var resultList = tagCounts.OrderByDescending(t => t.PageCount).ToList();
        await Cache.SetAsync(AllWithCountsKey, resultList, null, cancellationToken);

        return resultList;
    }

    public async Task<IEnumerable<TagWithCountDto>> GetPopularTagsAsync(int count = 20, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{PopularKeyPrefix}{count}";
        var cached = await Cache.GetAsync<List<TagWithCountDto>>(cacheKey, cancellationToken);
        if (cached != null)
        {
            LogDebug("Tag cache hit for key: {CacheKey}", cacheKey);
            return cached;
        }

        LogDebug("Tag cache miss for key: {CacheKey}", cacheKey);
        var popularTags = await _tagRepository.GetPopularTagsAsync(count, cancellationToken);
        var resultList = popularTags.Select(t => new TagWithCountDto
        {
            Id = t.Id,
            Name = t.Name,
            PageCount = t.PageTags?.Count ?? 0
        }).ToList();

        await Cache.SetAsync(cacheKey, resultList, null, cancellationToken);

        return resultList;
    }

    public async Task<IEnumerable<TagDto>> GetTagsForPageAsync(int pageId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{PageKeyPrefix}{pageId}";
        var cached = await Cache.GetAsync<List<TagDto>>(cacheKey, cancellationToken);
        if (cached != null)
        {
            LogDebug("Tag cache hit for key: {CacheKey}", cacheKey);
            return cached;
        }

        LogDebug("Tag cache miss for key: {CacheKey}", cacheKey);
        var tags = await _tagRepository.GetTagsForPageAsync(pageId, cancellationToken);
        var resultList = Mapper.Map<List<TagDto>>(tags);

        await Cache.SetAsync(cacheKey, resultList, null, cancellationToken);

        return resultList;
    }

    public async Task<IEnumerable<PageDto>> GetPagesWithTagAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var tag = await _tagRepository.GetByNameAsync(tagName, cancellationToken);
        if (tag == null)
        {
            return Enumerable.Empty<PageDto>();
        }

        var pages = await _pageRepository.GetByTagAsync(tagName, cancellationToken);
        return pages.Select(p => new PageDto
        {
            Id = p.Id,
            Title = p.Title,
            Slug = p.Slug,
            CategoryId = p.CategoryId,
            CreatedBy = p.CreatedBy,
            CreatedOn = p.CreatedOn,
            ModifiedBy = p.ModifiedBy,
            ModifiedOn = p.ModifiedOn,
            IsLocked = p.IsLocked,
            IsDeleted = p.IsDeleted
        });
    }

    public async Task<IEnumerable<TagCloudItemDto>> GetTagCloudAsync(int minCount = 1, int maxTags = 50, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CloudKeyPrefix}{minCount}:{maxTags}";
        var cached = await Cache.GetAsync<List<TagCloudItemDto>>(cacheKey, cancellationToken);
        if (cached != null)
        {
            LogDebug("Tag cache hit for key: {CacheKey}", cacheKey);
            return cached;
        }

        LogDebug("Tag cache miss for key: {CacheKey}", cacheKey);
        var tagsWithCounts = await GetAllWithCountsAsync(cancellationToken);
        var filteredTags = tagsWithCounts
            .Where(t => t.PageCount >= minCount)
            .OrderByDescending(t => t.PageCount)
            .Take(maxTags)
            .ToList();

        if (!filteredTags.Any())
        {
            return Enumerable.Empty<TagCloudItemDto>();
        }

        // Calculate weights (1-10) based on usage
        var minUsage = filteredTags.Min(t => t.PageCount);
        var maxUsage = filteredTags.Max(t => t.PageCount);
        var range = maxUsage - minUsage;

        var cloudItems = filteredTags.Select(t =>
        {
            int weight;
            if (range == 0)
            {
                weight = 5; // All tags have same count
            }
            else
            {
                // Scale to 1-10
                weight = (int)Math.Ceiling(((t.PageCount - minUsage) / (double)range) * 9) + 1;
            }

            return new TagCloudItemDto
            {
                Name = t.Name,
                Count = t.PageCount,
                Weight = weight
            };
        });

        var resultList = cloudItems.OrderBy(t => t.Name).ToList();
        await Cache.SetAsync(cacheKey, resultList, null, cancellationToken);

        return resultList;
    }

    public async Task<TagDto> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        // Check if tag already exists
        var existing = await _tagRepository.GetByNameAsync(name, cancellationToken);
        if (existing != null)
        {
            throw new BusinessRuleException(
                $"Tag '{name}' already exists.",
                "TAG_ALREADY_EXISTS"
            ).WithContext("TagName", name);
        }

        var tag = new Tag
        {
            Name = name.Trim()
        };

        await _tagRepository.AddAsync(tag, cancellationToken);

        LogInfo("Created tag {TagName} with ID {TagId}", tag.Name, tag.Id);

        await InvalidateAllTagCachesAsync(cancellationToken);

        return Mapper.Map<TagDto>(tag);
    }

    public async Task<TagDto> GetOrCreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var tag = await _tagRepository.GetByNameAsync(name, cancellationToken);
        
        if (tag != null)
        {
            return Mapper.Map<TagDto>(tag);
        }

        var result = await CreateAsync(name, cancellationToken);
        // Cache invalidation already handled in CreateAsync
        return result;
    }

    public async Task<TagDto> UpdateAsync(int id, string newName, CancellationToken cancellationToken = default)
    {
        var tag = await _tagRepository.GetByIdAsync(id, cancellationToken);
        if (tag == null)
        {
            throw new EntityNotFoundException("Tag", id);
        }

        // Check if new name is already taken by another tag
        var existing = await _tagRepository.GetByNameAsync(newName, cancellationToken);
        if (existing != null && existing.Id != id)
        {
            throw new BusinessRuleException(
                $"Tag name '{newName}' is already taken.",
                "TAG_NAME_TAKEN"
            ).WithContext("TagName", newName)
             .WithContext("ExistingTagId", existing.Id);
        }

        var oldName = tag.Name;
        tag.Name = newName.Trim();
        await _tagRepository.UpdateAsync(tag, cancellationToken);

        LogInfo("Updated tag ID {TagId} to name {TagName}", tag.Id, tag.Name);

        await InvalidateAllTagCachesAsync(cancellationToken);
        await InvalidateTagByNameAsync(oldName, cancellationToken);
        await InvalidateTagByNameAsync(newName, cancellationToken);

        return Mapper.Map<TagDto>(tag);
    }

    public async Task<TagDto> RenameAsync(int id, string newName, CancellationToken cancellationToken = default)
    {
        return await UpdateAsync(id, newName, cancellationToken);
    }

    public async Task MergeTagsAsync(int sourceTagId, int targetTagId, CancellationToken cancellationToken = default)
    {
        if (sourceTagId == targetTagId)
        {
            throw new BusinessRuleException(
                "Cannot merge a tag with itself.",
                "CANNOT_MERGE_SAME_TAG"
            ).WithContext("TagId", sourceTagId);
        }

        var sourceTag = await _tagRepository.GetByIdAsync(sourceTagId, cancellationToken);
        if (sourceTag == null)
        {
            throw new EntityNotFoundException("Tag", sourceTagId);
        }

        var targetTag = await _tagRepository.GetByIdAsync(targetTagId, cancellationToken);
        if (targetTag == null)
        {
            throw new EntityNotFoundException("Tag", targetTagId);
        }

        // Get all pages with the source tag
        var pages = await _pageRepository.GetByTagAsync(sourceTag.Name, cancellationToken);

        foreach (var page in pages)
        {
            // Get current tags for the page
            var pageTags = await _tagRepository.GetTagsForPageAsync(page.Id, cancellationToken);
            var tagNames = pageTags.Select(t => t.Name).ToList();

            // Remove source tag and add target tag if not already present
            tagNames.Remove(sourceTag.Name);
            if (!tagNames.Contains(targetTag.Name))
            {
                tagNames.Add(targetTag.Name);
            }

            // Update page tags
            await _pageRepository.UpdateTagsAsync(page.Id, tagNames, cancellationToken);
        }

        // Delete the source tag
        await _tagRepository.DeleteAsync(sourceTag, cancellationToken);

        LogInfo("Merged tag {SourceTag} (ID: {SourceId}) into {TargetTag} (ID: {TargetId})",
            sourceTag.Name, sourceTagId, targetTag.Name, targetTagId);

        await InvalidateAllTagCachesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var tag = await _tagRepository.GetByIdAsync(id, cancellationToken);
        if (tag == null)
        {
            throw new EntityNotFoundException("Tag", id);
        }

        var tagName = tag.Name;
        await _tagRepository.DeleteAsync(tag, cancellationToken);

        LogInfo("Deleted tag {TagName} (ID: {TagId})", tagName, id);

        await InvalidateAllTagCachesAsync(cancellationToken);
        await InvalidateTagByNameAsync(tagName, cancellationToken);
    }

    public async Task<int> CleanupUnusedTagsAsync(CancellationToken cancellationToken = default)
    {
        var unusedTags = await _tagRepository.GetUnusedTagsAsync(cancellationToken);
        var count = unusedTags.Count();

        foreach (var tag in unusedTags)
        {
            await _tagRepository.DeleteAsync(tag, cancellationToken);
        }

        if (count > 0)
        {
            LogInfo("Cleaned up {Count} unused tags", count);
            await InvalidateAllTagCachesAsync(cancellationToken);
        }

        return count;
    }

    public async Task<IEnumerable<TagDto>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var allTags = await _tagRepository.GetAllAsync(cancellationToken);
        var matchingTags = allTags
            .Where(t => t.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Name);

        return Mapper.Map<IEnumerable<TagDto>>(matchingTags);
    }

    public async Task<IEnumerable<TagDto>> GetRelatedTagsAsync(int tagId, int count = 10, CancellationToken cancellationToken = default)
    {
        var tag = await _tagRepository.GetByIdAsync(tagId, cancellationToken);
        if (tag == null)
        {
            return Enumerable.Empty<TagDto>();
        }

        // Get all pages with this tag
        var pages = await _pageRepository.GetByTagAsync(tag.Name, cancellationToken);
        
        // Count co-occurrences of other tags
        var tagCounts = new Dictionary<int, int>();

        foreach (var page in pages)
        {
            var pageTags = await _tagRepository.GetTagsForPageAsync(page.Id, cancellationToken);
            
            foreach (var pageTag in pageTags.Where(t => t.Id != tagId))
            {
                if (tagCounts.ContainsKey(pageTag.Id))
                {
                    tagCounts[pageTag.Id]++;
                }
                else
                {
                    tagCounts[pageTag.Id] = 1;
                }
            }
        }

        // Get top related tags
        var relatedTagIds = tagCounts
            .OrderByDescending(kvp => kvp.Value)
            .Take(count)
            .Select(kvp => kvp.Key);

        var relatedTags = new List<TagDto>();
        foreach (var relatedTagId in relatedTagIds)
        {
            var relatedTag = await _tagRepository.GetByIdAsync(relatedTagId, cancellationToken);
            if (relatedTag != null)
            {
                relatedTags.Add(Mapper.Map<TagDto>(relatedTag));
            }
        }

        return relatedTags;
    }

    public async Task<TagStatsDto> GetTagStatsAsync(CancellationToken cancellationToken = default)
    {
        var allTags = await _tagRepository.GetAllAsync(cancellationToken);
        var totalTags = allTags.Count();

        var unusedTags = await _tagRepository.GetUnusedTagsAsync(cancellationToken);
        var unusedCount = unusedTags.Count();

        var allPages = await _pageRepository.GetAllAsync(cancellationToken);
        var totalPages = allPages.Count();

        var totalTaggedPages = 0;
        var totalTagAssociations = 0;

        foreach (var page in allPages)
        {
            var pageTags = await _tagRepository.GetTagsForPageAsync(page.Id, cancellationToken);
            var tagCount = pageTags.Count();
            
            if (tagCount > 0)
            {
                totalTaggedPages++;
                totalTagAssociations += tagCount;
            }
        }

        var averageTagsPerPage = totalTaggedPages > 0 
            ? (double)totalTagAssociations / totalTaggedPages 
            : 0;

        return new TagStatsDto
        {
            TotalTags = totalTags,
            TotalTaggedPages = totalTaggedPages,
            UnusedTags = unusedCount,
            AverageTagsPerPage = Math.Round(averageTagsPerPage, 2)
        };
    }

    #region Cache Invalidation Methods

    /// <summary>
    /// Invalidates all tag-related caches
    /// </summary>
    private async Task InvalidateAllTagCachesAsync(CancellationToken cancellationToken)
    {
        await CacheInvalidation.InvalidateTagsAsync(cancellationToken);
    }

    /// <summary>
    /// Invalidates cache for a specific tag name
    /// </summary>
    private async Task InvalidateTagByNameAsync(string name, CancellationToken cancellationToken)
    {
        var cacheKey = $"{ByNameKeyPrefix}{name.ToLowerInvariant()}";
        LogDebug("Invalidating tag cache for name: {TagName}", name);
        await Cache.RemoveAsync(cacheKey, cancellationToken);
    }

    /// <summary>
    /// Invalidates tag count-related caches (when page-tag associations change)
    /// </summary>
    public async Task InvalidateTagCountCachesAsync(CancellationToken cancellationToken = default)
    {
        // Use centralized cache invalidation service
        await CacheInvalidation.InvalidateTagsAsync(cancellationToken);
    }

    /// <summary>
    /// Invalidates tag cache for a specific page
    /// </summary>
    public async Task InvalidateTagCachesForPageAsync(int pageId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{PageKeyPrefix}{pageId}";
        LogDebug("Invalidating tag cache for page: {PageId}", pageId);
        await Cache.RemoveAsync(cacheKey, cancellationToken);
    }

    #endregion
}
