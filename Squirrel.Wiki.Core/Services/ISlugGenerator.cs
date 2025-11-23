using System.Threading;
using System.Threading.Tasks;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Service for generating URL-friendly slugs from text
/// </summary>
public interface ISlugGenerator
{
    /// <summary>
    /// Generates a URL-friendly slug from the given text
    /// </summary>
    /// <param name="text">The text to convert to a slug</param>
    /// <returns>A URL-friendly slug</returns>
    string GenerateSlug(string text);

    /// <summary>
    /// Generates a unique slug by checking for existing slugs and appending a number if needed
    /// </summary>
    /// <param name="text">The text to convert to a slug</param>
    /// <param name="existingSlugChecker">Function to check if a slug already exists</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A unique URL-friendly slug</returns>
    Task<string> GenerateUniqueSlugAsync(
        string text,
        Func<string, CancellationToken, Task<bool>> existingSlugChecker,
        CancellationToken cancellationToken = default);
}
