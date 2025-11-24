using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Service implementation for category management operations
/// </summary>
public class CategoryService : BaseService, ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IPageRepository _pageRepository;
    private readonly ISlugGenerator _slugGenerator;
    private const string CacheKeyPrefix = "category:";
    private const string CacheKeyTree = "category:tree";
    private const int MaxCategoryDepth = 3; // Maximum nesting depth for categories

    public CategoryService(
        ICategoryRepository categoryRepository,
        IPageRepository pageRepository,
        ICacheService cacheService,
        ISlugGenerator slugGenerator,
        ILogger<CategoryService> logger,
        ICacheInvalidationService cacheInvalidation)
        : base(logger, cacheService, cacheInvalidation)
    {
        _categoryRepository = categoryRepository;
        _pageRepository = pageRepository;
        _slugGenerator = slugGenerator;
    }

    public async Task<CategoryDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{id}";
        var cached = await Cache.GetAsync<CategoryDto>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            return cached;
        }

        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        if (category == null)
        {
            return null;
        }

        var dto = await MapToDtoAsync(category, cancellationToken);
        
        await Cache.SetAsync(cacheKey, dto, null, cancellationToken);

        return dto;
    }

    public async Task<CategoryDto?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByNameAsync(name, cancellationToken);
        return category != null ? await MapToDtoAsync(category, cancellationToken) : null;
    }

    public async Task<IEnumerable<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);
        var dtos = new List<CategoryDto>();
        
        foreach (var category in categories)
        {
            dtos.Add(await MapToDtoAsync(category, cancellationToken));
        }
        
        return dtos;
    }

    public async Task<IEnumerable<CategoryDto>> GetAllCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await GetAllAsync(cancellationToken);
    }

    public async Task<IEnumerable<CategoryDto>> GetRootCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _categoryRepository.GetRootCategoriesAsync(cancellationToken);
        var dtos = new List<CategoryDto>();
        
        foreach (var category in categories)
        {
            dtos.Add(await MapToDtoAsync(category, cancellationToken));
        }
        
        return dtos;
    }

    public async Task<IEnumerable<CategoryDto>> GetChildrenAsync(int parentId, CancellationToken cancellationToken = default)
    {
        var categories = await _categoryRepository.GetChildrenAsync(parentId, cancellationToken);
        var dtos = new List<CategoryDto>();
        
        foreach (var category in categories)
        {
            dtos.Add(await MapToDtoAsync(category, cancellationToken));
        }
        
        return dtos;
    }

    public async Task<IEnumerable<CategoryTreeDto>> GetCategoryTreeAsync(CancellationToken cancellationToken = default)
    {
        var cached = await Cache.GetAsync<List<CategoryTreeDto>>(CacheKeyTree, cancellationToken);
        
        if (cached != null)
        {
            return cached;
        }

        var rootCategories = await _categoryRepository.GetRootCategoriesAsync(cancellationToken);
        var tree = new List<CategoryTreeDto>();
        
        foreach (var root in rootCategories)
        {
            tree.Add(await BuildTreeNodeAsync(root, cancellationToken));
        }

        await Cache.SetAsync(CacheKeyTree, tree, null, cancellationToken);

        return tree;
    }

    public async Task<CategoryTreeDto?> GetCategorySubtreeAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId, cancellationToken);
        if (category == null)
        {
            return null;
        }

        return await BuildTreeNodeAsync(category, cancellationToken);
    }

    public async Task<IEnumerable<CategoryDto>> GetCategoryPathAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        var path = new List<CategoryDto>();
        var category = await _categoryRepository.GetByIdAsync(categoryId, cancellationToken);
        
        while (category != null)
        {
            path.Insert(0, await MapToDtoAsync(category, cancellationToken));
            
            if (category.ParentCategoryId.HasValue)
            {
                category = await _categoryRepository.GetByIdAsync(category.ParentCategoryId.Value, cancellationToken);
            }
            else
            {
                break;
            }
        }
        
        return path;
    }

    public async Task<IEnumerable<CategoryDto>> GetCategoriesByDepthAsync(int depth, CancellationToken cancellationToken = default)
    {
        var categories = await _categoryRepository.GetCategoriesByDepthAsync(depth, cancellationToken);
        var dtos = new List<CategoryDto>();
        
        foreach (var category in categories)
        {
            dtos.Add(await MapToDtoAsync(category, cancellationToken));
        }
        
        return dtos;
    }

    public async Task<CategoryDto> CreateAsync(CategoryCreateDto createDto, CancellationToken cancellationToken = default)
    {
        // Use ParentId (preferred) or fall back to ParentCategoryId (deprecated)
        var parentId = createDto.ParentId ?? createDto.ParentCategoryId;

        // Validate name availability within parent scope (hierarchical uniqueness)
        if (!await IsCategoryNameAvailableInParentAsync(createDto.Name, parentId, null, cancellationToken))
        {
            var parentName = parentId.HasValue ? 
                (await _categoryRepository.GetByIdAsync(parentId.Value, cancellationToken))?.Name ?? "Unknown" : 
                "root level";
            throw new InvalidOperationException($"Category name '{createDto.Name}' already exists under {parentName}.");
        }

        // Validate parent exists and check depth limit if specified
        if (parentId.HasValue)
        {
            var parent = await _categoryRepository.GetByIdAsync(parentId.Value, cancellationToken);
            if (parent == null)
            {
                throw new InvalidOperationException($"Parent category with ID {parentId.Value} not found.");
            }

            // Check depth limit - get parent's depth and ensure new category won't exceed max
            var parentPath = await _categoryRepository.GetCategoryPathAsync(parentId.Value, cancellationToken);
            var parentDepth = parentPath.Count() - 1; // 0-indexed depth
            
            if (parentDepth >= MaxCategoryDepth - 1)
            {
                throw new InvalidOperationException($"Cannot create category: maximum nesting depth of {MaxCategoryDepth} levels would be exceeded. Parent category is already at depth {parentDepth + 1}.");
            }
        }

        // Generate slug from name
        var slug = _slugGenerator.GenerateSlug(createDto.Name);

        var category = new Category
        {
            Name = createDto.Name,
            Slug = slug,
            Description = createDto.Description,
            ParentCategoryId = parentId,
            CreatedBy = createDto.CreatedBy,
            CreatedOn = DateTime.UtcNow,
            ModifiedBy = createDto.CreatedBy,
            ModifiedOn = DateTime.UtcNow
        };

        await _categoryRepository.AddAsync(category, cancellationToken);

        LogInfo("Created category {CategoryName} with ID {CategoryId} under parent {ParentId}", 
            category.Name, category.Id, parentId);

        await InvalidateCacheAsync(cancellationToken);

        return await MapToDtoAsync(category, cancellationToken);
    }

    public async Task<CategoryDto> UpdateAsync(int id, CategoryUpdateDto updateDto, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        if (category == null)
        {
            throw new InvalidOperationException($"Category with ID {id} not found.");
        }

        // Use ParentId (preferred) or fall back to ParentCategoryId (deprecated)
        var parentId = updateDto.ParentId ?? updateDto.ParentCategoryId;

        // Validate name availability within parent scope (hierarchical uniqueness)
        if (category.Name != updateDto.Name || parentId != category.ParentCategoryId)
        {
            if (!await IsCategoryNameAvailableInParentAsync(updateDto.Name, parentId, id, cancellationToken))
            {
                var parentName = parentId.HasValue ? 
                    (await _categoryRepository.GetByIdAsync(parentId.Value, cancellationToken))?.Name ?? "Unknown" : 
                    "root level";
                throw new InvalidOperationException($"Category name '{updateDto.Name}' already exists under {parentName}.");
            }
        }

        // Validate parent change
        if (parentId != category.ParentCategoryId)
        {
            if (!await ValidateMoveAsync(id, parentId, cancellationToken))
            {
                throw new InvalidOperationException("Cannot move category: would create circular reference.");
            }

            // Check depth limit when moving to a new parent
            if (parentId.HasValue)
            {
                var parentPath = await _categoryRepository.GetCategoryPathAsync(parentId.Value, cancellationToken);
                var parentDepth = parentPath.Count() - 1;
                
                // Get the depth of the subtree being moved
                var categoryPath = await _categoryRepository.GetCategoryPathAsync(id, cancellationToken);
                var currentDepth = categoryPath.Count() - 1;
                var subtreeDepth = await GetSubtreeDepthAsync(id, cancellationToken);
                var totalDepthAfterMove = parentDepth + 1 + subtreeDepth;
                
                if (totalDepthAfterMove > MaxCategoryDepth)
                {
                    throw new InvalidOperationException($"Cannot move category: would exceed maximum nesting depth of {MaxCategoryDepth} levels. This category has a subtree depth of {subtreeDepth + 1} levels, and the target parent is at depth {parentDepth + 1}.");
                }
            }
        }

        category.Name = updateDto.Name;
        category.Slug = _slugGenerator.GenerateSlug(updateDto.Name);
        category.Description = updateDto.Description;
        category.ParentCategoryId = parentId;
        category.ModifiedBy = updateDto.ModifiedBy ?? "system";
        category.ModifiedOn = DateTime.UtcNow;

        await _categoryRepository.UpdateAsync(category, cancellationToken);

        LogInfo("Updated category {CategoryName} (ID: {CategoryId})", category.Name, category.Id);

        await InvalidateCacheAsync(cancellationToken);

        return await MapToDtoAsync(category, cancellationToken);
    }

    public async Task<CategoryDto> MoveAsync(int categoryId, int? newParentId, CancellationToken cancellationToken = default)
    {
        if (!await ValidateMoveAsync(categoryId, newParentId, cancellationToken))
        {
            throw new InvalidOperationException("Cannot move category: would create circular reference.");
        }

        var category = await _categoryRepository.GetByIdAsync(categoryId, cancellationToken);
        if (category == null)
        {
            throw new InvalidOperationException($"Category with ID {categoryId} not found.");
        }

        // Check depth limit when moving to a new parent
        if (newParentId.HasValue)
        {
            var parentPath = await _categoryRepository.GetCategoryPathAsync(newParentId.Value, cancellationToken);
            var parentDepth = parentPath.Count() - 1;
            
            var subtreeDepth = await GetSubtreeDepthAsync(categoryId, cancellationToken);
            var totalDepthAfterMove = parentDepth + 1 + subtreeDepth;
            
            if (totalDepthAfterMove > MaxCategoryDepth)
            {
                throw new InvalidOperationException($"Cannot move category: would exceed maximum nesting depth of {MaxCategoryDepth} levels. This category has a subtree depth of {subtreeDepth + 1} levels, and the target parent is at depth {parentDepth + 1}.");
            }
        }

        category.ParentCategoryId = newParentId;
        await _categoryRepository.UpdateAsync(category, cancellationToken);

        LogInfo("Moved category {CategoryName} (ID: {CategoryId}) to parent {ParentId}", 
            category.Name, category.Id, newParentId);

        await InvalidateCacheAsync(cancellationToken);

        return await MapToDtoAsync(category, cancellationToken);
    }

    public async Task DeleteAsync(int id, bool deleteChildren = false, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        if (category == null)
        {
            throw new InvalidOperationException($"Category with ID {id} not found.");
        }

        var children = await _categoryRepository.GetChildrenAsync(id, cancellationToken);
        
        if (children.Any() && !deleteChildren)
        {
            throw new InvalidOperationException($"Category has {children.Count()} child categories. Set deleteChildren=true to delete them.");
        }

        if (deleteChildren)
        {
            foreach (var child in children)
            {
                await DeleteAsync(child.Id, true, cancellationToken);
            }
        }

        await _categoryRepository.DeleteAsync(category, cancellationToken);

        LogInfo("Deleted category {CategoryName} (ID: {CategoryId})", category.Name, category.Id);

        await InvalidateCacheAsync(cancellationToken);
    }

    public async Task<int> GetPageCountAsync(int categoryId, bool includeSubcategories = false, CancellationToken cancellationToken = default)
    {
        var count = await _categoryRepository.GetPageCountAsync(categoryId, cancellationToken);
        
        if (includeSubcategories)
        {
            var children = await _categoryRepository.GetChildrenAsync(categoryId, cancellationToken);
            foreach (var child in children)
            {
                count += await GetPageCountAsync(child.Id, true, cancellationToken);
            }
        }
        
        return count;
    }

    public async Task<IEnumerable<PageDto>> GetPagesAsync(int categoryId, bool includeSubcategories = false, CancellationToken cancellationToken = default)
    {
        var pages = await _pageRepository.GetByCategoryAsync(categoryId, cancellationToken);
        var pageDtos = pages.Select(p => new PageDto
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
        }).ToList();

        if (includeSubcategories)
        {
            var children = await _categoryRepository.GetChildrenAsync(categoryId, cancellationToken);
            foreach (var child in children)
            {
                var childPages = await GetPagesAsync(child.Id, true, cancellationToken);
                pageDtos.AddRange(childPages);
            }
        }

        return pageDtos;
    }

    public async Task<bool> IsCategoryNameAvailableAsync(string name, int? excludeCategoryId = null, CancellationToken cancellationToken = default)
    {
        // Note: This method checks global name availability
        // For hierarchical uniqueness, use IsCategoryNameAvailableInParentAsync
        var category = await _categoryRepository.GetByNameAsync(name, cancellationToken);
        
        if (category == null)
        {
            return true;
        }

        return excludeCategoryId.HasValue && category.Id == excludeCategoryId.Value;
    }

    /// <summary>
    /// Check if a category name is available within a specific parent scope
    /// </summary>
    private async Task<bool> IsCategoryNameAvailableInParentAsync(string name, int? parentId, int? excludeCategoryId = null, CancellationToken cancellationToken = default)
    {
        var allCategories = await _categoryRepository.GetAllAsync(cancellationToken);
        
        // Get all categories with the same parent
        var siblingsWithSameName = allCategories
            .Where(c => c.ParentCategoryId == parentId && 
                       c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        if (!siblingsWithSameName.Any())
        {
            return true;
        }

        // If we're excluding a category (during update), check if the only match is the excluded one
        if (excludeCategoryId.HasValue)
        {
            return siblingsWithSameName.All(c => c.Id == excludeCategoryId.Value);
        }

        return false;
    }

    public async Task<bool> ValidateMoveAsync(int categoryId, int? newParentId, CancellationToken cancellationToken = default)
    {
        if (!newParentId.HasValue)
        {
            return true; // Moving to root is always valid
        }

        if (categoryId == newParentId.Value)
        {
            return false; // Cannot be its own parent
        }

        // Check if newParent is a descendant of category (would create circular reference)
        var current = await _categoryRepository.GetByIdAsync(newParentId.Value, cancellationToken);
        
        while (current != null)
        {
            if (current.Id == categoryId)
            {
                return false; // Circular reference detected
            }
            
            if (current.ParentCategoryId.HasValue)
            {
                current = await _categoryRepository.GetByIdAsync(current.ParentCategoryId.Value, cancellationToken);
            }
            else
            {
                break;
            }
        }

        return true;
    }

    private async Task<CategoryDto> MapToDtoAsync(Category category, CancellationToken cancellationToken)
    {
        var pageCount = await _categoryRepository.GetPageCountAsync(category.Id, cancellationToken);
        var path = await _categoryRepository.GetCategoryPathAsync(category.Id, cancellationToken);
        var depth = path.Count() - 1;

        string? parentName = null;
        if (category.ParentCategoryId.HasValue)
        {
            var parent = await _categoryRepository.GetByIdAsync(category.ParentCategoryId.Value, cancellationToken);
            parentName = parent?.Name;
        }

        var pathString = string.Join(" > ", path.Select(c => c.Name));
        var fullPath = string.Join(":", path.Select(c => c.Name));

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            ParentId = category.ParentCategoryId,
            ParentCategoryId = category.ParentCategoryId, // Deprecated
            ParentName = parentName,
            PageCount = pageCount,
            Level = depth,
            Depth = depth, // Deprecated
            FullPath = fullPath,
            Path = pathString, // Deprecated
            DisplayOrder = category.DisplayOrder,
            CreatedOn = category.CreatedOn,
            CreatedBy = category.CreatedBy,
            ModifiedOn = category.ModifiedOn,
            ModifiedBy = category.ModifiedBy
        };
    }

    private async Task<CategoryTreeDto> BuildTreeNodeAsync(Category category, CancellationToken cancellationToken)
    {
        var children = await _categoryRepository.GetChildrenAsync(category.Id, cancellationToken);
        var childNodes = new List<CategoryTreeDto>();
        
        foreach (var child in children)
        {
            childNodes.Add(await BuildTreeNodeAsync(child, cancellationToken));
        }

        var pageCount = await _categoryRepository.GetPageCountAsync(category.Id, cancellationToken);

        return new CategoryTreeDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            PageCount = pageCount,
            Children = childNodes
        };
    }

    /// <summary>
    /// Get the maximum depth of a category's subtree (0 if no children)
    /// </summary>
    private async Task<int> GetSubtreeDepthAsync(int categoryId, CancellationToken cancellationToken)
    {
        var children = await _categoryRepository.GetChildrenAsync(categoryId, cancellationToken);
        
        if (!children.Any())
        {
            return 0;
        }

        var maxChildDepth = 0;
        foreach (var child in children)
        {
            var childDepth = await GetSubtreeDepthAsync(child.Id, cancellationToken);
            maxChildDepth = Math.Max(maxChildDepth, childDepth);
        }

        return maxChildDepth + 1;
    }


    // ============================================================================
    // PHASE 8: NESTED CATEGORIES - NEW METHOD IMPLEMENTATIONS
    // ============================================================================

    public async Task<CategoryDto?> GetByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        // Split path by colon (e.g., "documentation:gettingstarted:installation")
        var segments = path.Split(':', StringSplitOptions.RemoveEmptyEntries);
        
        if (segments.Length == 0)
        {
            return null;
        }

        // Start with root categories
        Category? current = null;
        
        foreach (var segment in segments)
        {
            var segmentName = segment.Trim();
            
            if (current == null)
            {
                // Looking for root category - must have no parent
                var rootCategories = await _categoryRepository.GetRootCategoriesAsync(cancellationToken);
                current = rootCategories.FirstOrDefault(c => c.Name.Equals(segmentName, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                // Looking for child of current category
                var children = await _categoryRepository.GetChildrenAsync(current.Id, cancellationToken);
                current = children.FirstOrDefault(c => c.Name.Equals(segmentName, StringComparison.OrdinalIgnoreCase));
            }
            
            if (current == null)
            {
                LogDebug("Category path segment '{Segment}' not found in path '{Path}'", segmentName, path);
                return null;
            }
        }

        return current != null ? await MapToDtoAsync(current, cancellationToken) : null;
    }

    public async Task<string> GetFullPathAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        var path = await _categoryRepository.GetCategoryPathAsync(categoryId, cancellationToken);
        return string.Join("/", path.Select(c => c.Name));
    }

    public async Task<int> GetDepthAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        var path = await _categoryRepository.GetCategoryPathAsync(categoryId, cancellationToken);
        return path.Count();
    }

    public async Task<IEnumerable<CategoryDto>> GetSubCategoriesAsync(int parentId, CancellationToken cancellationToken = default)
    {
        // Alias for GetChildrenAsync for clarity
        return await GetChildrenAsync(parentId, cancellationToken);
    }

    public async Task<bool> CanDeleteAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId, cancellationToken);
        if (category == null)
        {
            return false;
        }

        // Check for subcategories
        var children = await _categoryRepository.GetChildrenAsync(categoryId, cancellationToken);
        if (children.Any())
        {
            return false;
        }

        // Check for pages
        var pageCount = await _categoryRepository.GetPageCountAsync(categoryId, cancellationToken);
        if (pageCount > 0)
        {
            return false;
        }

        return true;
    }

    public async Task ReorderAsync(int categoryId, int newDisplayOrder, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId, cancellationToken);
        if (category == null)
        {
            throw new InvalidOperationException($"Category with ID {categoryId} not found.");
        }

        category.DisplayOrder = newDisplayOrder;
        await _categoryRepository.UpdateAsync(category, cancellationToken);

        LogInfo("Reordered category {CategoryName} (ID: {CategoryId}) to position {DisplayOrder}", 
            category.Name, category.Id, newDisplayOrder);

        await InvalidateCacheAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CategoryDeleteOptions options, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        if (category == null)
        {
            throw new InvalidOperationException($"Category with ID {id} not found.");
        }

        // Get all pages in this category
        var pages = await _pageRepository.GetByCategoryAsync(id, cancellationToken);
        
        // Handle pages based on delete action
        switch (options.Action)
        {
            case CategoryDeleteAction.MoveToParent:
                foreach (var page in pages)
                {
                    page.CategoryId = category.ParentCategoryId;
                    await _pageRepository.UpdateAsync(page, cancellationToken);
                }
                LogInfo("Moved {PageCount} pages from category {CategoryId} to parent category {ParentId}", 
                    pages.Count(), id, category.ParentCategoryId);
                break;

            case CategoryDeleteAction.MoveToCategory:
                if (!options.TargetCategoryId.HasValue)
                {
                    throw new InvalidOperationException("TargetCategoryId must be specified when using MoveToCategory action.");
                }
                
                var targetCategory = await _categoryRepository.GetByIdAsync(options.TargetCategoryId.Value, cancellationToken);
                if (targetCategory == null)
                {
                    throw new InvalidOperationException($"Target category with ID {options.TargetCategoryId.Value} not found.");
                }
                
                foreach (var page in pages)
                {
                    page.CategoryId = options.TargetCategoryId.Value;
                    await _pageRepository.UpdateAsync(page, cancellationToken);
                }
                LogInfo("Moved {PageCount} pages from category {CategoryId} to category {TargetId}", 
                    pages.Count(), id, options.TargetCategoryId.Value);
                break;

            case CategoryDeleteAction.RemoveCategory:
                foreach (var page in pages)
                {
                    page.CategoryId = null;
                    await _pageRepository.UpdateAsync(page, cancellationToken);
                }
                LogInfo("Removed category from {PageCount} pages in category {CategoryId}", 
                    pages.Count(), id);
                break;
        }

        // Check for subcategories
        var children = await _categoryRepository.GetChildrenAsync(id, cancellationToken);
        if (children.Any())
        {
            throw new InvalidOperationException($"Category has {children.Count()} subcategories. Delete or move them first.");
        }

        // Delete the category
        await _categoryRepository.DeleteAsync(category, cancellationToken);

        LogInfo("Deleted category {CategoryName} (ID: {CategoryId})", category.Name, category.Id);

        await InvalidateCacheAsync(cancellationToken);
    }

    /// <summary>
    /// Invalidate category caches using the centralized cache invalidation service
    /// </summary>
    private async Task InvalidateCacheAsync(CancellationToken cancellationToken)
    {
        // Use the centralized cache invalidation service which handles all category-related caches
        await CacheInvalidation.InvalidateCategoryAsync(0, cancellationToken);
    }
}
