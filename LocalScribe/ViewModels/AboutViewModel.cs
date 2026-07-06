using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalScribe.Core;
using LocalScribe.Models;
using LocalScribe.Services;

namespace LocalScribe.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    private readonly IUpdateCheckService _updateCheckService;
    private readonly UpdatePromptService _updatePromptService;
    private UpdateCheckResult? _lastUpdateResult;

    [ObservableProperty]
    private string _pageTitle = "About";

    [ObservableProperty]
    private string _statusMessage = "Local speech-to-text powered by Whisper.";

    [ObservableProperty]
    private string _updateStatusMessage = "Checks GitHub for new SonicScribe releases.";

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private bool _isApplyingUpdate;

    public AboutViewModel(
        IUpdateCheckService updateCheckService,
        UpdatePromptService updatePromptService)
    {
        _updateCheckService = updateCheckService;
        _updatePromptService = updatePromptService;
    }

    public string AppName => AppBranding.AppName;

    public string Tagline => AppBranding.Tagline;

    public string VersionLabel => $"Version {AppVersion.Current}";

    public string GitHubProfileUrl => AppBranding.GitHubProfileUrl;

    public string GitHubProfileLabel => "github.com/aadiichau";

    public string Description =>
        $"{AppBranding.AppName} transcribes audio and video files on your PC using OpenAI Whisper "
        + "(via faster-whisper). Everything runs locally — your files never leave your machine.";

    public IReadOnlyList<string> Highlights { get; } =
    [
        "Unlimited local transcription with no cloud uploads or usage caps",
        "GPU acceleration when CUDA is available, with CPU fallback",
        "Queue multiple files, review transcripts in-app, and export to TXT, SRT, VTT, and JSON",
        "99+ language options with auto-detect",
        "History keeps past jobs searchable on your device",
    ];

    public IReadOnlyList<string> Requirements { get; } =
    [
        "Windows 10 or 11",
        "Python 3.12 with faster-whisper and PyTorch (CUDA optional)",
        "FFmpeg recommended for video and additional audio formats",
    ];

    public async Task InitializeAsync()
    {
        await RefreshUpdateStatusAsync(forceRefresh: false);
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        await RefreshUpdateStatusAsync(forceRefresh: true);
    }

    [RelayCommand(CanExecute = nameof(CanDownloadUpdate))]
    private async Task DownloadUpdateAsync()
    {
        if (_lastUpdateResult is null || !_lastUpdateResult.IsUpdateAvailable)
        {
            return;
        }

        IsApplyingUpdate = true;
        UpdateStatusMessage = "Downloading and installing update...";

        try
        {
            var applied = await _updatePromptService.ApplyUpdateAsync(_lastUpdateResult);
            if (!applied)
            {
                UpdateStatusMessage =
                    $"Update available: v{_lastUpdateResult.LatestVersion} (you have v{_lastUpdateResult.CurrentVersion}).";
            }
        }
        finally
        {
            IsApplyingUpdate = false;
            DownloadUpdateCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanDownloadUpdate() =>
        IsUpdateAvailable && !IsCheckingForUpdates && !IsApplyingUpdate;

    private async Task RefreshUpdateStatusAsync(bool forceRefresh)
    {
        IsCheckingForUpdates = true;

        try
        {
            var result = await _updateCheckService.CheckForUpdatesAsync(forceRefresh);
            _lastUpdateResult = result;
            IsUpdateAvailable = result.IsUpdateAvailable;

            if (!result.IsSuccessful)
            {
                UpdateStatusMessage = $"Could not check for updates: {result.ErrorMessage}";
                return;
            }

            UpdateStatusMessage = result.IsUpdateAvailable
                ? $"Update available: v{result.LatestVersion} (you have v{result.CurrentVersion})."
                : $"You are on the latest version (v{result.CurrentVersion}).";
        }
        finally
        {
            IsCheckingForUpdates = false;
            DownloadUpdateCommand.NotifyCanExecuteChanged();
        }
    }

    partial void OnIsUpdateAvailableChanged(bool value) =>
        DownloadUpdateCommand.NotifyCanExecuteChanged();

    partial void OnIsApplyingUpdateChanged(bool value) =>
        DownloadUpdateCommand.NotifyCanExecuteChanged();
}