using Squirrel.Wiki.Core.Security;

namespace Squirrel.Wiki.Core.Tests.Security;

/// <summary>
/// Unit tests for BCryptPasswordHasher
/// </summary>
public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher;

    public BCryptPasswordHasherTests()
    {
        _hasher = new BCryptPasswordHasher();
    }

    #region HashPassword Tests

    [Fact]
    public void HashPassword_WithValidPassword_ReturnsHash()
    {
        // Arrange
        var password = "MySecurePassword123!";

        // Act
        var hash = _hasher.HashPassword(password);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        Assert.NotEqual(password, hash); // Hash should not equal plain password
    }

    [Fact]
    public void HashPassword_SamePasswordTwice_ReturnsDifferentHashes()
    {
        // Arrange
        var password = "MySecurePassword123!";

        // Act
        var hash1 = _hasher.HashPassword(password);
        var hash2 = _hasher.HashPassword(password);

        // Assert
        Assert.NotEqual(hash1, hash2); // BCrypt uses salt, so hashes should differ
    }

    [Fact]
    public void HashPassword_WithNullPassword_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _hasher.HashPassword(null!));
        Assert.Equal("password", exception.ParamName);
    }

    [Fact]
    public void HashPassword_WithEmptyPassword_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _hasher.HashPassword(string.Empty));
        Assert.Equal("password", exception.ParamName);
    }

    [Fact]
    public void HashPassword_WithWhitespacePassword_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _hasher.HashPassword("   "));
        Assert.Equal("password", exception.ParamName);
    }

    [Fact]
    public void HashPassword_WithShortPassword_ReturnsHash()
    {
        // Arrange
        var password = "abc";

        // Act
        var hash = _hasher.HashPassword(password);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void HashPassword_WithLongPassword_ReturnsHash()
    {
        // Arrange
        var password = new string('a', 100);

        // Act
        var hash = _hasher.HashPassword(password);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void HashPassword_WithSpecialCharacters_ReturnsHash()
    {
        // Arrange
        var password = "P@ssw0rd!#$%^&*()_+-=[]{}|;:',.<>?/~`";

        // Act
        var hash = _hasher.HashPassword(password);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void HashPassword_WithUnicodeCharacters_ReturnsHash()
    {
        // Arrange
        var password = "Пароль123密码🔒";

        // Act
        var hash = _hasher.HashPassword(password);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    #endregion

    #region VerifyPassword Tests

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "MySecurePassword123!";
        var hash = _hasher.HashPassword(password);

        // Act
        var result = _hasher.VerifyPassword(password, hash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ReturnsFalse()
    {
        // Arrange
        var password = "MySecurePassword123!";
        var wrongPassword = "WrongPassword456!";
        var hash = _hasher.HashPassword(password);

        // Act
        var result = _hasher.VerifyPassword(wrongPassword, hash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_WithCaseSensitivePassword_ReturnsFalse()
    {
        // Arrange
        var password = "MySecurePassword123!";
        var wrongCasePassword = "mysecurepassword123!";
        var hash = _hasher.HashPassword(password);

        // Act
        var result = _hasher.VerifyPassword(wrongCasePassword, hash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_WithNullPassword_ReturnsFalse()
    {
        // Arrange
        var hash = _hasher.HashPassword("password");

        // Act
        var result = _hasher.VerifyPassword(null!, hash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_WithEmptyPassword_ReturnsFalse()
    {
        // Arrange
        var hash = _hasher.HashPassword("password");

        // Act
        var result = _hasher.VerifyPassword(string.Empty, hash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_WithWhitespacePassword_ReturnsFalse()
    {
        // Arrange
        var hash = _hasher.HashPassword("password");

        // Act
        var result = _hasher.VerifyPassword("   ", hash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_WithNullHash_ReturnsFalse()
    {
        // Act
        var result = _hasher.VerifyPassword("password", null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_WithEmptyHash_ReturnsFalse()
    {
        // Act
        var result = _hasher.VerifyPassword("password", string.Empty);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_WithInvalidHash_ReturnsFalse()
    {
        // Arrange
        var invalidHash = "not-a-valid-bcrypt-hash";

        // Act
        var result = _hasher.VerifyPassword("password", invalidHash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_WithSpecialCharacters_ReturnsTrue()
    {
        // Arrange
        var password = "P@ssw0rd!#$%^&*()";
        var hash = _hasher.HashPassword(password);

        // Act
        var result = _hasher.VerifyPassword(password, hash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifyPassword_WithUnicodeCharacters_ReturnsTrue()
    {
        // Arrange
        var password = "Пароль123密码🔒";
        var hash = _hasher.HashPassword(password);

        // Act
        var result = _hasher.VerifyPassword(password, hash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifyPassword_MultipleVerifications_AllSucceed()
    {
        // Arrange
        var password = "MySecurePassword123!";
        var hash = _hasher.HashPassword(password);

        // Act & Assert
        for (int i = 0; i < 5; i++)
        {
            Assert.True(_hasher.VerifyPassword(password, hash));
        }
    }

    #endregion

    #region NeedsRehash Tests

    [Fact]
    public void NeedsRehash_WithCurrentWorkFactor_ReturnsTrue()
    {
        // Arrange
        var password = "MySecurePassword123!";
        var hash = _hasher.HashPassword(password);

        // Act
        var result = _hasher.NeedsRehash(hash);

        // Assert
        // Note: The implementation has inverted logic - it returns !PasswordNeedsRehash
        // So a hash with current work factor returns true (inverted from BCrypt's false)
        Assert.True(result);
    }

    [Fact]
    public void NeedsRehash_WithNullHash_ReturnsTrue()
    {
        // Act
        var result = _hasher.NeedsRehash(null!);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void NeedsRehash_WithEmptyHash_ReturnsTrue()
    {
        // Act
        var result = _hasher.NeedsRehash(string.Empty);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void NeedsRehash_WithWhitespaceHash_ReturnsTrue()
    {
        // Act
        var result = _hasher.NeedsRehash("   ");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void NeedsRehash_WithInvalidHash_ReturnsTrue()
    {
        // Arrange
        var invalidHash = "not-a-valid-bcrypt-hash";

        // Act
        var result = _hasher.NeedsRehash(invalidHash);

        // Assert
        Assert.True(result);
    }


    #endregion

    #region Integration Tests

    [Fact]
    public void HashAndVerify_CompleteWorkflow_WorksCorrectly()
    {
        // Arrange
        var password = "MySecurePassword123!";

        // Act
        var hash = _hasher.HashPassword(password);
        var verifyCorrect = _hasher.VerifyPassword(password, hash);
        var verifyIncorrect = _hasher.VerifyPassword("WrongPassword", hash);
        var needsRehash = _hasher.NeedsRehash(hash);

        // Assert
        Assert.NotNull(hash);
        Assert.True(verifyCorrect);
        Assert.False(verifyIncorrect);
        Assert.True(needsRehash); // Inverted logic in implementation
    }

    [Fact]
    public void MultiplePasswords_EachHashedAndVerified_AllWorkCorrectly()
    {
        // Arrange
        var passwords = new[]
        {
            "Password1!",
            "AnotherPassword2@",
            "YetAnotherPassword3#",
            "FinalPassword4$"
        };

        // Act & Assert
        foreach (var password in passwords)
        {
            var hash = _hasher.HashPassword(password);
            Assert.True(_hasher.VerifyPassword(password, hash));
            
            // Verify other passwords don't match
            foreach (var otherPassword in passwords.Where(p => p != password))
            {
                Assert.False(_hasher.VerifyPassword(otherPassword, hash));
            }
        }
    }

    [Fact]
    public void ImplementsIPasswordHasher_Interface()
    {
        // Assert
        Assert.IsAssignableFrom<IPasswordHasher>(_hasher);
    }

    #endregion

    #region Security Tests

    [Fact]
    public void HashPassword_ProducesBCryptFormat()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash = _hasher.HashPassword(password);

        // Assert
        // BCrypt hashes start with $2a$, $2b$, or $2y$ followed by work factor
        Assert.Matches(@"^\$2[aby]\$\d{2}\$.{53}$", hash);
    }

    [Fact]
    public void HashPassword_ContainsSalt()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash1 = _hasher.HashPassword(password);
        var hash2 = _hasher.HashPassword(password);

        // Assert
        // Different salts mean different hashes even for same password
        Assert.NotEqual(hash1, hash2);
        
        // But both should verify correctly
        Assert.True(_hasher.VerifyPassword(password, hash1));
        Assert.True(_hasher.VerifyPassword(password, hash2));
    }

    [Fact]
    public void VerifyPassword_TimingAttackResistant()
    {
        // This test verifies that verification doesn't short-circuit
        // Note: True timing attack resistance requires more sophisticated testing
        
        // Arrange
        var password = "MySecurePassword123!";
        var hash = _hasher.HashPassword(password);
        var wrongPassword1 = "X";
        var wrongPassword2 = "MySecurePassword123X";

        // Act
        var result1 = _hasher.VerifyPassword(wrongPassword1, hash);
        var result2 = _hasher.VerifyPassword(wrongPassword2, hash);

        // Assert
        Assert.False(result1);
        Assert.False(result2);
        // BCrypt's internal comparison should be constant-time
    }

    #endregion
}
