using Windows.Storage.Pickers;

namespace ExifInjector.App.Services;

/// <summary>WinUI implementation of <see cref="IFilePickerService"/> using the WinRT pickers.</summary>
public sealed class FilePickerService : IFilePickerService
{
    private static readonly string[] JpegExtensions = [".jpg", ".jpeg", ".jpe", ".jfif"];

    private readonly WindowContext _windowContext;

    public FilePickerService(WindowContext windowContext)
    {
        _windowContext = windowContext;
    }

    public async Task<IReadOnlyList<string>> PickJpegFilesAsync()
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };

        foreach (var extension in JpegExtensions)
        {
            picker.FileTypeFilter.Add(extension);
        }

        Initialize(picker);

        var files = await picker.PickMultipleFilesAsync();
        return files is null ? [] : files.Select(file => file.Path).ToList();
    }

    public async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };

        picker.FileTypeFilter.Add("*");
        Initialize(picker);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private void Initialize(object picker) =>
        WinRT.Interop.InitializeWithWindow.Initialize(picker, _windowContext.WindowHandle);
}
