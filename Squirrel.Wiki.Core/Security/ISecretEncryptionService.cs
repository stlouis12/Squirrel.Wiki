namespace Squirrel.Wiki.Core.Security;

/// <summary>
/// Service for encrypting and decrypting sensitive configuration values
/// </summary>
public interface ISecretEncryptionService
{
    /// <summary>
    /// Encrypt a plain text value
    /// </summary>
    /// <param name="plainText">The value to encrypt</param>
    /// <returns>The encrypted value with a prefix indicating it's encrypted</returns>
    string Encrypt(string plainText);

    /// <summary>
    /// Decrypt an encrypted value
    /// </summary>
    /// <param name="cipherText">The encrypted value</param>
    /// <returns>The decrypted plain text value</returns>
    string Decrypt(string cipherText);

    /// <summary>
    /// Check if a value is encrypted
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <returns>True if the value is encrypted, false otherwise</returns>
    bool IsEncrypted(string value);

    /// <summary>
    /// Encrypt a value if it's not already encrypted
    /// </summary>
    /// <param name="value">The value to encrypt</param>
    /// <returns>The encrypted value, or the original if already encrypted</returns>
    string EncryptIfNeeded(string value);

    /// <summary>
    /// Decrypt a value if it's encrypted, otherwise return as-is
    /// </summary>
    /// <param name="value">The value to decrypt</param>
    /// <returns>The decrypted value, or the original if not encrypted</returns>
    string DecryptIfNeeded(string value);
}
