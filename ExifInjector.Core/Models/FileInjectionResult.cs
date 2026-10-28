namespace ExifInjector.Core.Models;

public enum InjectionStatus
{
    Updated,
    Skipped,
    Failed
}

/// <summary>Outcome for a single file.</summary>
/// <param name="FilePath">Full path of the processed file.</param>
/// <param name="Status">Whether the file was updated, skipped or failed.</param>
/// <param name="AssignedDate">Date/time written to the file, when a date was written.</param>
/// <param name="WrittenTags">Tag names actually written.</param>
/// <param name="SkippedTags">Tag names skipped because the file's Exif version does not support them.</param>
/// <param name="Message">Human readable reason for a skip or failure.</param>
public sealed record FileInjectionResult(
    string FilePath,
    InjectionStatus Status,
    DateTime? AssignedDate = null,
    IReadOnlyList<string>? WrittenTags = null,
    IReadOnlyList<string>? SkippedTags = null,
    string? Message = null);

/// <summary>Aggregate outcome of a whole run.</summary>
public sealed record InjectionSummary(IReadOnlyList<FileInjectionResult> Results)
{
    public int UpdatedCount => Results.Count(r => r.Status == InjectionStatus.Updated);

    public int SkippedCount => Results.Count(r => r.Status == InjectionStatus.Skipped);

    public int FailedCount => Results.Count(r => r.Status == InjectionStatus.Failed);

    public int TotalCount => Results.Count;
}
