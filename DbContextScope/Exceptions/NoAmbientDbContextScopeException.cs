namespace DbContextScope.Exceptions;

using System;

/// <summary>
/// A custom exception that is thrown when an operation which is required to be run in the presence
/// of an ambient <see cref="DbContextScope"/> is run with no such scope present.
/// </summary>
public class NoAmbientDbContextScopeException : Exception
{
    public NoAmbientDbContextScopeException()
    {
    }

    public NoAmbientDbContextScopeException(string message)
        : base(message)
    {
    }

    public NoAmbientDbContextScopeException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
