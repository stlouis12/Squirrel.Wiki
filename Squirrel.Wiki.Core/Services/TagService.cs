using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Service implementation for tag management operations
/// </summary>
public class TagService : ITagService
{
    private readonly ITagRepository _tagRepository;
    private readonly IPageRepository _pageRepository;
    private readonly ILogger<TagService> _logger;

    public TagService(
        ITagRepository tagRepository,
        IPageRepository pageRepository,
        ILogger<TagService> logger)
    {
        _tagRepository = tagRepository;
        _pageRepository = pageRepository;
        _logger = logger;
    }

    public async Task<TagDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var tag = await _tagRepository.GetByIdAsync(id, cancellationToken);
        return tag != null ? MapToDto(tag) : null;
    }

    public async Task<TagDto?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var tag = await _tagRepository.GetByNameAsync(name, cancellationToken);
        return tag != null ? MapToDto(tag) : null;
    }

    public async Task<IEnumerable<TagDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tags = await _tagRepository.GetAllAsync(cancellationToken);
        return tags.Select(MapToDto);
    }

    public async Task<IEnumerable<TagDto>> GetAllTagsAsync(CancellationToken cancellationToken = default)
    {
        return await GetAllAsync(cancellationToken);
    }

    public async Task<IEnumerable<TagWithCountDto>> GetAllWithCountsAsync(CancellationToken cancellationToken = default)
    {
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

        return tagCounts.OrderByDescending(t => t.PageCount);
    }

    public async Task<IEnumerable<TagWithCountDto>> GetPopularTagsAsync(int count = 20, CancellationToken cancellationToken = default)
    {
        var popularTags = await _tagRepository.GetPopularTagsAsync(count, cancellationToken);
        return popularTags.Select(t => new TagWithCountDto
        {
            Id = t.Id,
            Name = t.Name,
            PageCount = t.PageTags?.Count ?? 0
        });
    }

    public async Task<IEnumerable<TagDto>> GetTagsForPageAsync(int pageId, CancellationToken cancellationToken = default)
    {
        var tags = await _tagRepository.GetTagsForPageAsync(pageId, cancellationToken);
        return tags.Select(MapToDto);
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

        return filteredTags.Select(t =>
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
        }).OrderBy(t => t.Name);
    }

    public async Task<TagDto> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        // Check if tag already exists
        var existing = await _tagRepository.GetByNameAsync(name, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"Tag '{name}' already exists.");
        }

        var tag = new Tag
        {
            Name = name.Trim()
        };

        await _tagRepository.AddAsync(tag, cancellationToken);

        _logger.LogInformation("Created tag {TagName} with ID {TagId}", tag.Name, tag.Id);

        return MapToDto(tag);
    }

    public async Task<TagDto> GetOrCreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var tag = await _tagRepository.GetByNameAsync(name, cancellationToken);
        
        if (tag != null)
        {
            return MapToDto(tag);
        }

        return await CreateAsync(name, cancellationToken);
    }

    public async Task<TagDto> UpdateAsync(int id, string newName, CancellationToken cancellationToken = default)
    {
        var tag = await _tagRepository.GetByIdAsync(id, cancellationToken);
        if (tag == null)
        {
            throw new InvalidOperationException($"Tag with ID {id} not found.");
        }

        // Check if new name is already taken by another tag
        var existing = await _tagRepository.GetByNameAsync(newName, cancellationToken);
        if (existing != null && existing.Id != id)
        {
            throw new InvalidOperationException($"Tag name '{newName}' is already taken.");
        }

        tag.Name = newName.Trim();
        await _tagRepository.UpdateAsync(tag, cancellationToken);

        _logger.LogInformation("Updated tag ID {TagId} to name {TagName}", tag.Id, tag.Name);

        return MapToDto(tag);
    }

    public async Task<TagDto> RenameAsync(int id, string newName, CancellationToken cancellationToken = default)
    {
        return await UpdateAsync(id, newName, cancellationToken);
    }

    public async Task MergeTagsAsync(int sourceTagId, int targetTagId, CancellationToken cancellationToken = default)
    {
        if (sourceTagId == targetTagId)
        {
            throw new InvalidOperationException("Cannot merge a tag with itself.");
        }

        var sourceTag = await _tagRepository.GetByIdAsync(sourceTagId, cancellationToken);
        if (sourceTag == null)
        {
            throw new InvalidOperationException($"Source tag with ID {sourceTagId} not found.");
        }

        var targetTag = await _tagRepository.GetByIdAsync(targetTagId, cancellationToken);
        if (targetTag == null)
        {
            throw new InvalidOperationException($"Target tag with ID {targetTagId} not found.");
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

        _logger.LogInformation("Merged tag {SourceTag} (ID: {SourceId}) into {TargetTag} (ID: {TargetId})",
            sourceTag.Name, sourceTagId, targetTag.Name, targetTagId);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var tag = await _tagRepository.GetByIdAsync(id, cancellationToken);
        if (tag == null)
        {
            throw new InvalidOperationException($"Tag with ID {id} not found.");
        }

        await _tagRepository.DeleteAsync(tag, cancellationToken);

        _logger.LogInformation("Deleted tag {TagName} (ID: {TagId})", tag.Name, tag.Id);
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
            _logger.LogInformation("Cleaned up {Count} unused tags", count);
        }

        return count;
    }

    public async Task<IEnumerable<TagDto>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var allTags = await _tagRepository.GetAllAsync(cancellationToken);
        var matchingTags = allTags
            .Where(t => t.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Name);

        return matchingTags.Select(MapToDto);
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
                relatedTags.Add(MapToDto(relatedTag));
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

    private static TagDto MapToDto(Tag tag)
    {
        return new TagDto
        {
            Id = tag.Id,
            Name = tag.Name
        };
    }
}
