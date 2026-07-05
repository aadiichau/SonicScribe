namespace LocalScribe.Models;

public sealed class UpdateCheckResult
{
    public bool IsUpdateAvailable { get; init; }

    public string CurrentVersion { get; init; } = string.Empty;

    public string? LatestVersion { get; init; }

    public string? ReleaseNotes { get; init; }

    public string? DownloadUrl { get; init; }

    public string? InstallerDownloadUrl { get; init; }

    public string? PortableDownloadUrl { get; init; }

    public string? ReleasePageUrl { get; init; }

    public string? ErrorMessage { get; init; }

    public bool IsSuccessful => string.IsNullOrWhiteSpace(ErrorMessage);
}