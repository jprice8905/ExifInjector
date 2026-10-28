namespace ExifInjector.Core.Models;

/// <summary>Everything the user asked to inject, expressed independently of any UI technology.</summary>
public sealed class MetadataInjectionRequest
{
    /// <summary>Date/time assigned to the first file. Null leaves date tags untouched.</summary>
    public DateTime? DateTaken { get; init; }

    /// <summary>Offset added for each subsequent file so that no two files share a timestamp.</summary>
    public TimeSpan IncrementPerFile { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>IPTC Caption-Abstract value. Null or blank leaves the tag untouched.</summary>
    public string? Caption { get; init; }

    /// <summary>IPTC Keywords. An empty list leaves the tag untouched.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>GPS location. Null leaves GPS tags untouched.</summary>
    public GpsCoordinates? Gps { get; init; }

    /// <summary>Exif UserComment value. Null or blank leaves the tag untouched.</summary>
    public string? UserComment { get; init; }

    /// <summary>When true, files that already carry a valid Exif date are left alone entirely.</summary>
    public bool SkipFilesWithExistingDateTaken { get; init; }

    /// <summary>
    /// When true, tags that the file's Exif version does not support are skipped instead of written,
    /// leaving only the date tags updated on older files.
    /// </summary>
    public bool RestrictToDateWhenTagsUnsupported { get; init; }

    public bool HasAnyMetadata =>
        DateTaken.HasValue ||
        !string.IsNullOrWhiteSpace(Caption) ||
        Keywords.Count > 0 ||
        Gps is not null ||
        !string.IsNullOrWhiteSpace(UserComment);
}
