using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Tests.Models;

/// <summary>
/// Unit tests for the Result<T> class
/// </summary>
public class ResultTests
{
    #region Success Tests

    [Fact]
    public void Success_CreatesSuccessfulResult_WithValue()
    {
        // Arrange
        var expectedValue = "test value";

        // Act
        var result = Result<string>.Success(expectedValue);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(expectedValue, result.Value);
        Assert.Null(result.Error);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Success_WithNullValue_CreatesSuccessfulResult()
    {
        // Act
        var result = Result<string?>.Success(null);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    #endregion

    #region Failure Tests

    [Fact]
    public void Failure_CreatesFailedResult_WithErrorAndCode()
    {
        // Arrange
        var errorMessage = "Something went wrong";
        var errorCode = "ERROR_001";

        // Act
        var result = Result<string>.Failure(errorMessage, errorCode);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        Assert.Equal(errorMessage, result.Error);
        Assert.Equal(errorCode, result.ErrorCode);
    }

    [Fact]
    public void FromException_CreatesFailedResult_WithExceptionMessage()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var errorCode = "CUSTOM_CODE";

        // Act
        var result = Result<string>.FromException(exception, errorCode);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(exception.Message, result.Error);
        Assert.Equal(errorCode, result.ErrorCode);
    }

    [Fact]
    public void FromException_WithoutErrorCode_UsesDefaultCode()
    {
        // Arrange
        var exception = new Exception("Test");

        // Act
        var result = Result<string>.FromException(exception);

        // Assert
        Assert.Equal("EXCEPTION", result.ErrorCode);
    }

    #endregion

    #region Context Tests

    [Fact]
    public void WithContext_AddsContextInformation()
    {
        // Arrange
        var result = Result<string>.Success("test");
        var key = "userId";
        var value = 123;

        // Act
        result.WithContext(key, value);

        // Assert
        Assert.NotNull(result.Context);
        Assert.True(result.Context.ContainsKey(key));
        Assert.Equal(value, result.Context[key]);
    }

    [Fact]
    public void WithContext_CanAddMultipleContextItems()
    {
        // Arrange
        var result = Result<string>.Success("test");

        // Act
        result.WithContext("key1", "value1")
              .WithContext("key2", 42)
              .WithContext("key3", true);

        // Assert
        Assert.Equal(3, result.Context!.Count);
        Assert.Equal("value1", result.Context["key1"]);
        Assert.Equal(42, result.Context["key2"]);
        Assert.Equal(true, result.Context["key3"]);
    }

    [Fact]
    public void WithContext_OverwritesExistingKey()
    {
        // Arrange
        var result = Result<string>.Success("test");
        var key = "testKey";

        // Act
        result.WithContext(key, "first")
              .WithContext(key, "second");

        // Assert
        Assert.Equal("second", result.Context![key]);
    }

    #endregion

    #region Map Tests

    [Fact]
    public void Map_OnSuccess_TransformsValue()
    {
        // Arrange
        var result = Result<int>.Success(5);

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        Assert.True(mapped.IsSuccess);
        Assert.Equal(10, mapped.Value);
    }

    [Fact]
    public void Map_OnFailure_PreservesError()
    {
        // Arrange
        var result = Result<int>.Failure("Error", "CODE");

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        Assert.False(mapped.IsSuccess);
        Assert.Equal("Error", mapped.Error);
        Assert.Equal("CODE", mapped.ErrorCode);
    }

    [Fact]
    public void Map_WhenMapperThrows_ReturnsFailure()
    {
        // Arrange
        var result = Result<int>.Success(5);

        // Act
        var mapped = result.Map<int>(x => throw new InvalidOperationException("Mapper failed"));

        // Assert
        Assert.False(mapped.IsSuccess);
        Assert.Equal("Mapper failed", mapped.Error);
        Assert.Equal("EXCEPTION", mapped.ErrorCode);
    }

    [Fact]
    public async Task MapAsync_OnSuccess_TransformsValueAsynchronously()
    {
        // Arrange
        var result = Result<int>.Success(5);

        // Act
        var mapped = await result.MapAsync(async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });

        // Assert
        Assert.True(mapped.IsSuccess);
        Assert.Equal(10, mapped.Value);
    }

    [Fact]
    public async Task MapAsync_OnFailure_PreservesError()
    {
        // Arrange
        var result = Result<int>.Failure("Error", "CODE");

        // Act
        var mapped = await result.MapAsync(async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });

        // Assert
        Assert.False(mapped.IsSuccess);
        Assert.Equal("Error", mapped.Error);
        Assert.Equal("CODE", mapped.ErrorCode);
    }

    #endregion

    #region OnSuccess/OnFailure Tests

    [Fact]
    public void OnSuccess_WhenSuccessful_ExecutesAction()
    {
        // Arrange
        var result = Result<int>.Success(42);
        var actionExecuted = false;
        var capturedValue = 0;

        // Act
        result.OnSuccess(value =>
        {
            actionExecuted = true;
            capturedValue = value;
        });

        // Assert
        Assert.True(actionExecuted);
        Assert.Equal(42, capturedValue);
    }

    [Fact]
    public void OnSuccess_WhenFailed_DoesNotExecuteAction()
    {
        // Arrange
        var result = Result<int>.Failure("Error", "CODE");
        var actionExecuted = false;

        // Act
        result.OnSuccess(_ => actionExecuted = true);

        // Assert
        Assert.False(actionExecuted);
    }

    [Fact]
    public void OnFailure_WhenFailed_ExecutesAction()
    {
        // Arrange
        var result = Result<int>.Failure("Test error", "TEST_CODE");
        var actionExecuted = false;
        var capturedError = "";
        var capturedCode = "";

        // Act
        result.OnFailure((error, code) =>
        {
            actionExecuted = true;
            capturedError = error;
            capturedCode = code;
        });

        // Assert
        Assert.True(actionExecuted);
        Assert.Equal("Test error", capturedError);
        Assert.Equal("TEST_CODE", capturedCode);
    }

    [Fact]
    public void OnFailure_WhenSuccessful_DoesNotExecuteAction()
    {
        // Arrange
        var result = Result<int>.Success(42);
        var actionExecuted = false;

        // Act
        result.OnFailure((_, _) => actionExecuted = true);

        // Assert
        Assert.False(actionExecuted);
    }

    #endregion

    #region Match Tests

    [Fact]
    public void Match_OnSuccess_ExecutesSuccessFunction()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var output = result.Match(
            onSuccess: value => $"Success: {value}",
            onFailure: (error, code) => $"Failure: {error}"
        );

        // Assert
        Assert.Equal("Success: 42", output);
    }

    [Fact]
    public void Match_OnFailure_ExecutesFailureFunction()
    {
        // Arrange
        var result = Result<int>.Failure("Error occurred", "ERR_001");

        // Act
        var output = result.Match(
            onSuccess: value => $"Success: {value}",
            onFailure: (error, code) => $"Failure: {error} ({code})"
        );

        // Assert
        Assert.Equal("Failure: Error occurred (ERR_001)", output);
    }

    #endregion
}

/// <summary>
/// Unit tests for the non-generic Result class
/// </summary>
public class ResultNonGenericTests
{
    #region Success Tests

    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        // Act
        var result = Result.Success();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
        Assert.Null(result.ErrorCode);
    }

    #endregion

    #region Failure Tests

    [Fact]
    public void Failure_CreatesFailedResult_WithErrorAndCode()
    {
        // Arrange
        var errorMessage = "Operation failed";
        var errorCode = "FAIL_001";

        // Act
        var result = Result.Failure(errorMessage, errorCode);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(errorMessage, result.Error);
        Assert.Equal(errorCode, result.ErrorCode);
    }

    [Fact]
    public void FromException_CreatesFailedResult()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument");

        // Act
        var result = Result.FromException(exception);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(exception.Message, result.Error);
        Assert.Equal("EXCEPTION", result.ErrorCode);
    }

    #endregion

    #region Context Tests

    [Fact]
    public void WithContext_AddsContextInformation()
    {
        // Arrange
        var result = Result.Success();

        // Act
        result.WithContext("operation", "delete")
              .WithContext("itemId", 123);

        // Assert
        Assert.NotNull(result.Context);
        Assert.Equal(2, result.Context.Count);
        Assert.Equal("delete", result.Context["operation"]);
        Assert.Equal(123, result.Context["itemId"]);
    }

    #endregion

    #region OnSuccess/OnFailure Tests

    [Fact]
    public void OnSuccess_WhenSuccessful_ExecutesAction()
    {
        // Arrange
        var result = Result.Success();
        var actionExecuted = false;

        // Act
        result.OnSuccess(() => actionExecuted = true);

        // Assert
        Assert.True(actionExecuted);
    }

    [Fact]
    public void OnSuccess_WhenFailed_DoesNotExecuteAction()
    {
        // Arrange
        var result = Result.Failure("Error", "CODE");
        var actionExecuted = false;

        // Act
        result.OnSuccess(() => actionExecuted = true);

        // Assert
        Assert.False(actionExecuted);
    }

    [Fact]
    public void OnFailure_WhenFailed_ExecutesAction()
    {
        // Arrange
        var result = Result.Failure("Error", "CODE");
        var actionExecuted = false;

        // Act
        result.OnFailure((_, _) => actionExecuted = true);

        // Assert
        Assert.True(actionExecuted);
    }

    #endregion

    #region Match Tests

    [Fact]
    public void Match_OnSuccess_ExecutesSuccessFunction()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var output = result.Match(
            onSuccess: () => "Operation succeeded",
            onFailure: (error, code) => $"Failed: {error}"
        );

        // Assert
        Assert.Equal("Operation succeeded", output);
    }

    [Fact]
    public void Match_OnFailure_ExecutesFailureFunction()
    {
        // Arrange
        var result = Result.Failure("Something broke", "BROKE");

        // Act
        var output = result.Match(
            onSuccess: () => "Success",
            onFailure: (error, code) => $"{code}: {error}"
        );

        // Assert
        Assert.Equal("BROKE: Something broke", output);
    }

    #endregion

    #region ToResult Tests

    [Fact]
    public void ToResult_OnSuccess_CreatesSuccessfulGenericResult()
    {
        // Arrange
        var result = Result.Success();
        var value = "test value";

        // Act
        var genericResult = result.ToResult(value);

        // Assert
        Assert.True(genericResult.IsSuccess);
        Assert.Equal(value, genericResult.Value);
    }

    [Fact]
    public void ToResult_OnFailure_CreatesFailedGenericResult()
    {
        // Arrange
        var result = Result.Failure("Error", "CODE");

        // Act
        var genericResult = result.ToResult("test");

        // Assert
        Assert.False(genericResult.IsSuccess);
        Assert.Equal("Error", genericResult.Error);
        Assert.Equal("CODE", genericResult.ErrorCode);
    }

    #endregion
}
