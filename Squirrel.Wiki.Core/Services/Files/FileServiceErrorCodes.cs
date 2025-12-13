namespace Squirrel.Wiki.Core.Services.Files;

/// <summary>
/// Shared error codes for file and folder service operations
/// </summary>
public static class FileServiceErrorCodes
{
    /// <summary>
    /// Error code when an entity (file or folder) is not found
    /// </summary>
    public const string NOT_FOUND = "NOT_FOUND";

    /// <summary>
    /// Error code when a GET operation fails
    /// </summary>
    public const string GET_ERROR = "GET_ERROR";
}
