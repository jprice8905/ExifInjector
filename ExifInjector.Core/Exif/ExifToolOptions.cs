namespace ExifInjector.Core.Exif;

/// <summary>
/// Configuration for locating and invoking ExifTool. Bound from the "ExifTool" section of appsettings.json.
/// </summary>
public sealed class ExifToolOptions
{
    public const string SectionName = "ExifTool";

    /// <summary>Full path to exiftool.exe. May also be a bare file name resolved through PATH.</summary>
    public string ExecutablePath { get; set; } = @"c:\Tools\ExifTool.exe";

    /// <summary>Maximum time a single ExifTool invocation may run.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>When true, ExifTool rewrites files in place instead of leaving *_original backups.</summary>
    public bool OverwriteOriginal { get; set; } = true;
}
