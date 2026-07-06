using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalScribe.Helpers;
using LocalScribe.Models;
using LocalScribe.Services;
using Microsoft.UI.Xaml;

namespace LocalScribe.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IDeviceDetectionService _deviceDetectionService;
    private readonly IFilePickerService _filePickerService;
    private readonly IShellService _shellService;
    private readonly IWhisperEngineHost _whisperEngineHost;
    private readonly IPrerequisiteSetupService _prerequisiteSetupService;
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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallEverythingCommand))]
    private bool _isCheckingPrerequisites;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallEverythingCommand))]
    private bool _isInstallingPrerequisites;

    [ObservableProperty]
    private string _setupStatusMessage = "One-click setup installs Python, faster-whisper, PyTorch, and FFmpeg.";

    [ObservableProperty]
    private string _setupProgressMessage = string.Empty;

    [ObservableProperty]
    private bool _showSetupProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallButtonVisibility))]
    private bool _isTranscriptionReady;

    [ObservableProperty]
    private int _setupProgressPercent;

    public ObservableCollection<PrerequisiteLineViewModel> PrerequisiteLines { get; } = [];

    public IReadOnlyList<WhisperModelOption> AvailableModels => WhisperModelCatalog.All;

    public IReadOnlyList<LanguageOption> AvailableLanguages { get; } = WhisperLanguageCatalog.All;

    public SettingsViewModel(
        ISettingsService settingsService,
        IDeviceDetectionService deviceDetectionService,
        IFilePickerService filePickerService,
        IShellService shellService,
        IWhisperEngineHost whisperEngineHost,
        IPrerequisiteSetupService prerequisiteSetupService)
    {
        _settingsService = settingsService;
        _deviceDetectionService = deviceDetectionService;
        _filePickerService = filePickerService;
        _shellService = shellService;
        _whisperEngineHost = whisperEngineHost;
        _prerequisiteSetupService = prerequisiteSetupService;
    }

    public async Task InitializeAsync()
    {
        await _settingsService.LoadAsync();
        ApplySettingsToView();
        await RefreshDeviceInfoAsync(forceRefresh: false);
        await CheckPrerequisitesAsync();
    }

    [RelayCommand]
    private async Task CheckPrerequisitesAsync()
    {
        IsCheckingPrerequisites = true;
        SetupStatusMessage = "Checking prerequisites...";

        try
        {
            var report = await _prerequisiteSetupService.CheckAsync();
            ApplyPrerequisiteReport(report);
            SetupStatusMessage = report.IsTranscriptionReady
                ? "All required components are installed. You are ready to transcribe."
                : "Some components are missing. Use Install everything to set up automatically.";
        }
        catch (Exception ex)
        {
            SetupStatusMessage = $"Prerequisite check failed: {ex.Message}";
        }
        finally
        {
            IsCheckingPrerequisites = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstallPrerequisites))]
    private async Task InstallEverythingAsync()
    {
        IsInstallingPrerequisites = true;
        ShowSetupProgress = true;
        SetupProgressMessage = "Starting automatic setup...";
        SetupStatusMessage = "Installing prerequisites. This can take 10–30 minutes on first run.";

        try
        {
            var progress = new Progress<PrerequisiteSetupProgress>(update =>
            {
                SetupProgressMessage = $"{update.Step}: {update.Message}";
                SetupProgressPercent = update.Percent ?? SetupProgressPercent;
                ShowSetupProgress = update.IsIndeterminate || update.Percent is not null;
            });

            var report = await _prerequisiteSetupService.InstallMissingAsync(progress);
            ApplyPrerequisiteReport(report);
            _whisperEngineHost.InvalidateWorker();
            await RefreshDeviceInfoAsync(forceRefresh: true);
            SetupStatusMessage = report.IsTranscriptionReady
                ? "Setup complete! You can start transcribing."
                : "Setup finished with warnings. Review the checklist below.";
        }
        catch (Exception ex)
        {
            SetupStatusMessage = $"Automatic setup failed: {ex.Message}";
        }
        finally
        {
            IsInstallingPrerequisites = false;
            ShowSetupProgress = false;
            InstallEverythingCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanInstallPrerequisites() =>
        !IsTranscriptionReady && !IsInstallingPrerequisites && !IsCheckingPrerequisites;

    public Visibility InstallButtonVisibility =>
        IsTranscriptionReady ? Visibility.Collapsed : Visibility.Visible;

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

            _settingsService.Current.OutputFolder =
                AppDataPathHelper.EnsureOutputFolderReady(_settingsService.Current.OutputFolder);
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

        OutputFolder = AppDataPathHelper.EnsureOutputFolderReady(OutputFolder);
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
            var path = await PythonLocator.LocateSupportedAsync(
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
        OutputFolder = AppDataPathHelper.GetDefaultOutputFolder();
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

    private void ApplyPrerequisiteReport(PrerequisiteReport report)
    {
        IsTranscriptionReady = report.IsTranscriptionReady;
        OnPropertyChanged(nameof(InstallButtonVisibility));
        InstallEverythingCommand.NotifyCanExecuteChanged();

        if (PrerequisiteLines.Count == 0)
        {
            foreach (var item in report.Items)
            {
                PrerequisiteLines.Add(new PrerequisiteLineViewModel(item));
            }

            return;
        }

        for (var i = 0; i < report.Items.Count && i < PrerequisiteLines.Count; i++)
        {
            PrerequisiteLines[i].Apply(report.Items[i]);
        }
    }
}