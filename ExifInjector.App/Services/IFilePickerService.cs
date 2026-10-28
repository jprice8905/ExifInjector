namespace ExifInjector.App.Services;

/// <summary>
/// UI-side file selection, abstracted so the view model never touches WinUI picker types directly.
/// </summary>
public interface IFilePickerService
{
    /// <summary>Shows a multi-select JPEG file dialog. Returns an empty list when cancelled.</summary>
    Task<IReadOnlyList<string>> PickJpegFilesAsync();

    /// <summary>Shows a folder dialog. Returns null when cancelled.</summary>
    Task<string?> PickFolderAsync();
}
