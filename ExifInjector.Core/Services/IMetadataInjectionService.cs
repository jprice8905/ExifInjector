using ExifInjector.Core.Models;

namespace ExifInjector.Core.Services;

/// <summary>Applies a <see cref="MetadataInjectionRequest"/> to a set of files.</summary>
public interface IMetadataInjectionService
{
    Task<InjectionSummary> InjectAsync(
        IReadOnlyList<string> filePaths,
        MetadataInjectionRequest request,
        IProgress<FileInjectionResult>? progress = null,
        CancellationToken cancellationToken = default);
}
