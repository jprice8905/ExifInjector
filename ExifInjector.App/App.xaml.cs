using ExifInjector.App.Services;
using ExifInjector.App.ViewModels;
using ExifInjector.App.Views;
using ExifInjector.Core.Exif;
using ExifInjector.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Serilog;

namespace ExifInjector.App;

public partial class App : Application
{
    private IServiceProvider? _services;
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var baseDirectory = AppContext.BaseDirectory;
        Directory.SetCurrentDirectory(baseDirectory);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(baseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        _services = BuildServiceProvider(configuration);

        Log.Information("ExifInjector started. ExifToolPath={ExifToolPath}",
            configuration[$"{ExifToolOptions.SectionName}:ExecutablePath"]);

        _window = _services.GetRequiredService<MainWindow>();
        _services.GetRequiredService<WindowContext>().WindowHandle =
            WinRT.Interop.WindowNative.GetWindowHandle(_window);
        _window.Activate();
    }

    private static ServiceProvider BuildServiceProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        services.AddSingleton(configuration);
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });

        services.Configure<ExifToolOptions>(configuration.GetSection(ExifToolOptions.SectionName));
        services.Configure<InjectionOptions>(configuration.GetSection(InjectionOptions.SectionName));

        services.AddSingleton<IExifTool, ExifToolWrapper>();
        services.AddSingleton<IMetadataInjectionService, MetadataInjectionService>();
        services.AddSingleton<WindowContext>();
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled UI exception: {Message}", e.Message);
        Log.CloseAndFlush();
    }
}
