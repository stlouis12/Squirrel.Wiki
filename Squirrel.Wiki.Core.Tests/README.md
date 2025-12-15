# Squirrel.Wiki.Core.Tests

This project contains comprehensive unit tests for the Squirrel.Wiki.Core library.

## Test Structure

The tests are organized into the following categories:

### 1. Models Tests (`Models/`)
- **ResultTests.cs** - Tests for the Result pattern implementation
  - Success and failure scenarios
  - Error handling and messages
  - Implicit conversions
  
- **FileDtoTests.cs** - Tests for file data transfer objects
  - DTO property validation
  - Mapping scenarios

### 2. Exceptions Tests (`Exceptions/`)
- **ExceptionTests.cs** - Tests for custom exception types
  - BusinessRuleException with error codes and context
  - EntityNotFoundException with entity type and ID
  - ValidationException with multiple validation errors
  - FileStorageException, FileSizeExceededException, FileTypeNotAllowedException
  - ConfigurationException, AuthorizationException, ExternalServiceException

### 3. Security Tests (`Security/`)
- **BCryptPasswordHasherTests.cs** - Tests for password hashing
  - Password hashing functionality
  - Password verification
  - Hash validation
  - Rehashing detection for security upgrades

### 4. Service Layer Tests (`Services/`)
- **UserServiceTests.cs** - Comprehensive tests for UserService using Moq
  - User retrieval (by ID, username, email, external ID)
  - User creation with validation
  - User updates with conflict detection
  - Username and email availability checks
  - Password validation (length, complexity requirements)
  - Role management (promote/demote admin and editor)
  - Account locking and unlocking
  - Password reset workflows

- **PageServiceTests.cs** - Comprehensive tests for PageService using Moq
  - Page retrieval (by ID, slug, title, category, tag, author)
  - Page creation with slug generation and tag management
  - Page updates with versioning support
  - Page deletion (soft delete) and restoration
  - Slug generation and uniqueness validation
  - Caching behavior verification
  - Link updates when page titles change

## Testing Patterns

### Arrange-Act-Assert Pattern
All tests follow the AAA pattern for clarity:
```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange - Set up test data and mocks
    var input = new TestData();
    
    // Act - Execute the method under test
    var result = await _service.MethodAsync(input);
    
    // Assert - Verify the expected outcome
    Assert.NotNull(result);
    Assert.Equal(expected, result.Value);
}
```

### Mocking with Moq
Service layer tests use Moq to isolate units under test:
```csharp
// Setup mock behavior
_repositoryMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
    .ReturnsAsync(entity);

// Verify mock was called
_repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Entity>(), It.IsAny<CancellationToken>()), 
    Times.Once);
```

### Test Naming Convention
Tests follow the pattern: `MethodName_Scenario_ExpectedBehavior`
- **MethodName**: The method being tested
- **Scenario**: The specific condition or input
- **ExpectedBehavior**: What should happen

Examples:
- `GetByIdAsync_WithExistingUser_ReturnsUserDto`
- `CreateAsync_WithExistingUsername_ThrowsBusinessRuleException`
- `ValidatePasswordAsync_WithShortPassword_ReturnsFailure`

### Test Organization
Tests are grouped using `#region` directives for better navigation:
```csharp
#region GetByIdAsync Tests
[Fact]
public async Task GetByIdAsync_WithExistingUser_ReturnsUserDto() { }

[Fact]
public async Task GetByIdAsync_WithNonExistentUser_ReturnsNull() { }
#endregion
```

## Dependencies

- **xUnit** - Testing framework
- **Moq** - Mocking library for creating test doubles
- **Microsoft.NET.Test.Sdk** - Test SDK for .NET
- **coverlet.collector** - Code coverage collection

## Running Tests

Run all tests:
```bash
dotnet test
```

Run tests with detailed output:
```bash
dotnet test --verbosity normal
```

Run tests with code coverage:
```bash
dotnet test /p:CollectCoverage=true
```

Run specific test class:
```bash
dotnet test --filter "FullyQualifiedName~UserServiceTests"
```

## Test Coverage

Current test coverage includes:

### Core Models
- ✅ Result pattern (success/failure scenarios)
- ✅ File DTOs
- ✅ Custom exceptions with context

### Security
- ✅ Password hashing and verification
- ✅ BCrypt implementation

### Services
- ✅ UserService (CRUD operations, validation, role management)
- ✅ PageService (CRUD operations, versioning, tags, caching)
- 🔄 Additional services to be added

### Areas for Future Coverage
- Repository implementations
- Event handlers
- Configuration services
- File storage services
- Page services
- Category services
- Menu services
- Search services

## Best Practices

1. **Isolation**: Each test should be independent and not rely on other tests
2. **Single Responsibility**: Each test should verify one specific behavior
3. **Clear Naming**: Test names should clearly describe what is being tested
4. **Comprehensive Coverage**: Test both happy paths and error scenarios
5. **Mock External Dependencies**: Use mocks to isolate the unit under test
6. **Verify Behavior**: Use `Verify()` to ensure mocks were called as expected
7. **Test Edge Cases**: Include tests for boundary conditions and edge cases

## Contributing

When adding new tests:
1. Follow the existing naming conventions
2. Use the AAA pattern
3. Group related tests with `#region` directives
4. Add XML documentation comments for test classes
5. Ensure tests are independent and can run in any order
6. Mock all external dependencies
7. Test both success and failure scenarios
