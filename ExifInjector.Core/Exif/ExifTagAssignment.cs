namespace ExifInjector.Core.Exif;

/// <summary>A single "-Tag=Value" assignment passed to ExifTool. Repeat the same tag to append list values.</summary>
/// <param name="Tag">Tag name, optionally group qualified (for example "EXIF:DateTimeOriginal").</param>
/// <param name="Value">Value to write. An empty string deletes the tag.</param>
public sealed record ExifTagAssignment(string Tag, string Value);
