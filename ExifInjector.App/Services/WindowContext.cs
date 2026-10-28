namespace ExifInjector.App.Services;

/// <summary>Holds the main window handle so services that need an owner HWND can resolve it.</summary>
public sealed class WindowContext
{
    public nint WindowHandle { get; set; }
}
