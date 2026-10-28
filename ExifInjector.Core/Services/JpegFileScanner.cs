namespace ExifInjector.Core.Services;

/// <summary>Finds the JPEG files that a run should operate on.</summary>
public static class JpegFileScanner
{
    private static readonly string[] JpegExtensions = [".jpg", ".jpeg", ".jpe", ".jfif"];

    public static bool IsJpeg(string filePath) =>
        JpegExtensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the JPEG files in a folder, sorted by name for a predictable date sequence.</summary>
    public static IReadOnlyList<string> EnumerateFolder(string folderPath, bool includeSubfolders)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory
            .EnumerateFiles(folderPath, "*", searchOption)
            .Where(IsJpeg)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Filters an explicit selection down to JPEG files, sorted by name.</summary>
    public static IReadOnlyList<string> FilterSelection(IEnumerable<string> filePaths) =>
        filePaths
            .Where(IsJpeg)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
