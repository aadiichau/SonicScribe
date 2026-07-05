using LocalScribe.Models;

namespace LocalScribe.Services;

public interface IAppUpdateService
{
    bool IsInstalledCopy { get; }

    Task<AppUpdateResult> DownloadAndApplyAsync(
        UpdateCheckResult update,
        IProgress<AppUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default);
}