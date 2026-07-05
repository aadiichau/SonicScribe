using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using LocalScribe.Core;

namespace LocalScribe.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    [ObservableProperty]
    private string _pageTitle = "About";

    [ObservableProperty]
    private string _statusMessage = "Local speech-to-text powered by Whisper.";

    public string AppName => AppBranding.AppName;

    public string Tagline => AppBranding.Tagline;

    public string VersionLabel => $"Version {GetAppVersion()}";

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

    private static string GetAppVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "1.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}