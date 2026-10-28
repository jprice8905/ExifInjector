using System.Globalization;
using System.Text;

namespace ExifInjector.Core.Exif;

/// <summary>
/// Decides which tags an individual file can safely receive, based on the Exif version it declares.
/// Older photos (Exif 2.0, or files with no Exif block at all) do not reliably support the
/// UserComment or GPS tags, so those writes can be suppressed instead of corrupting the file.
/// </summary>
public static class ExifCompatibility
{
    public const string ExifVersionTag = "ExifVersion";

    /// <summary>Exif 2.1 introduced UserComment in its current form.</summary>
    private const int UserCommentMinimumVersion = 210;

    /// <summary>Exif 2.2 is the first version with a fully specified GPS IFD.</summary>
    private const int GpsMinimumVersion = 220;

    public static bool SupportsUserComment(string? exifVersion) =>
        MeetsMinimum(exifVersion, UserCommentMinimumVersion);

    public static bool SupportsGps(string? exifVersion) =>
        MeetsMinimum(exifVersion, GpsMinimumVersion);

    /// <summary>
    /// IPTC is a separate standard carried in an APP13 segment, so it never depends on the Exif version.
    /// </summary>
    public static bool SupportsIptc(string? exifVersion) => true;

    /// <summary>
    /// Normalises the many shapes ExifTool can report ("0232", "2.32", "2.3") into hundredths, so
    /// "0232" and "2.32" both become 232. Returns null when the version is missing or unparsable.
    /// </summary>
    public static int? ParseVersion(string? exifVersion)
    {
        if (string.IsNullOrWhiteSpace(exifVersion))
        {
            return null;
        }

        var cleaned = new StringBuilder();
        foreach (var character in exifVersion)
        {
            if (char.IsDigit(character) || character == '.')
            {
                cleaned.Append(character);
            }
        }

        var text = cleaned.ToString();
        if (text.Length == 0)
        {
            return null;
        }

        if (text.Contains('.'))
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var dotted))
            {
                return null;
            }

            return (int)Math.Round(dotted * 100);
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var digits))
        {
            return null;
        }

        // "0232" and "232" both mean 2.32; a bare "2" means 2.00.
        return digits < 100 ? digits * 100 : digits;
    }

    private static bool MeetsMinimum(string? exifVersion, int minimumVersion)
    {
        var version = ParseVersion(exifVersion);
        return version.HasValue && version.Value >= minimumVersion;
    }
}
