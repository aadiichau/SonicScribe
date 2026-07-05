using LocalScribe.Models;

namespace LocalScribe.Services;

public interface IPrerequisiteSetupService
{
    Task<PrerequisiteReport> CheckAsync(CancellationToken cancellationToken = default);

    Task<PrerequisiteReport> InstallMissingAsync(
        IProgress<PrerequisiteSetupProgress>? progress = null,
        CancellationToken cancellationToken = default);
}