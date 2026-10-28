namespace ExifInjector.Core.Exif;

/// <summary>Abstraction over the ExifTool command line so callers (and tests) never touch the process directly.</summary>
public interface IExifTool
{
    /// <summary>Returns the ExifTool version string, for example "13.10".</summary>
    Task<string> GetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads the requested tags from a file. Missing tags are simply absent from the result.</summary>
    Task<IReadOnlyDictionary<string, string>> ReadTagsAsync(
        string filePath,
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default);

    /// <summary>Writes the supplied tag assignments to a file.</summary>
    Task<ExifToolResult> WriteTagsAsync(
        string filePath,
        IEnumerable<ExifTagAssignment> assignments,
        CancellationToken cancellationToken = default);
}
