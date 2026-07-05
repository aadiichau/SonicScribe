using LocalScribe.Models;

namespace LocalScribe.Services;

public interface IUpdateCheckService
{
    string CurrentVersion { get; }

    Task<UpdateCheckResult> CheckForUpdatesAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);

    bool ShouldShowUpdatePrompt(UpdateCheckResult result, string? dismissedVersion);

    void MarkUpdatePromptDismissed(string version);
}