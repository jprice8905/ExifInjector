using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExifInjector.Core.Exif;

/// <summary>
/// Thin wrapper around the ExifTool executable. All process handling, argument quoting and
/// output parsing lives here so the rest of the application stays free of ExifTool details.
/// </summary>
public sealed class ExifToolWrapper : IExifTool
{
    private readonly ExifToolOptions _options;
    private readonly ILogger<ExifToolWrapper> _logger;

    public ExifToolWrapper(IOptions<ExifToolOptions> options, ILogger<ExifToolWrapper> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["-ver"], cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new ExifToolException($"ExifTool returned exit code {result.ExitCode}: {result.ErrorMessage}");
        }

        return result.StandardOutput.Trim();
    }

    public async Task<IReadOnlyDictionary<string, string>> ReadTagsAsync(
        string filePath,
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var arguments = new List<string> { "-charset", "filename=UTF8", "-json", "-short", "-struct" };
        arguments.AddRange(tags.Select(tag => "-" + tag));
        arguments.Add(filePath);

        var result = await RunAsync(arguments, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new ExifToolException(
                $"ExifTool failed to read tags from '{filePath}' (exit code {result.ExitCode}): {result.ErrorMessage}");
        }

        return ParseTagJson(result.StandardOutput);
    }

    public Task<ExifToolResult> WriteTagsAsync(
        string filePath,
        IEnumerable<ExifTagAssignment> assignments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var assignmentList = assignments as IReadOnlyList<ExifTagAssignment> ?? assignments.ToList();
        if (assignmentList.Count == 0)
        {
            return Task.FromResult(new ExifToolResult(0, string.Empty, string.Empty));
        }

        var arguments = new List<string> { "-charset", "filename=UTF8", "-charset", "iptc=UTF8" };
        if (_options.OverwriteOriginal)
        {
            arguments.Add("-overwrite_original");
        }

        arguments.AddRange(assignmentList.Select(a => $"-{a.Tag}={a.Value}"));
        arguments.Add(filePath);

        return RunAsync(arguments, cancellationToken);
    }

    private static IReadOnlyDictionary<string, string> ParseTagJson(string json)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return tags;
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
        {
            return tags;
        }

        foreach (var property in document.RootElement[0].EnumerateObject())
        {
            tags[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                JsonValueKind.Null => string.Empty,
                JsonValueKind.Array => string.Join(
                    ", ",
                    property.Value.EnumerateArray().Select(element => element.ToString())),
                _ => property.Value.ToString()
            };
        }

        return tags;
    }

    private async Task<ExifToolResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new ExifToolException(
                $"Unable to start ExifTool using '{_options.ExecutablePath}'. Check the ExifTool:ExecutablePath setting in appsettings.json.",
                ex);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_options.Timeout > TimeSpan.Zero)
        {
            timeoutSource.CancelAfter(_options.Timeout);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new ExifToolException($"ExifTool timed out after {_options.Timeout}.");
        }

        var standardOutput = await stdoutTask.ConfigureAwait(false);
        var standardError = await stderrTask.ConfigureAwait(false);

        _logger.LogDebug(
            "ExifTool exited with {ExitCode} for arguments {Arguments}",
            process.ExitCode,
            string.Join(' ', arguments));

        return new ExifToolResult(process.ExitCode, standardOutput, standardError);
    }

    private void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to terminate the ExifTool process");
        }
    }
}
