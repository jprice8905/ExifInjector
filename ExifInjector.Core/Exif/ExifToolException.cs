namespace ExifInjector.Core.Exif;

/// <summary>Thrown when ExifTool cannot be located or executed at all.</summary>
public sealed class ExifToolException : Exception
{
    public ExifToolException(string message) : base(message)
    {
    }

    public ExifToolException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
