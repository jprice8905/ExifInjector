using System.Globalization;
using ExifInjector.Core.Exif;
using ExifInjector.Core.Models;
using Microsoft.Extensions.Logging;

namespace ExifInjector.Core.Services;

/// <summary>
/// Orchestrates metadata injection: decides what to write for each file, delegates the actual
/// writing to <see cref="IExifTool"/>, and reports per-file results.
/// </summary>
public sealed class MetadataInjectionService : IMetadataInjectionService
{
    private const string ExifDateFormat = "yyyy:MM:dd HH:mm:ss";

    private static readonly string[] DateTags = ["EXIF:DateTimeOriginal", "EXIF:CreateDate", "EXIF:ModifyDate"];

    private static readonly string[] ProbeTags =
    [
        "EXIF:DateTimeOriginal",
        "EXIF:CreateDate",
        ExifCompatibility.ExifVersionTag
    ];

    private readonly IExifTool _exifTool;
    private readonly ILogger<MetadataInjectionService> _logger;

    public MetadataInjectionService(IExifTool exifTool, ILogger<MetadataInjectionService> logger)
    {
        _exifTool = exifTool;
        _logger = logger;
    }

    public async Task<InjectionSummary> InjectAsync(
        IReadOnlyList<string> filePaths,
        MetadataInjectionRequest request,
        IProgress<FileInjectionResult>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        ArgumentNullException.ThrowIfNull(request);

        var results = new List<FileInjectionResult>(filePaths.Count);
        var dateSequence = 0;

        foreach (var filePath in filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await ProcessFileAsync(filePath, request, dateSequence, cancellationToken)
                .ConfigureAwait(false);

            if (result.AssignedDate.HasValue)
            {
                dateSequence++;
            }

            results.Add(result);
            progress?.Report(result);
        }

        var summary = new InjectionSummary(results);
        _logger.LogInformation(
            "Run finished. Total={Total} Updated={Updated} Skipped={Skipped} Failed={Failed}",
            summary.TotalCount,
            summary.UpdatedCount,
            summary.SkippedCount,
            summary.FailedCount);

        return summary;
    }

    private async Task<FileInjectionResult> ProcessFileAsync(
        string filePath,
        MetadataInjectionRequest request,
        int dateSequence,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogError("File not found. File={File}", filePath);
            return new FileInjectionResult(filePath, InjectionStatus.Failed, Message: "File not found.");
        }

        IReadOnlyDictionary<string, string> existingTags;
        try
        {
            existingTags = await _exifTool.ReadTagsAsync(filePath, ProbeTags, cancellationToken).ConfigureAwait(false);
        }
        catch (ExifToolException ex)
        {
            _logger.LogError(ex, "Unable to read existing metadata. File={File}", filePath);
            return new FileInjectionResult(filePath, InjectionStatus.Failed, Message: ex.Message);
        }

        if (request.SkipFilesWithExistingDateTaken && HasValidDateTaken(existingTags, out var existingDate))
        {
            _logger.LogInformation(
                "Skipped file: it already has a valid date. File={File} ExistingDate={ExistingDate}",
                filePath,
                existingDate);
            return new FileInjectionResult(
                filePath,
                InjectionStatus.Skipped,
                Message: $"Already has a date ({existingDate}).");
        }

        existingTags.TryGetValue(ExifCompatibility.ExifVersionTag, out var exifVersion);

        var assignedDate = request.DateTaken?.Add(request.IncrementPerFile * dateSequence);
        var plan = BuildPlan(request, assignedDate, exifVersion);

        if (plan.SkippedTags.Count > 0)
        {
            _logger.LogInformation(
                "Skipped unsupported tags. File={File} ExifVersion={ExifVersion} SkippedTags={SkippedTags}",
                filePath,
                string.IsNullOrWhiteSpace(exifVersion) ? "none" : exifVersion,
                plan.SkippedTags);
        }

        if (plan.Assignments.Count == 0)
        {
            _logger.LogInformation("Skipped file: nothing left to write. File={File}", filePath);
            return new FileInjectionResult(
                filePath,
                InjectionStatus.Skipped,
                SkippedTags: plan.SkippedTags,
                Message: "No supported fields to write.");
        }

        try
        {
            var writeResult = await _exifTool.WriteTagsAsync(filePath, plan.Assignments, cancellationToken)
                .ConfigureAwait(false);

            if (!writeResult.Succeeded)
            {
                _logger.LogError(
                    "ExifTool write failed. File={File} ExitCode={ExitCode} Error={Error}",
                    filePath,
                    writeResult.ExitCode,
                    writeResult.ErrorMessage);
                return new FileInjectionResult(
                    filePath,
                    InjectionStatus.Failed,
                    SkippedTags: plan.SkippedTags,
                    Message: writeResult.ErrorMessage);
            }
        }
        catch (ExifToolException ex)
        {
            _logger.LogError(ex, "ExifTool write failed. File={File}", filePath);
            return new FileInjectionResult(
                filePath,
                InjectionStatus.Failed,
                SkippedTags: plan.SkippedTags,
                Message: ex.Message);
        }

        return new FileInjectionResult(
            filePath,
            InjectionStatus.Updated,
            assignedDate,
            plan.WrittenTags,
            plan.SkippedTags);
    }

    private static WritePlan BuildPlan(MetadataInjectionRequest request, DateTime? assignedDate, string? exifVersion)
    {
        var assignments = new List<ExifTagAssignment>();
        var writtenTags = new List<string>();
        var skippedTags = new List<string>();
        var restrict = request.RestrictToDateWhenTagsUnsupported;

        if (assignedDate.HasValue)
        {
            var value = assignedDate.Value.ToString(ExifDateFormat, CultureInfo.InvariantCulture);
            foreach (var tag in DateTags)
            {
                assignments.Add(new ExifTagAssignment(tag, value));
                writtenTags.Add(tag);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Caption))
        {
            if (restrict && !ExifCompatibility.SupportsIptc(exifVersion))
            {
                skippedTags.Add("IPTC:Caption-Abstract");
            }
            else
            {
                assignments.Add(new ExifTagAssignment("IPTC:Caption-Abstract", request.Caption.Trim()));
                writtenTags.Add("IPTC:Caption-Abstract");
            }
        }

        if (request.Keywords.Count > 0)
        {
            if (restrict && !ExifCompatibility.SupportsIptc(exifVersion))
            {
                skippedTags.Add("IPTC:Keywords");
            }
            else
            {
                // Repeated plain assignments replace the existing list and build the new one,
                // so repeated runs do not accumulate duplicate keywords.
                assignments.AddRange(request.Keywords.Select(k => new ExifTagAssignment("IPTC:Keywords", k)));
                writtenTags.Add("IPTC:Keywords");
            }
        }

        if (request.Gps is not null)
        {
            if (restrict && !ExifCompatibility.SupportsGps(exifVersion))
            {
                skippedTags.Add("EXIF:GPS");
            }
            else
            {
                assignments.Add(new ExifTagAssignment("EXIF:GPSLatitude", request.Gps.LatitudeValue));
                assignments.Add(new ExifTagAssignment("EXIF:GPSLatitudeRef", request.Gps.LatitudeRef));
                assignments.Add(new ExifTagAssignment("EXIF:GPSLongitude", request.Gps.LongitudeValue));
                assignments.Add(new ExifTagAssignment("EXIF:GPSLongitudeRef", request.Gps.LongitudeRef));
                writtenTags.Add("EXIF:GPS");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.UserComment))
        {
            if (restrict && !ExifCompatibility.SupportsUserComment(exifVersion))
            {
                skippedTags.Add("EXIF:UserComment");
            }
            else
            {
                assignments.Add(new ExifTagAssignment("EXIF:UserComment", request.UserComment.Trim()));
                writtenTags.Add("EXIF:UserComment");
            }
        }

        return new WritePlan(assignments, writtenTags, skippedTags);
    }

    private static bool HasValidDateTaken(IReadOnlyDictionary<string, string> tags, out string? existingDate)
    {
        foreach (var tag in new[] { "DateTimeOriginal", "CreateDate" })
        {
            if (!tags.TryGetValue(tag, out var value) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (value.StartsWith("0000", StringComparison.Ordinal))
            {
                continue;
            }

            if (DateTime.TryParseExact(
                    value.Trim(),
                    [ExifDateFormat, "yyyy:MM:dd HH:mm:sszzz", "yyyy:MM:dd HH:mm:ss.FFF"],
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _))
            {
                existingDate = value.Trim();
                return true;
            }
        }

        existingDate = null;
        return false;
    }

    private sealed record WritePlan(
        IReadOnlyList<ExifTagAssignment> Assignments,
        IReadOnlyList<string> WrittenTags,
        IReadOnlyList<string> SkippedTags);
}
