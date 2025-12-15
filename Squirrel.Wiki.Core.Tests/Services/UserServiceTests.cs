using AutoMapper;
using Microsoft.Extensions.Logging;
using Moq;
using Squirrel.Wiki.Contracts.Authentication;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Events;
using Squirrel.Wiki.Core.Exceptions;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Services.Caching;
using Squirrel.Wiki.Core.Services.Users;

namespace Squirrel.Wiki.Core.Tests.Services;

/// <summary>
/// Unit tests for UserService
/// </summary>
public class UserServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IPageRepository> _pageRepositoryMock;
    private readonly Mock<ILogger<UserService>> _loggerMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<IConfigurationService> _configurationMock;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _pageRepositoryMock = new Mock<IPageRepository>();
        _loggerMock = new Mock<ILogger<UserService>>();
        _cacheMock = new Mock<ICacheService>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _mapperMock = new Mock<IMapper>();
        _configurationMock = new Mock<IConfigurationService>();

        _userService = new UserService(
            _userRepositoryMock.Object,
            _pageRepositoryMock.Object,
            _loggerMock.Object,
            _cacheMock.Object,
            _eventPublisherMock.Object,
            _mapperMock.Object,
            _configurationMock.Object
        );
    }

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithExistingUser_ReturnsUserDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Username = "testuser", Email = "test@example.com" };
        var userDto = new UserDto { Id = userId, Username = "testuser", Email = "test@example.com" };

        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mapperMock.Setup(m => m.Map<UserDto>(user))
            .Returns(userDto);

        // Act
        var result = await _userService.GetByIdAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.Id);
        Assert.Equal("testuser", result.Username);
        _userRepositoryMock.Verify(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentUser_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _userService.GetByIdAsync(userId);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetByUsernameAsync Tests

    [Fact]
    public async Task GetByUsernameAsync_WithExistingUser_ReturnsUserDto()
    {
        // Arrange
        var username = "testuser";
        var user = new User { Id = Guid.NewGuid(), Username = username, Email = "test@example.com" };
        var userDto = new UserDto { Id = user.Id, Username = username, Email = "test@example.com" };

        _userRepositoryMock.Setup(r => r.GetByUsernameAsync(username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mapperMock.Setup(m => m.Map<UserDto>(user))
            .Returns(userDto);

        // Act
        var result = await _userService.GetByUsernameAsync(username);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(username, result.Username);
    }

    [Fact]
    public async Task GetByUsernameAsync_WithNonExistentUser_ReturnsNull()
    {
        // Arrange
        var username = "nonexistent";
        _userRepositoryMock.Setup(r => r.GetByUsernameAsync(username, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _userService.GetByUsernameAsync(username);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesUser()
    {
        // Arrange
        var createDto = new UserCreateDto
        {
            Username = "newuser",
            Email = "new@example.com",
            DisplayName = "New User",
            IsAdmin = false,
            IsEditor = true
        };

        _userRepositoryMock.Setup(r => r.GetByUsernameAsync(createDto.Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepositoryMock.Setup(r => r.GetByEmailAsync(createDto.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepositoryMock.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User user, CancellationToken ct) => user);
        _mapperMock.Setup(m => m.Map<UserDto>(It.IsAny<User>()))
            .Returns(new UserDto { Username = createDto.Username, Email = createDto.Email });

        // Act
        var result = await _userService.CreateAsync(createDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(createDto.Username, result.Username);
        Assert.Equal(createDto.Email, result.Email);
        _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithExistingUsername_ThrowsBusinessRuleException()
    {
        // Arrange
        var createDto = new UserCreateDto
        {
            Username = "existinguser",
            Email = "new@example.com",
            DisplayName = "New User"
        };

        var existingUser = new User { Id = Guid.NewGuid(), Username = "existinguser" };
        _userRepositoryMock.Setup(r => r.GetByUsernameAsync(createDto.Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _userService.CreateAsync(createDto));
        Assert.Equal("USERNAME_EXISTS", exception.ErrorCode);
        Assert.Contains("existinguser", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_WithExistingEmail_ThrowsBusinessRuleException()
    {
        // Arrange
        var createDto = new UserCreateDto
        {
            Username = "newuser",
            Email = "existing@example.com",
            DisplayName = "New User"
        };

        var existingUser = new User { Id = Guid.NewGuid(), Email = "existing@example.com" };
        _userRepositoryMock.Setup(r => r.GetByUsernameAsync(createDto.Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepositoryMock.Setup(r => r.GetByEmailAsync(createDto.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _userService.CreateAsync(createDto));
        Assert.Equal("EMAIL_EXISTS", exception.ErrorCode);
        Assert.Contains("existing@example.com", exception.Message);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Username = "testuser",
            Email = "old@example.com",
            DisplayName = "Old Name"
        };

        var updateDto = new UserUpdateDto
        {
            Email = "new@example.com",
            DisplayName = "New Name",
            FirstName = "First",
            LastName = "Last",
            IsAdmin = true,
            IsEditor = false
        };

        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepositoryMock.Setup(r => r.GetByEmailAsync(updateDto.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mapperMock.Setup(m => m.Map<UserDto>(It.IsAny<User>()))
            .Returns(new UserDto { Id = userId, Email = updateDto.Email, DisplayName = updateDto.DisplayName });

        // Act
        var result = await _userService.UpdateAsync(userId, updateDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(updateDto.Email, result.Email);
        Assert.Equal(updateDto.DisplayName, result.DisplayName);
        _userRepositoryMock.Verify(r => r.UpdateAsync(It.Is<User>(u =>
            u.Email == updateDto.Email &&
            u.DisplayName == updateDto.DisplayName &&
            u.FirstName == updateDto.FirstName &&
            u.LastName == updateDto.LastName &&
            u.IsAdmin == updateDto.IsAdmin &&
            u.IsEditor == updateDto.IsEditor
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentUser_ThrowsEntityNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var updateDto = new UserUpdateDto { Email = "new@example.com", DisplayName = "New Name" };

        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _userService.UpdateAsync(userId, updateDto));
        Assert.Equal("User", exception.EntityType);
        Assert.Equal(userId, exception.EntityId);
    }

    [Fact]
    public async Task UpdateAsync_WithEmailTakenByAnotherUser_ThrowsBusinessRuleException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var user = new User { Id = userId, Username = "testuser", Email = "old@example.com" };
        var otherUser = new User { Id = otherUserId, Email = "taken@example.com" };
        var updateDto = new UserUpdateDto { Email = "taken@example.com", DisplayName = "Test" };

        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepositoryMock.Setup(r => r.GetByEmailAsync(updateDto.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _userService.UpdateAsync(userId, updateDto));
        Assert.Equal("EMAIL_EXISTS", exception.ErrorCode);
    }

    #endregion

    #region IsUsernameAvailableAsync Tests

    [Fact]
    public async Task IsUsernameAvailableAsync_WithAvailableUsername_ReturnsTrue()
    {
        // Arrange
        var username = "available";
        _userRepositoryMock.Setup(r => r.GetByUsernameAsync(username, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _userService.IsUsernameAvailableAsync(username);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsUsernameAvailableAsync_WithTakenUsername_ReturnsFalse()
    {
        // Arrange
        var username = "taken";
        var existingUser = new User { Id = Guid.NewGuid(), Username = username };
        _userRepositoryMock.Setup(r => r.GetByUsernameAsync(username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _userService.IsUsernameAvailableAsync(username);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsUsernameAvailableAsync_WithExcludedUserId_ReturnsTrue()
    {
        // Arrange
        var username = "testuser";
        var userId = Guid.NewGuid();
        var existingUser = new User { Id = userId, Username = username };
        _userRepositoryMock.Setup(r => r.GetByUsernameAsync(username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _userService.IsUsernameAvailableAsync(username, userId);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region IsEmailAvailableAsync Tests

    [Fact]
    public async Task IsEmailAvailableAsync_WithAvailableEmail_ReturnsTrue()
    {
        // Arrange
        var email = "available@example.com";
        _userRepositoryMock.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _userService.IsEmailAvailableAsync(email);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsEmailAvailableAsync_WithTakenEmail_ReturnsFalse()
    {
        // Arrange
        var email = "taken@example.com";
        var existingUser = new User { Id = Guid.NewGuid(), Email = email };
        _userRepositoryMock.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _userService.IsEmailAvailableAsync(email);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region ValidatePasswordAsync Tests

    [Fact]
    public async Task ValidatePasswordAsync_WithValidPassword_ReturnsSuccess()
    {
        // Arrange
        var password = "ValidPass123!";

        // Act
        var result = await _userService.ValidatePasswordAsync(password);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidatePasswordAsync_WithShortPassword_ReturnsFailure()
    {
        // Arrange
        var password = "Short1!";

        // Act
        var result = await _userService.ValidatePasswordAsync(password);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("at least 8 characters"));
    }

    [Fact]
    public async Task ValidatePasswordAsync_WithNoUppercase_ReturnsFailure()
    {
        // Arrange
        var password = "lowercase123!";

        // Act
        var result = await _userService.ValidatePasswordAsync(password);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("uppercase"));
    }

    [Fact]
    public async Task ValidatePasswordAsync_WithNoLowercase_ReturnsFailure()
    {
        // Arrange
        var password = "UPPERCASE123!";

        // Act
        var result = await _userService.ValidatePasswordAsync(password);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("lowercase"));
    }

    [Fact]
    public async Task ValidatePasswordAsync_WithNoDigit_ReturnsFailure()
    {
        // Arrange
        var password = "NoDigitsHere!";

        // Act
        var result = await _userService.ValidatePasswordAsync(password);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("digit"));
    }

    [Fact]
    public async Task ValidatePasswordAsync_WithNoSpecialCharacter_ReturnsFailure()
    {
        // Arrange
        var password = "NoSpecialChar123";

        // Act
        var result = await _userService.ValidatePasswordAsync(password);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("special character"));
    }

    [Fact]
    public async Task ValidatePasswordAsync_WithEmptyPassword_ReturnsFailure()
    {
        // Arrange
        var password = "";

        // Act
        var result = await _userService.ValidatePasswordAsync(password);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("required"));
    }

    [Fact]
    public async Task ValidatePasswordAsync_WithTooLongPassword_ReturnsFailure()
    {
        // Arrange
        var password = new string('a', 129) + "A1!";

        // Act
        var result = await _userService.ValidatePasswordAsync(password);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("128 characters"));
    }

    #endregion

    #region PromoteToAdminAsync Tests

    [Fact]
    public async Task PromoteToAdminAsync_WithExistingUser_PromotesUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Username = "testuser", IsAdmin = false };

        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _userService.PromoteToAdminAsync(userId);

        // Assert
        _userRepositoryMock.Verify(r => r.UpdateAsync(It.Is<User>(u => u.IsAdmin == true), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PromoteToAdminAsync_WithNonExistentUser_ThrowsEntityNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _userService.PromoteToAdminAsync(userId));
    }

    #endregion

    #region DemoteFromAdminAsync Tests

    [Fact]
    public async Task DemoteFromAdminAsync_WithExistingUser_DemotesUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Username = "testuser", IsAdmin = true };

        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _userService.DemoteFromAdminAsync(userId);

        // Assert
        _userRepositoryMock.Verify(r => r.UpdateAsync(It.Is<User>(u => u.IsAdmin == false), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
