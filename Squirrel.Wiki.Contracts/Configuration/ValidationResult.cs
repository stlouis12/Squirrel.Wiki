namespace Squirrel.Wiki.Contracts.Configuration;

/// <summary>
/// Result of validating a configuration value
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether the validation passed
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validation error messages (empty if valid)
    /// </summary>
    public IList<string> Errors { get; set; } = new List<string>();

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    public static ValidationResult Success()
    {
        return new ValidationResult { IsValid = true };
    }

    /// <summary>
    /// Creates a failed validation result with error messages
    /// </summary>
    public static ValidationResult Failure(params string[] errors)
    {
        return new ValidationResult
        {
            IsValid = false,
            Errors = new List<string>(errors)
        };
    }

    /// <summary>
    /// Creates a failed validation result with a single error message
    /// </summary>
    public static ValidationResult Failure(string error)
    {
        return new ValidationResult
        {
            IsValid = false,
            Errors = new List<string> { error }
        };
    }
}
