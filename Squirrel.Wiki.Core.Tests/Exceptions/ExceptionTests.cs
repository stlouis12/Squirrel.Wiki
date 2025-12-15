using Squirrel.Wiki.Core.Exceptions;

namespace Squirrel.Wiki.Core.Tests.Exceptions;

/// <summary>
/// Unit tests for custom exception classes
/// </summary>
public class ExceptionTests
{
    #region ValidationException Tests

    [Fact]
    public void ValidationException_WithMultipleErrors_SetsPropertiesCorrectly()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            new ValidationError("Email", "Email is required"),
            new ValidationError("Password", "Password must be at least 8 characters")
        };

        // Act
        var exception = new ValidationException(errors);

        // Assert
        Assert.Equal("VALIDATION_ERROR", exception.ErrorCode);
        Assert.Equal(400, exception.StatusCode);
        Assert.False(exception.ShouldLog);
        Assert.Equal(2, exception.Errors.Count);
        Assert.Equal(2, exception.Context["ErrorCount"]);
    }

    [Fact]
    public void ValidationException_WithSingleFieldError_CreatesCorrectly()
    {
        // Arrange
        var field = "Username";
        var message = "Username is required";

        // Act
        var exception = new ValidationException(field, message);

        // Assert
        Assert.Single(exception.Errors);
        Assert.Equal(field, exception.Errors[0].Field);
        Assert.Equal(message, exception.Errors[0].Message);
    }

    [Fact]
    public void ValidationException_WithMessageOnly_CreatesWithEmptyField()
    {
        // Arrange
        var message = "Validation failed";

        // Act
        var exception = new ValidationException(message);

        // Assert
        Assert.Single(exception.Errors);
        Assert.Equal(string.Empty, exception.Errors[0].Field);
        Assert.Equal(message, exception.Errors[0].Message);
    }

    [Fact]
    public void ValidationException_GetUserMessage_WithSingleError_ReturnsSingleMessage()
    {
        // Arrange
        var exception = new ValidationException("Email", "Email is invalid");

        // Act
        var userMessage = exception.GetUserMessage();

        // Assert
        Assert.Equal("Email is invalid", userMessage);
    }

    [Fact]
    public void ValidationException_GetUserMessage_WithMultipleErrors_ReturnsCombinedMessage()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            new ValidationError("Email", "Email is required"),
            new ValidationError("Password", "Password is too short")
        };
        var exception = new ValidationException(errors);

        // Act
        var userMessage = exception.GetUserMessage();

        // Assert
        Assert.Contains("Validation failed with 2 error(s)", userMessage);
        Assert.Contains("Email is required", userMessage);
        Assert.Contains("Password is too short", userMessage);
    }

    [Fact]
    public void ValidationError_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var field = "Email";
        var message = "Invalid email format";
        var errorCode = "INVALID_EMAIL";

        // Act
        var error = new ValidationError(field, message, errorCode);

        // Assert
        Assert.Equal(field, error.Field);
        Assert.Equal(message, error.Message);
        Assert.Equal(errorCode, error.ErrorCode);
    }

    [Fact]
    public void ValidationError_WithoutErrorCode_HasNullErrorCode()
    {
        // Act
        var error = new ValidationError("Field", "Message");

        // Assert
        Assert.Null(error.ErrorCode);
    }

    #endregion

    #region EntityNotFoundException Tests

    [Fact]
    public void EntityNotFoundException_WithTypeAndId_SetsPropertiesCorrectly()
    {
        // Arrange
        var entityType = "Page";
        var entityId = 123;

        // Act
        var exception = new EntityNotFoundException(entityType, entityId);

        // Assert
        Assert.Equal("ENTITY_NOT_FOUND", exception.ErrorCode);
        Assert.Equal(404, exception.StatusCode);
        Assert.False(exception.ShouldLog);
        Assert.Equal(entityType, exception.EntityType);
        Assert.Equal(entityId, exception.EntityId);
        Assert.Contains(entityType, exception.Message);
        Assert.Contains(entityId.ToString(), exception.Message);
    }

    [Fact]
    public void EntityNotFoundException_WithAdditionalInfo_IncludesInfoInMessage()
    {
        // Arrange
        var entityType = "User";
        var entityId = "john@example.com";
        var additionalInfo = "The user may have been deleted.";

        // Act
        var exception = new EntityNotFoundException(entityType, entityId, additionalInfo);

        // Assert
        Assert.Contains(additionalInfo, exception.Message);
        Assert.Equal(additionalInfo, exception.Context["AdditionalInfo"]);
    }

    [Fact]
    public void EntityNotFoundException_GetUserMessage_ReturnsUserFriendlyMessage()
    {
        // Arrange
        var exception = new EntityNotFoundException("Page", 123);

        // Act
        var userMessage = exception.GetUserMessage();

        // Assert
        Assert.Equal("The requested page could not be found.", userMessage);
    }

    [Fact]
    public void EntityNotFoundException_AddsContextInformation()
    {
        // Arrange
        var entityType = "Category";
        var entityId = 456;

        // Act
        var exception = new EntityNotFoundException(entityType, entityId);

        // Assert
        Assert.Equal(entityType, exception.Context["EntityType"]);
        Assert.Equal(entityId, exception.Context["EntityId"]);
    }

    #endregion

    #region BusinessRuleException Tests

    [Fact]
    public void BusinessRuleException_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var message = "Cannot delete category with pages";
        var ruleCode = "CATEGORY_HAS_PAGES";

        // Act
        var exception = new BusinessRuleException(message, ruleCode);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Equal(ruleCode, exception.ErrorCode);
        Assert.Equal(ruleCode, exception.RuleCode);
        Assert.Equal(422, exception.StatusCode);
        Assert.False(exception.ShouldLog);
    }

    [Fact]
    public void BusinessRuleException_WithInnerException_ShouldLog()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");
        var message = "Business rule failed";
        var ruleCode = "RULE_FAILED";

        // Act
        var exception = new BusinessRuleException(message, ruleCode, innerException);

        // Assert
        Assert.Equal(innerException, exception.InnerException);
        Assert.True(exception.ShouldLog);
    }

    [Fact]
    public void BusinessRuleException_SlugAlreadyExists_CreatesCorrectException()
    {
        // Arrange
        var slug = "my-page";

        // Act
        var exception = BusinessRuleException.SlugAlreadyExists(slug);

        // Assert
        Assert.Equal("SLUG_EXISTS", exception.ErrorCode);
        Assert.Contains(slug, exception.Message);
        Assert.Equal(slug, exception.Context["Slug"]);
    }

    [Fact]
    public void BusinessRuleException_UsernameAlreadyExists_CreatesCorrectException()
    {
        // Arrange
        var username = "johndoe";

        // Act
        var exception = BusinessRuleException.UsernameAlreadyExists(username);

        // Assert
        Assert.Equal("USERNAME_EXISTS", exception.ErrorCode);
        Assert.Contains(username, exception.Message);
        Assert.Equal(username, exception.Context["Username"]);
    }

    [Fact]
    public void BusinessRuleException_EmailAlreadyExists_CreatesCorrectException()
    {
        // Arrange
        var email = "john@example.com";

        // Act
        var exception = BusinessRuleException.EmailAlreadyExists(email);

        // Assert
        Assert.Equal("EMAIL_EXISTS", exception.ErrorCode);
        Assert.Contains(email, exception.Message);
        Assert.Equal(email, exception.Context["Email"]);
    }

    [Fact]
    public void BusinessRuleException_MaxDepthExceeded_CreatesCorrectException()
    {
        // Arrange
        var maxDepth = 5;
        var currentDepth = 6;

        // Act
        var exception = BusinessRuleException.MaxDepthExceeded(maxDepth, currentDepth);

        // Assert
        Assert.Equal("MAX_DEPTH_EXCEEDED", exception.ErrorCode);
        Assert.Contains(maxDepth.ToString(), exception.Message);
        Assert.Contains(currentDepth.ToString(), exception.Message);
        Assert.Equal(maxDepth, exception.Context["MaxDepth"]);
        Assert.Equal(currentDepth, exception.Context["CurrentDepth"]);
    }

    [Fact]
    public void BusinessRuleException_CircularReferenceDetected_CreatesCorrectException()
    {
        // Arrange
        var entityType = "Category";

        // Act
        var exception = BusinessRuleException.CircularReferenceDetected(entityType);

        // Assert
        Assert.Equal("CIRCULAR_REFERENCE", exception.ErrorCode);
        Assert.Contains(entityType, exception.Message);
        Assert.Equal(entityType, exception.Context["EntityType"]);
    }

    #endregion

    #region AuthorizationException Tests

    [Fact]
    public void AuthorizationException_WithMessageOnly_SetsPropertiesCorrectly()
    {
        // Arrange
        var message = "Access denied";

        // Act
        var exception = new AuthorizationException(message);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Equal("AUTHORIZATION_FAILED", exception.ErrorCode);
        Assert.Equal(403, exception.StatusCode);
        Assert.False(exception.ShouldLog);
    }

    [Fact]
    public void AuthorizationException_WithUsernameAndRole_SetsPropertiesCorrectly()
    {
        // Arrange
        var message = "Access denied";
        var username = "john@example.com";
        var requiredRole = "Admin";

        // Act
        var exception = new AuthorizationException(message, username, requiredRole);

        // Assert
        Assert.Contains(username, exception.Message);
        Assert.Contains(requiredRole, exception.Message);
        Assert.Equal(username, exception.Username);
        Assert.Equal(requiredRole, exception.RequiredRole);
        Assert.Equal(username, exception.Context["Username"]);
        Assert.Equal(requiredRole, exception.Context["RequiredRole"]);
    }

    [Fact]
    public void AuthorizationException_WithResourceAndAction_SetsPropertiesCorrectly()
    {
        // Arrange
        var message = "Access denied";
        var username = "john@example.com";
        var resource = "Page";
        var action = "delete";

        // Act
        var exception = new AuthorizationException(message, username, resource, action);

        // Assert
        Assert.Contains(username, exception.Message);
        Assert.Contains(resource, exception.Message);
        Assert.Contains(action, exception.Message);
        Assert.Equal(username, exception.Username);
        Assert.Equal(resource, exception.Resource);
        Assert.Equal(username, exception.Context["Username"]);
        Assert.Equal(resource, exception.Context["Resource"]);
        Assert.Equal(action, exception.Context["Action"]);
    }

    [Fact]
    public void AuthorizationException_GetUserMessage_ReturnsGenericMessage()
    {
        // Arrange
        var exception = new AuthorizationException("Access denied");

        // Act
        var userMessage = exception.GetUserMessage();

        // Assert
        Assert.Equal("You do not have permission to perform this action.", userMessage);
    }

    #endregion

    #region SquirrelWikiException Base Tests

    [Fact]
    public void SquirrelWikiException_WithContext_AddsContextInformation()
    {
        // Arrange
        var exception = new ValidationException("Test");

        // Act
        exception.WithContext("UserId", 123)
                 .WithContext("Action", "Create");

        // Assert
        Assert.Equal(123, exception.Context["UserId"]);
        Assert.Equal("Create", exception.Context["Action"]);
    }

    [Fact]
    public void SquirrelWikiException_WithContext_OverwritesExistingKey()
    {
        // Arrange
        var exception = new ValidationException("Test");

        // Act
        exception.WithContext("Key", "Value1")
                 .WithContext("Key", "Value2");

        // Assert
        Assert.Equal("Value2", exception.Context["Key"]);
    }

    [Fact]
    public void SquirrelWikiException_WithContext_ReturnsException()
    {
        // Arrange
        var exception = new ValidationException("Test");

        // Act
        var result = exception.WithContext("Key", "Value");

        // Assert
        Assert.Same(exception, result);
    }

    [Fact]
    public void SquirrelWikiException_InheritsFromException()
    {
        // Arrange & Act
        var exception = new ValidationException("Test");

        // Assert
        Assert.IsAssignableFrom<Exception>(exception);
    }

    #endregion
}
