using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExifInjector.App.Services;
using ExifInjector.Core.Exif;
using ExifInjector.Core.Models;
using ExifInjector.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExifInjector.App.ViewModels;

/// <summary>
/// Presentation state for the main window. All real work is delegated to
/// <see cref="IMetadataInjectionService"/> and <see cref="IFilePickerService"/>.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private static readonly char[] KeywordSeparators = [',', ';', '\r', '\n'];

    private readonly IMetadataInjectionService _injectionService;
    private readonly IFilePickerService _filePicker;
    private readonly IExifTool _exifTool;
    private readonly ILogger<MainViewModel> _logger;
    private readonly InjectionOptions _injectionOptions;

    private CancellationTokenSource? _cancellationSource;

    public MainViewModel(
        IMetadataInjectionService injectionService,
        IFilePickerService filePicker,
        IExifTool exifTool,
        IOptions<InjectionOptions> injectionOptions,
        ILogger<MainViewModel> logger)
    {
        _injectionService = injectionService;
        _filePicker = filePicker;
        _exifTool = exifTool;
        _logger = logger;
        _injectionOptions = injectionOptions.Value;

        var now = DateTime.Now;
        SelectedDate = new DateTimeOffset(now.Date, DateTimeOffset.Now.Offset);
        SelectedTime = new TimeSpan(now.Hour, now.Minute, 0);
        IncludeSubfolders = _injectionOptions.IncludeSubfolders;
        IncrementMinutes = Math.Max(0, _injectionOptions.IncrementPerFile.TotalMinutes);
    }

    [ObservableProperty]
    public partial DateTimeOffset? SelectedDate { get; set; }

    [ObservableProperty]
    public partial TimeSpan SelectedTime { get; set; }

    [ObservableProperty]
    public partial bool ApplyDateTaken { get; set; } = true;

    [ObservableProperty]
    public partial double IncrementMinutes { get; set; }

    [ObservableProperty]
    public partial string Caption { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Keywords { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Latitude { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Longitude { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UserComment { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool SkipFilesWithExistingDate { get; set; } = true;

    [ObservableProperty]
    public partial bool RestrictToDateWhenTagsUnsupported { get; set; }

    [ObservableProperty]
    public partial bool IncludeSubfolders { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Ready.";

    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    [ObservableProperty]
    public partial double ProgressMaximum { get; set; } = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SelectFilesCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    public partial bool IsBusy { get; set; }

    public ObservableCollection<string> Activity { get; } = [];

    private bool CanStart => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task SelectFilesAsync()
    {
        var selection = await _filePicker.PickJpegFilesAsync();
        if (selection.Count == 0)
        {
            StatusMessage = "No files selected.";
            return;
        }

        var files = JpegFileScanner.FilterSelection(selection);
        await RunAsync(files);
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task SelectFolderAsync()
    {
        var folder = await _filePicker.PickFolderAsync();
        if (string.IsNullOrWhiteSpace(folder))
        {
            StatusMessage = "No folder selected.";
            return;
        }

        IReadOnlyList<string> files;
        try
        {
            files = JpegFileScanner.EnumerateFolder(folder, IncludeSubfolders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to enumerate folder. Folder={Folder}", folder);
            StatusMessage = $"Unable to read folder: {ex.Message}";
            return;
        }

        if (files.Count == 0)
        {
            StatusMessage = $"No JPEG files found in {folder}.";
            return;
        }

        await RunAsync(files);
    }

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel()
    {
        _cancellationSource?.Cancel();
        StatusMessage = "Cancelling...";
    }

    [RelayCommand]
    private void ClearActivity()
    {
        Activity.Clear();
        StatusMessage = "Ready.";
    }

    private async Task RunAsync(IReadOnlyList<string> files)
    {
        if (!TryBuildRequest(files.Count, out var request, out var validationError))
        {
            StatusMessage = validationError!;
            Append($"ERROR  {validationError}");
            return;
        }

        IsBusy = true;
        ProgressValue = 0;
        ProgressMaximum = files.Count;
        _cancellationSource = new CancellationTokenSource();

        try
        {
            var version = await _exifTool.GetVersionAsync(_cancellationSource.Token);
            Append($"INFO   ExifTool {version} - processing {files.Count} file(s).");

            var progress = new Progress<FileInjectionResult>(OnFileProcessed);
            var summary = await _injectionService.InjectAsync(files, request!, progress, _cancellationSource.Token);

            StatusMessage =
                $"Done. Updated {summary.UpdatedCount}, skipped {summary.SkippedCount}, failed {summary.FailedCount} of {summary.TotalCount}.";
            Append($"INFO   {StatusMessage}");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled.";
            Append("INFO   Run cancelled by user.");
            _logger.LogInformation("Run cancelled by user");
        }
        catch (ExifToolException ex)
        {
            StatusMessage = ex.Message;
            Append($"ERROR  {ex.Message}");
            _logger.LogError(ex, "ExifTool is not usable");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unexpected error: {ex.Message}";
            Append($"ERROR  {ex.Message}");
            _logger.LogError(ex, "Unexpected failure during injection run");
        }
        finally
        {
            _cancellationSource.Dispose();
            _cancellationSource = null;
            IsBusy = false;
        }
    }

    private void OnFileProcessed(FileInjectionResult result)
    {
        ProgressValue++;

        var name = Path.GetFileName(result.FilePath);
        var line = result.Status switch
        {
            InjectionStatus.Updated => $"OK     {name} -> {Describe(result)}",
            InjectionStatus.Skipped => $"INFO   {name} skipped: {result.Message}",
            _ => $"ERROR  {name}: {result.Message}"
        };

        if (result.Status == InjectionStatus.Updated && result.SkippedTags is { Count: > 0 })
        {
            line += $" (skipped unsupported: {string.Join(", ", result.SkippedTags)})";
        }

        Append(line);
    }

    private static string Describe(FileInjectionResult result)
    {
        var parts = new List<string>();
        if (result.AssignedDate.HasValue)
        {
            parts.Add(result.AssignedDate.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }

        if (result.WrittenTags is { Count: > 0 })
        {
            parts.Add(string.Join(", ", result.WrittenTags));
        }

        return parts.Count == 0 ? "updated" : string.Join(" | ", parts);
    }

    private bool TryBuildRequest(int fileCount, out MetadataInjectionRequest? request, out string? error)
    {
        request = null;
        error = null;

        DateTime? dateTaken = null;
        if (ApplyDateTaken)
        {
            if (SelectedDate is null)
            {
                error = "Choose a date, or clear the \"Set date taken\" option.";
                return false;
            }

            dateTaken = SelectedDate.Value.Date.Add(SelectedTime);
        }

        if (!GpsCoordinates.TryParse(Latitude, Longitude, out var gps, out var gpsError))
        {
            error = gpsError;
            return false;
        }

        var keywords = Keywords
            .Split(KeywordSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        request = new MetadataInjectionRequest
        {
            DateTaken = dateTaken,
            IncrementPerFile = TimeSpan.FromMinutes(Math.Max(0, IncrementMinutes)),
            Caption = string.IsNullOrWhiteSpace(Caption) ? null : Caption,
            Keywords = keywords,
            Gps = gps,
            UserComment = string.IsNullOrWhiteSpace(UserComment) ? null : UserComment,
            SkipFilesWithExistingDateTaken = SkipFilesWithExistingDate,
            RestrictToDateWhenTagsUnsupported = RestrictToDateWhenTagsUnsupported
        };

        if (!request.HasAnyMetadata)
        {
            error = "Specify at least one field to write.";
            request = null;
            return false;
        }

        _logger.LogInformation(
            "Starting run. Files={FileCount} HasDate={HasDate} HasCaption={HasCaption} Keywords={KeywordCount} HasGps={HasGps} HasUserComment={HasUserComment} SkipExistingDates={SkipExisting} DateOnlyWhenUnsupported={Restrict}",
            fileCount,
            dateTaken.HasValue,
            !string.IsNullOrWhiteSpace(Caption),
            keywords.Count,
            gps is not null,
            !string.IsNullOrWhiteSpace(UserComment),
            SkipFilesWithExistingDate,
            RestrictToDateWhenTagsUnsupported);

        return true;
    }

    private void Append(string line)
    {
        Activity.Add($"{DateTime.Now:HH:mm:ss}  {line}");
        while (Activity.Count > 2000)
        {
            Activity.RemoveAt(0);
        }
    }
}
