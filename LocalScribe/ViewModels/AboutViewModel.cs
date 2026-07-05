using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalScribe.Core;
using LocalScribe.Models;
using LocalScribe.Services;

namespace LocalScribe.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    private readonly IUpdateCheckService _updateCheckService;
    private readonly IShellService _shellService;
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

    public AboutViewModel(IUpdateCheckService updateCheckService, IShellService shellService)
    {
        _updateCheckService = updateCheckService;
        _shellService = shellService;
    }

    public string AppName => AppBranding.AppName;

    public string Tagline => AppBranding.Tagline;

    public string VersionLabel => $"Version {AppVersion.Current}";

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
    private Task DownloadUpdateAsync()
    {
        var url = _lastUpdateResult?.DownloadUrl ?? AppBranding.ReleasesUrl;
        _shellService.OpenUrl(url);
        UpdateStatusMessage = "Opening the latest installer in your browser...";
        return Task.CompletedTask;
    }

    private bool CanDownloadUpdate() => IsUpdateAvailable && !IsCheckingForUpdates;

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
}