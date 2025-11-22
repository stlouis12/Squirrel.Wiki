namespace Squirrel.Wiki.Core.Security;

/// <summary>
/// Interface for password hashing operations
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a password
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a password against a hash
    /// </summary>
    bool VerifyPassword(string password, string hash);

    /// <summary>
    /// Checks if a hash needs to be rehashed (e.g., due to algorithm updates)
    /// </summary>
    bool NeedsRehash(string hash);
}
