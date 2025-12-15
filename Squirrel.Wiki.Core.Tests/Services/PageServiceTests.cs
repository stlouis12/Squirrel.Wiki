using Microsoft.Extensions.Logging;
using Moq;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Events;
using Squirrel.Wiki.Core.Exceptions;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Services.Caching;
using Squirrel.Wiki.Core.Services.Content;
using Squirrel.Wiki.Core.Services.Pages;
using Squirrel.Wiki.Core.Services.Tags;

namespace Squirrel.Wiki.Core.Tests.Services;

/// <summary>
/// Unit tests for PageService
/// </summary>
public class PageServiceTests
{
    private readonly Mock<IPageRepository> _pageRepositoryMock;
    private readonly Mock<ITagRepository> _tagRepositoryMock;
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock;
    private readonly Mock<IPageContentService> _pageContentServiceMock;
    private readonly Mock<IPageRenderingService> _pageRenderingServiceMock;
    private readonly Mock<IPageLinkService> _pageLinkServiceMock;
    private readonly Mock<ITagService> _tagServiceMock;
    private readonly Mock<ISlugGenerator> _slugGeneratorMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<ILogger<PageService>> _loggerMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly Mock<IConfigurationService> _configurationMock;
    private readonly PageService _pageService;

    public PageServiceTests()
    {
        _pageRepositoryMock = new Mock<IPageRepository>();
        _tagRepositoryMock = new Mock<ITagRepository>();
        _categoryRepositoryMock = new Mock<ICategoryRepository>();
        _pageContentServiceMock = new Mock<IPageContentService>();
        _pageRenderingServiceMock = new Mock<IPageRenderingService>();
        _pageLinkServiceMock = new Mock<IPageLinkService>();
        _tagServiceMock = new Mock<ITagService>();
        _slugGeneratorMock = new Mock<ISlugGenerator>();
        _cacheMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<PageService>>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _configurationMock = new Mock<IConfigurationService>();

        // Setup cache to return null by default (cache miss) - no generic setup needed
        
        _pageService = new PageService(
            _pageRepositoryMock.Object,
            _tagRepositoryMock.Object,
            _categoryRepositoryMock.Object,
            _pageContentServiceMock.Object,
            _pageRenderingServiceMock.Object,
            _pageLinkServiceMock.Object,
            _tagServiceMock.Object,
            _slugGeneratorMock.Object,
            _cacheMock.Object,
            _loggerMock.Object,
            _eventPublisherMock.Object,
            _configurationMock.Object
        );
    }

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithExistingPage_ReturnsPageDto()
    {
        // Arrange
        var pageId = 1;
        var page = new Page
        {
            Id = pageId,
            Title = "Test Page",
            Slug = "test-page",
            CategoryId = 1,
            CreatedBy = "testuser",
            CreatedOn = DateTime.UtcNow
        };
        var content = new PageContent
        {
            Id = Guid.NewGuid(),
            PageId = pageId,
            Text = "Test content",
            VersionNumber = 1
        };
        var category = new Category { Id = 1, Name = "Test Category" };

        _pageRepositoryMock.Setup(r => r.GetByIdAsync(pageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);
        _pageRepositoryMock.Setup(r => r.GetLatestContentAsync(pageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        _categoryRepositoryMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        _pageRenderingServiceMock.Setup(r => r.RenderContentAsync(content.Text, It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>Test content</p>");

        // Act
        var result = await _pageService.GetByIdAsync(pageId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(pageId, result.Id);
        Assert.Equal("Test Page", result.Title);
        Assert.Equal("test-page", result.Slug);
        Assert.Equal("Test content", result.Content);
        Assert.Equal(1, result.Version);
        _pageRepositoryMock.Verify(r => r.GetByIdAsync(pageId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentPage_ThrowsEntityNotFoundException()
    {
        // Arrange
        var pageId = 999;
        _pageRepositoryMock.Setup(r => r.GetByIdAsync(pageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Page?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _pageService.GetByIdAsync(pageId));
        Assert.Equal("Page", exception.EntityType);
        Assert.Equal(pageId, (int)exception.EntityId);
    }

    [Fact]
    public async Task GetByIdAsync_WithCachedPage_ReturnsCachedResult()
    {
        // Arrange
        var pageId = 1;
        var cachedPage = new PageDto
        {
            Id = pageId,
            Title = "Cached Page",
            Slug = "cached-page"
        };

        _cacheMock.Setup(c => c.GetAsync<PageDto>($"page:{pageId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedPage);

        // Act
        var result = await _pageService.GetByIdAsync(pageId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Cached Page", result.Title);
        _pageRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region GetBySlugAsync Tests

    [Fact]
    public async Task GetBySlugAsync_WithExistingPage_ReturnsPageDto()
    {
        // Arrange
        var slug = "test-page";
        var page = new Page
        {
            Id = 1,
            Title = "Test Page",
            Slug = slug,
            CreatedBy = "testuser",
            CreatedOn = DateTime.UtcNow
        };
        var content = new PageContent { Id = Guid.NewGuid(), PageId = 1, Text = "Content", VersionNumber = 1 };

        _pageRepositoryMock.Setup(r => r.GetBySlugAsync(slug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);
        _pageRepositoryMock.Setup(r => r.GetLatestContentAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        _pageRenderingServiceMock.Setup(r => r.RenderContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>Content</p>");

        // Act
        var result = await _pageService.GetBySlugAsync(slug);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(slug, result.Slug);
        Assert.Equal("Test Page", result.Title);
    }

    [Fact]
    public async Task GetBySlugAsync_WithNonExistentPage_ReturnsNull()
    {
        // Arrange
        var slug = "nonexistent";
        _pageRepositoryMock.Setup(r => r.GetBySlugAsync(slug, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Page?)null);

        // Act
        var result = await _pageService.GetBySlugAsync(slug);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetByTitleAsync Tests

    [Fact]
    public async Task GetByTitleAsync_WithExistingPage_ReturnsPageDto()
    {
        // Arrange
        var title = "Test Page";
        var page = new Page
        {
            Id = 1,
            Title = title,
            Slug = "test-page",
            CreatedBy = "testuser",
            CreatedOn = DateTime.UtcNow
        };
        var content = new PageContent { Id = Guid.NewGuid(), PageId = 1, Text = "Content", VersionNumber = 1 };

        _pageRepositoryMock.Setup(r => r.GetByTitleAsync(title, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);
        _pageRepositoryMock.Setup(r => r.GetLatestContentAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        _pageRenderingServiceMock.Setup(r => r.RenderContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>Content</p>");

        // Act
        var result = await _pageService.GetByTitleAsync(title);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(title, result.Title);
    }

    [Fact]
    public async Task GetByTitleAsync_WithNonExistentPage_ReturnsNull()
    {
        // Arrange
        var title = "Nonexistent Page";
        _pageRepositoryMock.Setup(r => r.GetByTitleAsync(title, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Page?)null);

        // Act
        var result = await _pageService.GetByTitleAsync(title);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesPage()
    {
        // Arrange
        var createDto = new PageCreateDto
        {
            Title = "New Page",
            Slug = "new-page",
            Content = "Page content",
            CategoryId = 1,
            Tags = new List<string> { "tag1", "tag2" }
        };
        var username = "testuser";
        var page = new Page
        {
            Id = 1,
            Title = createDto.Title,
            Slug = createDto.Slug,
            CategoryId = createDto.CategoryId,
            CreatedBy = username,
            CreatedOn = DateTime.UtcNow
        };
        var content = new PageContent
        {
            Id = Guid.NewGuid(),
            PageId = 1,
            Text = createDto.Content,
            VersionNumber = 1
        };

        _pageRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Page>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Page p, CancellationToken ct) => { p.Id = 1; return p; });
        _pageRepositoryMock.Setup(r => r.GetLatestContentAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        _pageContentServiceMock.Setup(s => s.CreateContentVersionAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _tagRepositoryMock.Setup(r => r.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CancellationToken ct) => new Tag { Id = 1, Name = name });
        _pageRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Page>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _pageRenderingServiceMock.Setup(r => r.RenderContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>Page content</p>");

        // Act
        var result = await _pageService.CreateAsync(createDto, username);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(createDto.Title, result.Title);
        Assert.Equal(createDto.Slug, result.Slug);
        _pageRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Page>(), It.IsAny<CancellationToken>()), Times.Once);
        _pageContentServiceMock.Verify(s => s.CreateContentVersionAsync(
            It.IsAny<int>(), createDto.Content, username, null, It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisherMock.Verify(e => e.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithoutSlug_GeneratesSlug()
    {
        // Arrange
        var createDto = new PageCreateDto
        {
            Title = "New Page",
            Slug = "", // Empty slug
            Content = "Content",
            Tags = new List<string>()
        };
        var username = "testuser";
        var generatedSlug = "new-page";

        _slugGeneratorMock.Setup(s => s.GenerateUniqueSlugAsync(
            createDto.Title, It.IsAny<Func<string, CancellationToken, Task<bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generatedSlug);
        _pageRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Page>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Page p, CancellationToken ct) => { p.Id = 1; return p; });
        _pageRepositoryMock.Setup(r => r.GetLatestContentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageContent { Id = Guid.NewGuid(), PageId = 1, Text = "Content", VersionNumber = 1 });
        _pageContentServiceMock.Setup(s => s.CreateContentVersionAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _pageRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Page>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _pageRenderingServiceMock.Setup(r => r.RenderContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>Content</p>");

        // Act
        var result = await _pageService.CreateAsync(createDto, username);

        // Assert
        Assert.Equal(generatedSlug, result.Slug);
        _slugGeneratorMock.Verify(s => s.GenerateUniqueSlugAsync(
            createDto.Title, It.IsAny<Func<string, CancellationToken, Task<bool>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesPage()
    {
        // Arrange
        var pageId = 1;
        var updateDto = new PageUpdateDto
        {
            Title = "Updated Title",
            Slug = "updated-slug",
            Content = "Updated content",
            CategoryId = 2,
            Tags = new List<string> { "newtag" },
            ChangeComment = "Updated page"
        };
        var username = "testuser";
        var existingPage = new Page
        {
            Id = pageId,
            Title = "Old Title",
            Slug = "old-slug",
            CategoryId = 1,
            PageTags = new List<PageTag>
            {
                new PageTag { PageId = pageId, TagId = 1, Tag = new Tag { Id = 1, Name = "oldtag" } }
            }
        };
        var content = new PageContent
        {
            Id = Guid.NewGuid(),
            PageId = pageId,
            Text = updateDto.Content,
            VersionNumber = 2
        };

        // First call returns the existing page
        _pageRepositoryMock.SetupSequence(r => r.GetByIdAsync(pageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPage)
            .ReturnsAsync(new Page 
            { 
                Id = pageId, 
                Title = updateDto.Title, 
                Slug = updateDto.Slug,
                CategoryId = updateDto.CategoryId,
                PageTags = new List<PageTag>
                {
                    new PageTag { PageId = pageId, TagId = 2, Tag = new Tag { Id = 2, Name = "newtag" } }
                }
            });
        _pageRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Page>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _pageRepositoryMock.Setup(r => r.GetLatestContentAsync(pageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        _configurationMock.Setup(c => c.GetValueAsync<bool>("SQUIRREL_ENABLE_PAGE_VERSIONING", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _pageContentServiceMock.Setup(s => s.CreateContentVersionAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _tagRepositoryMock.Setup(r => r.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CancellationToken ct) => new Tag { Id = 1, Name = name });
        _pageLinkServiceMock.Setup(s => s.UpdateLinksToPageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _pageRenderingServiceMock.Setup(r => r.RenderContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>Updated content</p>");

        // Act
        var result = await _pageService.UpdateAsync(pageId, updateDto, username);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(updateDto.Title, result.Title);
        Assert.Equal(updateDto.Slug, result.Slug);
        _pageRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Page>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _pageContentServiceMock.Verify(s => s.CreateContentVersionAsync(
            pageId, updateDto.Content, username, updateDto.ChangeComment, It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisherMock.Verify(e => e.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentPage_ThrowsEntityNotFoundException()
    {
        // Arrange
        var pageId = 999;
        var updateDto = new PageUpdateDto { Title = "Updated", Content = "Content", Tags = new List<string>() };
        var username = "testuser";

        _pageRepositoryMock.Setup(r => r.GetByIdAsync(pageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Page?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _pageService.UpdateAsync(pageId, updateDto, username));
        Assert.Equal("Page", exception.EntityType);
        Assert.Equal(pageId, (int)exception.EntityId);
    }

    [Fact]
    public async Task UpdateAsync_WithVersioningDisabled_UpdatesExistingVersion()
    {
        // Arrange
        var pageId = 1;
        var updateDto = new PageUpdateDto
        {
            Title = "Updated",
            Content = "Updated content",
            Tags = new List<string>()
        };
        var username = "testuser";
        var existingPage = new Page
        {
            Id = pageId,
            Title = "Old Title",
            Slug = "old-slug",
            PageTags = new List<PageTag>()
        };

        _pageRepositoryMock.Setup(r => r.GetByIdAsync(pageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPage);
        _pageRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Page>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _pageRepositoryMock.Setup(r => r.GetLatestContentAsync(pageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageContent { Id = Guid.NewGuid(), PageId = pageId, Text = "Content", VersionNumber = 1 });
        _configurationMock.Setup(c => c.GetValueAsync<bool>("SQUIRREL_ENABLE_PAGE_VERSIONING", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Versioning disabled
        _pageContentServiceMock.Setup(s => s.UpdateContentVersionAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _pageRenderingServiceMock.Setup(r => r.RenderContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>Content</p>");

        // Act
        await _pageService.UpdateAsync(pageId, updateDto, username);

        // Assert
        _pageContentServiceMock.Verify(s => s.UpdateContentVersionAsync(
            pageId, updateDto.Content, username, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _pageContentServiceMock.Verify(s => s.CreateContentVersionAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithTitleChange_UpdatesLinks()
    {
        // Arrange
        var pageId = 1;
        var oldTitle = "Old Title";
        var newTitle = "New Title";
        var updateDto = new PageUpdateDto
        {
            Title = newTitle,
            Content = "Content",
            Tags = new List<string>()
        };
        var username = "testuser";
        var existingPage = new Page
        {
            Id = pageId,
            Title = oldTitle,
            Slug = "old-title",
            PageTags = new List<PageTag>()
        };

        _pageRepositoryMock.Setup(r => r.GetByIdAsync(pageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPage);
        _pageRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Page>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _pageRepositoryMock.Setup(r => r.GetLatestContentAsync(pageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageContent { Id = Guid.NewGuid(), PageId = pageId, Text = "Content", VersionNumber = 1 });
        _configurationMock.Setup(c => c.GetValueAsync<bool>("SQUIRREL_ENABLE_PAGE_VERSIONING", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _pageContentServiceMock.Setup(s => s.CreateContentVersionAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _pageLinkServiceMock.Setup(s => s.UpdateLinksToPageAsync(oldTitle, newTitle, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _pageRenderingServiceMock.Setup(r => r.RenderContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>Content</p>");

        // Act
        await _pageService.UpdateAsync(pageId, updateDto, username);

        // Assert
        _pageLinkServiceMock.Verify(s => s.UpdateLinksToPageAsync(oldTitle, newTitle, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_WithExistingPage_SoftDeletesPage()
    {
        // Arrange
        var pageId = 1;
        var page = new Page
        {
            Id = pageId,
            Title = "Test Page",
            Slug = "test-page"
        };

        _pageRepositoryMock.Setup(r => r.GetByIdAsync(pageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);
        _pageRepositoryMock.Setup(r => r.SoftDeleteAsync(pageId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _pageService.DeleteAsync(pageId);

        // Assert
        _pageRepositoryMock.Verify(r => r.SoftDeleteAsync(pageId, It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisherMock.Verify(e => e.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentPage_ThrowsEntityNotFoundException()
    {
        // Arrange
        var pageId = 999;
        _pageRepositoryMock.Setup(r => r.GetByIdAsync(pageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Page?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _pageService.DeleteAsync(pageId));
        Assert.Equal("Page", exception.EntityType);
        Assert.Equal(pageId, (int)exception.EntityId);
    }

    #endregion

    #region RestoreAsync Tests

    [Fact]
    public async Task RestoreAsync_WithExistingPage_RestoresPage()
    {
        // Arrange
        var pageId = 1;
        var page = new Page
        {
            Id = pageId,
            Title = "Test Page",
            Slug = "test-page",
            IsDeleted = true,
            PageTags = new List<PageTag>()
        };
        var content = new PageContent { Id = Guid.NewGuid(), PageId = pageId, Text = "Content", VersionNumber = 1 };

        _pageRepositoryMock.Setup(r => r.GetByIdAsync(pageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);
        _pageRepositoryMock.Setup(r => r.RestoreAsync(pageId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _pageRepositoryMock.Setup(r => r.GetLatestContentAsync(pageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        _pageRenderingServiceMock.Setup(r => r.RenderContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>Content</p>");

        // Act
        var result = await _pageService.RestoreAsync(pageId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(pageId, result.Id);
        _pageRepositoryMock.Verify(r => r.RestoreAsync(pageId, It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisherMock.Verify(e => e.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestoreAsync_WithNonExistentPage_ThrowsEntityNotFoundException()
    {
        // Arrange
        var pageId = 999;
        _pageRepositoryMock.Setup(r => r.GetByIdAsync(pageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Page?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _pageService.RestoreAsync(pageId));
        Assert.Equal("Page", exception.EntityType);
        Assert.Equal(pageId, (int)exception.EntityId);
    }

    #endregion

    #region GenerateSlugAsync Tests

    [Fact]
    public async Task GenerateSlugAsync_CallsSlugGenerator()
    {
        // Arrange
        var title = "Test Page";
        var expectedSlug = "test-page";

        _slugGeneratorMock.Setup(s => s.GenerateUniqueSlugAsync(
            title, It.IsAny<Func<string, CancellationToken, Task<bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSlug);

        // Act
        var result = await _pageService.GenerateSlugAsync(title);

        // Assert
        Assert.Equal(expectedSlug, result);
        _slugGeneratorMock.Verify(s => s.GenerateUniqueSlugAsync(
            title, It.IsAny<Func<string, CancellationToken, Task<bool>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetByCategoryAsync Tests

    [Fact]
    public async Task GetByCategoryAsync_ReturnsPages()
    {
        // Arrange
        var categoryId = 1;
        var pages = new List<Page>
        {
            new Page { Id = 1, Title = "Page 1", Slug = "page-1", CategoryId = categoryId, PageTags = new List<PageTag>() },
            new Page { Id = 2, Title = "Page 2", Slug = "page-2", CategoryId = categoryId, PageTags = new List<PageTag>() }
        };
        var content = new PageContent { Id = Guid.NewGuid(), PageId = 1, Text = "Content", VersionNumber = 1 };
        var categories = new List<Category> { new Category { Id = categoryId, Name = "Test Category" } };

        _pageRepositoryMock.Setup(r => r.GetByCategoryIdAsync(categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pages);
        _pageRepositoryMock.Setup(r => r.GetLatestContentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        _categoryRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);
        _pageRenderingServiceMock.Setup(r => r.RenderContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>Content</p>");

        // Act
        var result = await _pageService.GetByCategoryAsync(categoryId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        _pageRepositoryMock.Verify(r => r.GetByCategoryIdAsync(categoryId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetByTagAsync Tests

    [Fact]
    public async Task GetByTagAsync_ReturnsPages()
    {
        // Arrange
        var tag = "testtag";
        var pages = new List<Page>
        {
            new Page { Id = 1, Title = "Page 1", Slug = "page-1", PageTags = new List<PageTag>() }
        };
        var content = new PageContent { Id = Guid.NewGuid(), PageId = 1, Text = "Content", VersionNumber = 1 };

        _pageRepositoryMock.Setup(r => r.GetByTagAsync(tag, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pages);
        _pageRepositoryMock.Setup(r => r.GetLatestContentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        _categoryRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category>());
        _pageRenderingServiceMock.Setup(r => r.RenderContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>Content</p>");

        // Act
        var result = await _pageService.GetByTagAsync(tag);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        _pageRepositoryMock.Verify(r => r.GetByTagAsync(tag, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
