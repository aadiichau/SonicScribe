using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LocalScribe.Core;
using LocalScribe.Models;
using Microsoft.Extensions.Logging;

namespace LocalScribe.Services;

public sealed class UpdateCheckService : IUpdateCheckService, IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    private readonly ISettingsService _settingsService;
    private readonly ILogger<UpdateCheckService> _logger;
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private UpdateCheckResult? _cachedResult;
    private DateTimeOffset _cachedAt;

    public UpdateCheckService(ISettingsService settingsService, ILogger<UpdateCheckService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SonicScribe-UpdateChecker");
    }

    public string CurrentVersion => AppVersion.Current;

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        await _settingsService.LoadAsync(cancellationToken);

        if (!forceRefresh
            && _cachedResult is not null
            && DateTimeOffset.UtcNow - _cachedAt < CheckInterval)
        {
            return _cachedResult;
        }

        var lastCheck = _settingsService.Current.LastUpdateCheckUtc;
        if (!forceRefresh
            && lastCheck is not null
            && DateTimeOffset.UtcNow - lastCheck.Value < CheckInterval
            && _cachedResult is not null)
        {
            return _cachedResult;
        }

        try
        {
            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(
                $"https://api.github.com/repos/{AppBranding.GitHubRepo}/releases/latest",
                cancellationToken);

            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return CacheResult(new UpdateCheckResult
                {
                    CurrentVersion = CurrentVersion,
                    ErrorMessage = "Could not read the latest release information."
                });
            }

            var latestVersion = NormalizeVersionTag(release.TagName);
            var currentVersion = CurrentVersion;
            var isNewer = AppVersion.IsNewer(latestVersion, currentVersion);
            var installerAsset = release.Assets?
                .FirstOrDefault(asset => asset.Name?.StartsWith("SonicScribe-Setup-v", StringComparison.OrdinalIgnoreCase) == true
                    && asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

            var result = new UpdateCheckResult
            {
                IsUpdateAvailable = isNewer,
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion,
                ReleaseNotes = TrimReleaseNotes(release.Body),
                DownloadUrl = installerAsset?.BrowserDownloadUrl
                    ?? $"https://github.com/{AppBranding.GitHubRepo}/releases/latest/download/SonicScribe-Setup-v{latestVersion}.exe",
                ReleasePageUrl = release.HtmlUrl
                    ?? $"https://github.com/{AppBranding.GitHubRepo}/releases/latest"
            };

            _settingsService.Current.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
            await _settingsService.SaveAsync(cancellationToken);

            _logger.LogInformation(
                "Update check: current={CurrentVersion}, latest={LatestVersion}, updateAvailable={UpdateAvailable}",
                currentVersion,
                latestVersion,
                isNewer);

            return CacheResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed.");
            return CacheResult(new UpdateCheckResult
            {
                CurrentVersion = CurrentVersion,
                ErrorMessage = ex.Message
            });
        }
    }

    public bool ShouldShowUpdatePrompt(UpdateCheckResult result, string? dismissedVersion)
    {
        if (!result.IsSuccessful || !result.IsUpdateAvailable || result.LatestVersion is null)
        {
            return false;
        }

        return !string.Equals(dismissedVersion, result.LatestVersion, StringComparison.OrdinalIgnoreCase);
    }

    public void MarkUpdatePromptDismissed(string version)
    {
        _settingsService.Current.DismissedUpdateVersion = version;
        _ = _settingsService.SaveAsync();
    }

    public void Dispose() => _httpClient.Dispose();

    private UpdateCheckResult CacheResult(UpdateCheckResult result)
    {
        _cachedResult = result;
        _cachedAt = DateTimeOffset.UtcNow;
        return result;
    }

    private static string NormalizeVersionTag(string tagName) =>
        tagName.Trim().TrimStart('v', 'V');

    private static string? TrimReleaseNotes(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        const int maxLength = 500;
        var trimmed = body.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength] + "...";
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAsset>? Assets { get; set; }
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}