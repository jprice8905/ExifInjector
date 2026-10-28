namespace ExifInjector.App.Services;

/// <summary>Defaults for a run, bound from the "Injection" section of appsettings.json.</summary>
public sealed class InjectionOptions
{
    public const string SectionName = "Injection";

    /// <summary>Spacing applied between consecutive files so no two photos share a timestamp.</summary>
    public TimeSpan IncrementPerFile { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Whether a folder selection also walks subfolders.</summary>
    public bool IncludeSubfolders { get; set; }
}
