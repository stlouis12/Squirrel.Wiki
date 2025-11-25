using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Squirrel.Wiki.Core.Exceptions;

namespace Squirrel.Wiki.Core.Services.Content;

/// <summary>
/// Service for generating URL-friendly slugs from text
/// </summary>
public class SlugGenerator : ISlugGenerator
{
    // Regex patterns for slug generation
    private static readonly Regex NonAlphanumericRegex = new(@"[^a-z0-9\s-]", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex MultipleHyphensRegex = new(@"-+", RegexOptions.Compiled);

    /// <summary>
    /// Generates a URL-friendly slug from the given text
    /// </summary>
    /// <param name="text">The text to convert to a slug</param>
    /// <returns>A URL-friendly slug</returns>
    public string GenerateSlug(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // Convert to lowercase
        var slug = text.ToLowerInvariant();

        // Remove non-alphanumeric characters (except spaces and hyphens)
        slug = NonAlphanumericRegex.Replace(slug, "");

        // Replace whitespace with hyphens
        slug = WhitespaceRegex.Replace(slug, "-");

        // Replace multiple consecutive hyphens with a single hyphen
        slug = MultipleHyphensRegex.Replace(slug, "-");

        // Trim hyphens from start and end
        slug = slug.Trim('-');

        return slug;
    }

    /// <summary>
    /// Generates a unique slug by checking for existing slugs and appending a number if needed
    /// </summary>
    /// <param name="text">The text to convert to a slug</param>
    /// <param name="existingSlugChecker">Function to check if a slug already exists (returns true if exists)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A unique URL-friendly slug</returns>
    public async Task<string> GenerateUniqueSlugAsync(
        string text,
        Func<string, CancellationToken, Task<bool>> existingSlugChecker,
        CancellationToken cancellationToken = default)
    {
        var baseSlug = GenerateSlug(text);
        var slug = baseSlug;
        var counter = 1;

        // Keep trying with incrementing numbers until we find a unique slug
        while (await existingSlugChecker(slug, cancellationToken))
        {
            slug = $"{baseSlug}-{counter}";
            counter++;

            // Safety check to prevent infinite loops
            if (counter > 1000)
            {
                throw new BusinessRuleException(
                    $"Unable to generate unique slug after 1000 attempts. The base slug '{baseSlug}' may be too common.",
                    "SLUG_GENERATION_FAILED"
                ).WithContext("BaseSlug", baseSlug)
                 .WithContext("OriginalText", text)
                 .WithContext("Attempts", counter);
            }
        }

        return slug;
    }
}
