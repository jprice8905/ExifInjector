namespace ExifInjector.Core.Exif;

/// <summary>Raw result of a single ExifTool invocation.</summary>
/// <param name="ExitCode">Process exit code. 0 means success.</param>
/// <param name="StandardOutput">Captured stdout.</param>
/// <param name="StandardError">Captured stderr.</param>
public sealed record ExifToolResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;

    public string ErrorMessage =>
        string.IsNullOrWhiteSpace(StandardError) ? StandardOutput.Trim() : StandardError.Trim();
}
