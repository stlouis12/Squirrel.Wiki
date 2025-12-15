using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Database.Entities;

namespace Squirrel.Wiki.Core.Tests.Models;

/// <summary>
/// Unit tests for File DTOs
/// </summary>
public class FileDtoTests
{
    #region FileDto Tests

    [Fact]
    public void FileSizeFormatted_WithBytesOnly_ReturnsBytes()
    {
        // Arrange
        var fileDto = new FileDto { FileSize = 512 };

        // Act
        var formatted = fileDto.FileSizeFormatted;

        // Assert
        Assert.Equal("512 bytes", formatted);
    }

    [Fact]
    public void FileSizeFormatted_WithKilobytes_ReturnsKB()
    {
        // Arrange
        var fileDto = new FileDto { FileSize = 1536 }; // 1.5 KB

        // Act
        var formatted = fileDto.FileSizeFormatted;

        // Assert
        Assert.Equal("1.50 KB", formatted);
    }

    [Fact]
    public void FileSizeFormatted_WithMegabytes_ReturnsMB()
    {
        // Arrange
        var fileDto = new FileDto { FileSize = 2621440 }; // 2.5 MB

        // Act
        var formatted = fileDto.FileSizeFormatted;

        // Assert
        Assert.Equal("2.50 MB", formatted);
    }

    [Fact]
    public void FileSizeFormatted_WithGigabytes_ReturnsGB()
    {
        // Arrange
        var fileDto = new FileDto { FileSize = 3221225472 }; // 3 GB

        // Act
        var formatted = fileDto.FileSizeFormatted;

        // Assert
        Assert.Equal("3.00 GB", formatted);
    }

    [Fact]
    public void FileSizeFormatted_WithZeroBytes_ReturnsZeroBytes()
    {
        // Arrange
        var fileDto = new FileDto { FileSize = 0 };

        // Act
        var formatted = fileDto.FileSizeFormatted;

        // Assert
        Assert.Equal("0 bytes", formatted);
    }

    [Fact]
    public void FileSizeFormatted_WithExactlyOneKB_ReturnsKB()
    {
        // Arrange
        var fileDto = new FileDto { FileSize = 1024 };

        // Act
        var formatted = fileDto.FileSizeFormatted;

        // Assert
        Assert.Equal("1.00 KB", formatted);
    }

    [Fact]
    public void FileSizeFormatted_WithExactlyOneMB_ReturnsMB()
    {
        // Arrange
        var fileDto = new FileDto { FileSize = 1048576 }; // 1024 * 1024

        // Act
        var formatted = fileDto.FileSizeFormatted;

        // Assert
        Assert.Equal("1.00 MB", formatted);
    }

    [Fact]
    public void FileDto_DefaultValues_AreSetCorrectly()
    {
        // Act
        var fileDto = new FileDto();

        // Assert
        Assert.Equal(Guid.Empty, fileDto.Id);
        Assert.Equal(string.Empty, fileDto.FileName);
        Assert.Equal(0, fileDto.FileSize);
        Assert.Equal(string.Empty, fileDto.ContentType);
        Assert.Null(fileDto.Description);
        Assert.Null(fileDto.FolderId);
        Assert.Equal(string.Empty, fileDto.StorageProvider);
        Assert.Equal(string.Empty, fileDto.UploadedBy);
        Assert.Equal(FileVisibility.Inherit, fileDto.Visibility);
        Assert.Equal(string.Empty, fileDto.DownloadUrl);
        Assert.Equal(0, fileDto.CurrentVersion);
    }

    #endregion

    #region FileUploadDto Tests

    [Fact]
    public void FileUploadDto_DefaultValues_AreSetCorrectly()
    {
        // Act
        var uploadDto = new FileUploadDto();

        // Assert
        Assert.Equal(string.Empty, uploadDto.FileName);
        Assert.Equal(0, uploadDto.FileSize);
        Assert.Equal(string.Empty, uploadDto.ContentType);
        Assert.Null(uploadDto.Description);
        Assert.Null(uploadDto.FolderId);
        Assert.Equal(Stream.Null, uploadDto.FileStream);
        Assert.Equal(FileVisibility.Inherit, uploadDto.Visibility);
    }

    [Fact]
    public void FileUploadDto_CanSetAllProperties()
    {
        // Arrange
        var fileName = "test.pdf";
        var fileSize = 1024L;
        var contentType = "application/pdf";
        var description = "Test file";
        var folderId = 5;
        var stream = new MemoryStream();
        var visibility = FileVisibility.Public;

        // Act
        var uploadDto = new FileUploadDto
        {
            FileName = fileName,
            FileSize = fileSize,
            ContentType = contentType,
            Description = description,
            FolderId = folderId,
            FileStream = stream,
            Visibility = visibility
        };

        // Assert
        Assert.Equal(fileName, uploadDto.FileName);
        Assert.Equal(fileSize, uploadDto.FileSize);
        Assert.Equal(contentType, uploadDto.ContentType);
        Assert.Equal(description, uploadDto.Description);
        Assert.Equal(folderId, uploadDto.FolderId);
        Assert.Equal(stream, uploadDto.FileStream);
        Assert.Equal(visibility, uploadDto.Visibility);
    }

    #endregion

    #region FileUpdateDto Tests

    [Fact]
    public void FileUpdateDto_DefaultValues_AreNull()
    {
        // Act
        var updateDto = new FileUpdateDto();

        // Assert
        Assert.Null(updateDto.FileName);
        Assert.Null(updateDto.Description);
        Assert.Null(updateDto.Visibility);
    }

    [Fact]
    public void FileUpdateDto_CanSetAllProperties()
    {
        // Arrange
        var fileName = "updated.pdf";
        var description = "Updated description";
        var visibility = FileVisibility.Private;

        // Act
        var updateDto = new FileUpdateDto
        {
            FileName = fileName,
            Description = description,
            Visibility = visibility
        };

        // Assert
        Assert.Equal(fileName, updateDto.FileName);
        Assert.Equal(description, updateDto.Description);
        Assert.Equal(visibility, updateDto.Visibility);
    }

    #endregion

    #region FileSearchDto Tests

    [Fact]
    public void FileSearchDto_DefaultValues_AreSetCorrectly()
    {
        // Act
        var searchDto = new FileSearchDto();

        // Assert
        Assert.Null(searchDto.SearchTerm);
        Assert.Null(searchDto.FolderId);
        Assert.Null(searchDto.ContentType);
        Assert.Null(searchDto.UploadedAfter);
        Assert.Null(searchDto.UploadedBefore);
        Assert.Null(searchDto.UploadedBy);
        Assert.Equal(1, searchDto.PageNumber);
        Assert.Equal(50, searchDto.PageSize);
    }

    [Fact]
    public void FileSearchDto_CanSetAllProperties()
    {
        // Arrange
        var searchTerm = "test";
        var folderId = 10;
        var contentType = "image/png";
        var uploadedAfter = DateTime.Now.AddDays(-7);
        var uploadedBefore = DateTime.Now;
        var uploadedBy = "user@example.com";
        var pageNumber = 2;
        var pageSize = 25;

        // Act
        var searchDto = new FileSearchDto
        {
            SearchTerm = searchTerm,
            FolderId = folderId,
            ContentType = contentType,
            UploadedAfter = uploadedAfter,
            UploadedBefore = uploadedBefore,
            UploadedBy = uploadedBy,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        // Assert
        Assert.Equal(searchTerm, searchDto.SearchTerm);
        Assert.Equal(folderId, searchDto.FolderId);
        Assert.Equal(contentType, searchDto.ContentType);
        Assert.Equal(uploadedAfter, searchDto.UploadedAfter);
        Assert.Equal(uploadedBefore, searchDto.UploadedBefore);
        Assert.Equal(uploadedBy, searchDto.UploadedBy);
        Assert.Equal(pageNumber, searchDto.PageNumber);
        Assert.Equal(pageSize, searchDto.PageSize);
    }

    #endregion

    #region FolderDto Tests

    [Fact]
    public void FolderDto_DefaultValues_AreSetCorrectly()
    {
        // Act
        var folderDto = new FolderDto();

        // Assert
        Assert.Equal(0, folderDto.Id);
        Assert.Equal(string.Empty, folderDto.Name);
        Assert.Equal(string.Empty, folderDto.Slug);
        Assert.Null(folderDto.Description);
        Assert.Null(folderDto.ParentFolderId);
        Assert.Null(folderDto.ParentFolderName);
        Assert.Null(folderDto.Path);
        Assert.Equal(0, folderDto.FileCount);
        Assert.Equal(0, folderDto.SubFolderCount);
        Assert.Equal(string.Empty, folderDto.CreatedBy);
    }

    #endregion

    #region FolderCreateDto Tests

    [Fact]
    public void FolderCreateDto_DefaultValues_AreSetCorrectly()
    {
        // Act
        var createDto = new FolderCreateDto();

        // Assert
        Assert.Equal(string.Empty, createDto.Name);
        Assert.Null(createDto.Description);
        Assert.Null(createDto.ParentFolderId);
        Assert.Equal(string.Empty, createDto.CreatedBy);
    }

    [Fact]
    public void FolderCreateDto_CanSetAllProperties()
    {
        // Arrange
        var name = "New Folder";
        var description = "Test folder";
        var parentFolderId = 5;
        var createdBy = "admin@example.com";

        // Act
        var createDto = new FolderCreateDto
        {
            Name = name,
            Description = description,
            ParentFolderId = parentFolderId,
            CreatedBy = createdBy
        };

        // Assert
        Assert.Equal(name, createDto.Name);
        Assert.Equal(description, createDto.Description);
        Assert.Equal(parentFolderId, createDto.ParentFolderId);
        Assert.Equal(createdBy, createDto.CreatedBy);
    }

    #endregion

    #region FolderUpdateDto Tests

    [Fact]
    public void FolderUpdateDto_DefaultValues_AreNull()
    {
        // Act
        var updateDto = new FolderUpdateDto();

        // Assert
        Assert.Null(updateDto.Name);
        Assert.Null(updateDto.Description);
        Assert.Null(updateDto.DisplayOrder);
    }

    #endregion

    #region FolderTreeDto Tests

    [Fact]
    public void FolderTreeDto_DefaultValues_AreSetCorrectly()
    {
        // Act
        var treeDto = new FolderTreeDto();

        // Assert
        Assert.Equal(0, treeDto.Id);
        Assert.Equal(string.Empty, treeDto.Name);
        Assert.Equal(string.Empty, treeDto.Slug);
        Assert.Null(treeDto.ParentFolderId);
        Assert.Equal(0, treeDto.FileCount);
        Assert.NotNull(treeDto.Children);
        Assert.Empty(treeDto.Children);
    }

    [Fact]
    public void FolderTreeDto_CanAddChildren()
    {
        // Arrange
        var parent = new FolderTreeDto { Id = 1, Name = "Parent" };
        var child1 = new FolderTreeDto { Id = 2, Name = "Child 1", ParentFolderId = 1 };
        var child2 = new FolderTreeDto { Id = 3, Name = "Child 2", ParentFolderId = 1 };

        // Act
        parent.Children.Add(child1);
        parent.Children.Add(child2);

        // Assert
        Assert.Equal(2, parent.Children.Count);
        Assert.Contains(child1, parent.Children);
        Assert.Contains(child2, parent.Children);
    }

    #endregion

    #region FileDetailsDto Tests

    [Fact]
    public void FileDetailsDto_InheritsFromFileDto()
    {
        // Act
        var detailsDto = new FileDetailsDto();

        // Assert
        Assert.IsAssignableFrom<FileDto>(detailsDto);
    }

    [Fact]
    public void FileDetailsDto_HasFolderPathProperty()
    {
        // Arrange
        var folderPath = "/root/subfolder";

        // Act
        var detailsDto = new FileDetailsDto { FolderPath = folderPath };

        // Assert
        Assert.Equal(folderPath, detailsDto.FolderPath);
    }

    #endregion

    #region FileVersionDto Tests

    [Fact]
    public void FileVersionDto_DefaultValues_AreSetCorrectly()
    {
        // Act
        var versionDto = new FileVersionDto();

        // Assert
        Assert.Equal(0, versionDto.Id);
        Assert.Equal(Guid.Empty, versionDto.FileId);
        Assert.Equal(0, versionDto.VersionNumber);
        Assert.Equal(0, versionDto.FileSize);
        Assert.Equal(string.Empty, versionDto.ContentType);
        Assert.Equal(string.Empty, versionDto.StoragePath);
        Assert.Equal(string.Empty, versionDto.UploadedBy);
    }

    [Fact]
    public void FileVersionDto_CanSetAllProperties()
    {
        // Arrange
        var id = 1;
        var fileId = Guid.NewGuid();
        var versionNumber = 2;
        var fileSize = 2048L;
        var contentType = "text/plain";
        var storagePath = "/storage/file.txt";
        var uploadedBy = "user@example.com";
        var uploadedOn = DateTime.Now;

        // Act
        var versionDto = new FileVersionDto
        {
            Id = id,
            FileId = fileId,
            VersionNumber = versionNumber,
            FileSize = fileSize,
            ContentType = contentType,
            StoragePath = storagePath,
            UploadedBy = uploadedBy,
            UploadedOn = uploadedOn
        };

        // Assert
        Assert.Equal(id, versionDto.Id);
        Assert.Equal(fileId, versionDto.FileId);
        Assert.Equal(versionNumber, versionDto.VersionNumber);
        Assert.Equal(fileSize, versionDto.FileSize);
        Assert.Equal(contentType, versionDto.ContentType);
        Assert.Equal(storagePath, versionDto.StoragePath);
        Assert.Equal(uploadedBy, versionDto.UploadedBy);
        Assert.Equal(uploadedOn, versionDto.UploadedOn);
    }

    #endregion
}
