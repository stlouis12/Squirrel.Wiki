using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Squirrel.Wiki.Core.Security;

/// <summary>
/// Service for encrypting and decrypting sensitive configuration values using ASP.NET Core Data Protection
/// </summary>
public class SecretEncryptionService : ISecretEncryptionService
{
    private readonly IDataProtector _protector;
    private readonly ILogger<SecretEncryptionService> _logger;
    private const string EncryptedPrefix = "ENC:";
    private const string Purpose = "Squirrel.Wiki.PluginSecrets";

    public SecretEncryptionService(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SecretEncryptionService> logger)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return plainText;
        }

        try
        {
            var encrypted = _protector.Protect(plainText);
            return $"{EncryptedPrefix}{encrypted}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt value");
            throw new InvalidOperationException("Failed to encrypt value", ex);
        }
    }

    /// <inheritdoc/>
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return cipherText;
        }

        if (!IsEncrypted(cipherText))
        {
            throw new InvalidOperationException("Value is not encrypted");
        }

        try
        {
            var encryptedValue = cipherText.Substring(EncryptedPrefix.Length);
            return _protector.Unprotect(encryptedValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt value");
            throw new InvalidOperationException("Failed to decrypt value. The encryption key may have changed.", ex);
        }
    }

    /// <inheritdoc/>
    public bool IsEncrypted(string value)
    {
        return !string.IsNullOrEmpty(value) && value.StartsWith(EncryptedPrefix);
    }

    /// <inheritdoc/>
    public string EncryptIfNeeded(string value)
    {
        if (string.IsNullOrEmpty(value) || IsEncrypted(value))
        {
            return value;
        }

        return Encrypt(value);
    }

    /// <inheritdoc/>
    public string DecryptIfNeeded(string value)
    {
        if (string.IsNullOrEmpty(value) || !IsEncrypted(value))
        {
            return value;
        }

        return Decrypt(value);
    }
}
