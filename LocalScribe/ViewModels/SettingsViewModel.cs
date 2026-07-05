using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalScribe.Helpers;
using LocalScribe.Models;
using LocalScribe.Services;

namespace LocalScribe.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IDeviceDetectionService _deviceDetectionService;
    private readonly IFilePickerService _filePickerService;
    private readonly IShellService _shellService;
    private readonly IWhisperEngineHost _whisperEngineHost;
    private string? _savedPythonPath;

    [ObservableProperty]
    private string _pageTitle = "Settings";

    [ObservableProperty]
    private string _statusMessage = "Configure defaults for new transcription jobs.";

    [ObservableProperty]
    private string _selectedModel = "large-v3";

    [ObservableProperty]
    private string _selectedLanguage = "zh";

    [ObservableProperty]
    private string _outputFolder = string.Empty;

    [ObservableProperty]
    private string _pythonPath = string.Empty;

    [ObservableProperty]
    private string _gpuName = "Detecting...";

    [ObservableProperty]
    private string _deviceTypeLabel = "Device: —";

    [ObservableProperty]
    private string _cudaStatusLabel = "CUDA: —";

    [ObservableProperty]
    private string _pythonStatusLabel = "Python: —";

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private bool _isDetectingDevice;

    public IReadOnlyList<ModelOption> AvailableModels { get; } =
    [
        new("large-v3", "large-v3 — Best quality"),
        new("medium", "medium — Faster"),
        new("small", "small — Fastest"),
        new("base", "base — Ultra fast")
    ];

    public IReadOnlyList<LanguageOption> AvailableLanguages { get; } = WhisperLanguageCatalog.All;

    public SettingsViewModel(
        ISettingsService settingsService,
        IDeviceDetectionService deviceDetectionService,
        IFilePickerService filePickerService,
        IShellService shellService,
        IWhisperEngineHost whisperEngineHost)
    {
        _settingsService = settingsService;
        _deviceDetectionService = deviceDetectionService;
        _filePickerService = filePickerService;
        _shellService = shellService;
        _whisperEngineHost = whisperEngineHost;
    }

    public async Task InitializeAsync()
    {
        await _settingsService.LoadAsync();
        ApplySettingsToView();
        await RefreshDeviceInfoAsync(forceRefresh: false);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsSaving = true;

        try
        {
            _settingsService.Current.DefaultModel = SelectedModel;
            _settingsService.Current.DefaultLanguage = SelectedLanguage;
            _settingsService.Current.OutputFolder = OutputFolder;
            _settingsService.Current.PythonExecutablePath = string.IsNullOrWhiteSpace(PythonPath)
                ? null
                : PythonPath.Trim();

            Directory.CreateDirectory(_settingsService.Current.OutputFolder);
            await _settingsService.SaveAsync();

            var pythonChanged = !string.Equals(
                _savedPythonPath,
                _settingsService.Current.PythonExecutablePath,
                StringComparison.OrdinalIgnoreCase);

            if (pythonChanged)
            {
                _whisperEngineHost.InvalidateWorker();
                _deviceDetectionService.InvalidateCache();
                await RefreshDeviceInfoAsync(forceRefresh: true);
            }

            _savedPythonPath = _settingsService.Current.PythonExecutablePath;
            StatusMessage = "Settings saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save settings: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task BrowseOutputFolderAsync()
    {
        var folder = await _filePickerService.PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            OutputFolder = folder;
            StatusMessage = "Output folder updated. Press Save to persist.";
        }
    }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            return;
        }

        Directory.CreateDirectory(OutputFolder);
        _shellService.OpenFolder(OutputFolder);
    }

    [RelayCommand]
    private async Task BrowsePythonAsync()
    {
        var path = await _filePickerService.PickPythonExecutableAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            PythonPath = path;
            StatusMessage = "Python path updated. Save and re-detect device to apply.";
        }
    }

    [RelayCommand]
    private async Task AutoDetectPythonAsync()
    {
        IsDetectingDevice = true;
        StatusMessage = "Searching for Python with PyTorch...";

        try
        {
            var path = await PythonLocator.LocateAsync(
                string.IsNullOrWhiteSpace(PythonPath) ? null : PythonPath);

            if (path is null)
            {
                StatusMessage = "No usable Python installation was found.";
                return;
            }

            PythonPath = path;
            _settingsService.Current.PythonExecutablePath = path;
            await _settingsService.SaveAsync();
            _savedPythonPath = path;
            _whisperEngineHost.InvalidateWorker();
            StatusMessage = $"Found Python at {path}. Re-detecting device...";
            await RefreshDeviceInfoAsync(forceRefresh: true);
        }
        finally
        {
            IsDetectingDevice = false;
        }
    }

    [RelayCommand]
    private async Task RedetectDeviceAsync()
    {
        await RefreshDeviceInfoAsync(forceRefresh: true);
        StatusMessage = "Device detection refreshed.";
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        SelectedModel = "large-v3";
        SelectedLanguage = "zh";
        OutputFolder = new Models.AppSettings().OutputFolder;
        StatusMessage = "Restored default model, language, and output folder. Press Save to persist.";
    }

    private void ApplySettingsToView()
    {
        SelectedModel = _settingsService.Current.DefaultModel;
        SelectedLanguage = _settingsService.Current.DefaultLanguage;
        OutputFolder = _settingsService.Current.OutputFolder;
        PythonPath = _settingsService.Current.PythonExecutablePath ?? string.Empty;
        _savedPythonPath = _settingsService.Current.PythonExecutablePath;
    }

    private async Task RefreshDeviceInfoAsync(bool forceRefresh)
    {
        IsDetectingDevice = true;

        try
        {
            if (forceRefresh)
            {
                _deviceDetectionService.InvalidateCache();
            }

            var device = await _deviceDetectionService.DetectAsync(forceRefresh: forceRefresh);
            ApplyDeviceInfo(device);

            if (!string.IsNullOrWhiteSpace(device.PythonPath))
            {
                PythonPath = device.PythonPath;
            }
        }
        finally
        {
            IsDetectingDevice = false;
        }
    }

    private void ApplyDeviceInfo(DeviceInfo device)
    {
        GpuName = device.DisplayName;
        DeviceTypeLabel = $"Device: {device.DeviceType}";
        CudaStatusLabel = device.IsCudaAvailable ? "CUDA: Available" : "CUDA: Not available";
        PythonStatusLabel = device.IsPythonAvailable && !string.IsNullOrWhiteSpace(device.PythonPath)
            ? $"Python: {device.PythonPath}"
            : "Python: Not found";

        if (!string.IsNullOrWhiteSpace(device.ProbeError))
        {
            StatusMessage = $"Device note: {device.ProbeError}";
        }
    }
}

public sealed record ModelOption(string Code, string DisplayName);