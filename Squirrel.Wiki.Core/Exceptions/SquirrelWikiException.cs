namespace Squirrel.Wiki.Core.Exceptions;

/// <summary>
/// Base exception for all Squirrel.Wiki custom exceptions
/// </summary>
public class SquirrelWikiException : Exception
{
    public SquirrelWikiException(string message) : base(message)
    {
    }

    public SquirrelWikiException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}
