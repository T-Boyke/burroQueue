namespace burroQueue.Domain.Exceptions;

public sealed class InvalidTrackUrlException : Exception
{
    public InvalidTrackUrlException(string message)
        : base(message)
    {
    }

    public InvalidTrackUrlException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

