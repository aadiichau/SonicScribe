using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalScribe.Core;
using LocalScribe.Helpers;
using LocalScribe.Models;
using LocalScribe.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LocalScribe.ViewModels;

public partial class TranscribeViewModel : ObservableObject
{
    private readonly IJobQueueService _jobQueueService;
    private readonly IFilePickerService _filePickerService;
    private readonly ISettingsService _settingsService;
    private readonly IExportService _exportService;
    private readonly IClipboardService _clipboardService;
    private readonly IShellService _shellService;
    private readonly IPrerequisiteSetupService _prerequisiteSetupService;
    private readonly IDeviceDetectionService _deviceDetectionService;
    private readonly IWhisperEngineHost _whisperEngineHost;
    private readonly Dictionary<string, QueueJobItemViewModel> _queueItemLookup = new();
    private string? _viewingJobId;

    [ObservableProperty]
    private string _pageTitle = "Transcribe";

    [ObservableProperty]
    private string _statusMessage = "Drop audio or video files to begin.";

    [ObservableProperty]
    private AppMessageSeverity _statusSeverity = AppMessageSeverity.Informational;

    [ObservableProperty]
    private bool _showStatusBar;

    [ObservableProperty]
    private bool _isQueueEmpty = true;

    [ObservableProperty]
    private int _queueCount;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _hasActiveJob;

    [ObservableProperty]
    private int _activeProgress;

    [ObservableProperty]
    private string _activeLogMessage = string.Empty;

    [ObservableProperty]
    private string _activeFileName = string.Empty;

    [ObservableProperty]
    private bool _isDragOver;

    [ObservableProperty]
    private bool _showTimestamps = true;

    [ObservableProperty]
    private bool _showProgressPanel;

    [ObservableProperty]
    private bool _showTranscriptPanel;

    [ObservableProperty]
    private bool _showEmptyPanel = true;

    [ObservableProperty]
    private string _viewingFileName = string.Empty;

    [ObservableProperty]
    private string _transcriptStatsLabel = string.Empty;

    [ObservableProperty]
    private string _selectedLanguage = "auto";

    [ObservableProperty]
    private QueueJobItemViewModel? _selectedQueueItem;

    [ObservableProperty]
    private bool _isTranscriptionReady;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallEverythingCommand))]
    private bool _isInstallingSetup;

    [ObservableProperty]
    private string _setupBannerMessage = "Install Python, faster-whisper, and PyTorch to start transcribing.";

    [ObservableProperty]
    private string _setupProgressMessage = string.Empty;

    [ObservableProperty]
    private bool _isModelLoading;

    public ObservableCollection<QueueJobItemViewModel> QueueItems { get; } = [];

    public ObservableCollection<TranscriptLineViewModel> TranscriptLines { get; } = [];

    public IReadOnlyList<LanguageOption> AvailableLanguages { get; } = WhisperLanguageCatalog.All;

    public TranscribeViewModel(
        IJobQueueService jobQueueService,
        IFilePickerService filePickerService,
        ISettingsService settingsService,
        IExportService exportService,
        IClipboardService clipboardService,
        IShellService shellService,
        IPrerequisiteSetupService prerequisiteSetupService,
        IDeviceDetectionService deviceDetectionService,
        IWhisperEngineHost whisperEngineHost)
    {
        _jobQueueService = jobQueueService;
        _filePickerService = filePickerService;
        _settingsService = settingsService;
        _exportService = exportService;
        _clipboardService = clipboardService;
        _shellService = shellService;
        _prerequisiteSetupService = prerequisiteSetupService;
        _deviceDetectionService = deviceDetectionService;
        _whisperEngineHost = whisperEngineHost;
        _jobQueueService.QueueChanged += OnQueueChanged;
        _jobQueueService.JobUpdated += OnJobUpdated;
        SelectedLanguage = _settingsService.Current.DefaultLanguage;
        RefreshQueueState();
    }

    public async Task InitializeAsync()
    {
        await CheckPrerequisitesAsync();
    }

    [RelayCommand]
    private async Task CheckPrerequisitesAsync()
    {
        try
        {
            var report = await _prerequisiteSetupService.CheckAsync();
            IsTranscriptionReady = report.IsTranscriptionReady;

            if (report.IsTranscriptionReady)
            {
                SetupBannerMessage = "All required components are installed.";
                return;
            }

            var missing = string.Join(
                ", ",
                report.MissingItems
                    .Where(item => item.Kind is not PrerequisiteKind.Winget)
                    .Select(item => item.Name));

            SetupBannerMessage = string.IsNullOrWhiteSpace(missing)
                ? "Tap Install everything to set up SonicScribe automatically."
                : $"Missing: {missing}. Tap Install everything — takes 10–30 minutes.";
        }
        catch (Exception ex)
        {
            IsTranscriptionReady = false;
            SetupBannerMessage = $"Could not check setup status: {ex.Message}";
        }
        finally
        {
            NotifySetupVisibility();
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstallEverything))]
    private async Task InstallEverythingAsync()
    {
        IsInstallingSetup = true;
        SetupProgressMessage = "Starting automatic setup...";
        SetStatus("Installing prerequisites. This can take 10–30 minutes — do not close the app.", AppMessageSeverity.Warning);

        try
        {
            var progress = new Progress<PrerequisiteSetupProgress>(update =>
            {
                SetupProgressMessage = $"{update.Step}: {update.Message}";
            });

            var report = await _prerequisiteSetupService.InstallMissingAsync(progress);
            IsTranscriptionReady = report.IsTranscriptionReady;
            _whisperEngineHost.InvalidateWorker();
            _deviceDetectionService.InvalidateCache();
            await _deviceDetectionService.DetectAsync(forceRefresh: true);

            SetupBannerMessage = report.IsTranscriptionReady
                ? "Setup complete! Drop files to start transcribing."
                : "Setup finished with warnings. Try Install everything again.";
            SetStatus(
                report.IsTranscriptionReady ? "Setup complete! You can start transcribing." : "Setup incomplete.",
                report.IsTranscriptionReady ? AppMessageSeverity.Success : AppMessageSeverity.Warning);
        }
        catch (Exception ex)
        {
            IsTranscriptionReady = false;
            SetupBannerMessage = $"Setup failed: {ex.Message}";
            SetStatus($"Setup failed: {ex.Message}", AppMessageSeverity.Error);
        }
        finally
        {
            IsInstallingSetup = false;
            SetupProgressMessage = string.Empty;
            NotifySetupVisibility();
        }
    }

    private bool CanInstallEverything() => !IsInstallingSetup;

    [RelayCommand]
    private async Task BrowseFilesAsync()
    {
        var paths = await _filePickerService.PickMediaFilesAsync();
        if (paths.Count > 0)
        {
            await EnqueueFilesInternalAsync(paths);
        }
    }

    public async Task EnqueueFilesAsync(IEnumerable<string> filePaths)
    {
        await EnqueueFilesInternalAsync(filePaths);
    }

    public void NotifyUnsupportedDrop()
    {
        SetStatus("Dropped files are not supported media formats.", AppMessageSeverity.Warning);
    }

    [RelayCommand]
    private void DismissStatus() => ShowStatusBar = false;

    public void SelectJob(string? jobId)
    {
        _viewingJobId = jobId;
        LoadTranscriptView();
        UpdateRightPanel();

        if (jobId is not null && _queueItemLookup.TryGetValue(jobId, out var item))
        {
            if (!ReferenceEquals(SelectedQueueItem, item))
            {
                SelectedQueueItem = item;
            }
        }
        else if (jobId is null)
        {
            SelectedQueueItem = null;
        }
    }

    public async Task StartProcessingAsync()
    {
        if (!IsTranscriptionReady)
        {
            SetStatus("Install everything first — tap the setup button on this page.", AppMessageSeverity.Warning);
            return;
        }

        if (!_jobQueueService.Queue.Any(job => job.Status == TranscriptionJobStatus.Queued))
        {
            SetStatus("No queued files to process.", AppMessageSeverity.Warning);
            RefreshQueueState();
            return;
        }

        await _jobQueueService.RemoveCompletedJobsAsync();
        ClearTranscriptView();
        await _jobQueueService.StartAsync();
        SetStatus("Processing queue...");
        RefreshQueueState();
    }

    public void RefreshDisplay() => RefreshQueueState();

    [RelayCommand(CanExecute = nameof(CanStartQueue))]
    private async Task StartQueueAsync() => await StartProcessingAsync();

    [RelayCommand(CanExecute = nameof(CanPauseQueue))]
    private async Task PauseQueueAsync()
    {
        await _jobQueueService.PauseAsync();
        SetStatus("Queue paused.", AppMessageSeverity.Warning);
        RefreshQueueState();
    }

    [RelayCommand(CanExecute = nameof(CanControlActiveJob))]
    private async Task CancelActiveAsync()
    {
        await _jobQueueService.CancelActiveAsync();
        SetStatus("Cancelled active job.", AppMessageSeverity.Warning);
        RefreshQueueState();
    }

    [RelayCommand(CanExecute = nameof(CanClearQueue))]
    private async Task ClearQueueAsync()
    {
        await _jobQueueService.ClearQueueAsync();
        SetStatus("Cleared queued files.");
        RefreshQueueState();
    }

    public async Task RemoveQueueItemAsync(QueueJobItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var removed = await _jobQueueService.RemoveJobAsync(item.JobId);
        if (!removed)
        {
            SetStatus("Cannot remove a file that is currently processing.", AppMessageSeverity.Warning);
            return;
        }

        if (string.Equals(_viewingJobId, item.JobId, StringComparison.OrdinalIgnoreCase))
        {
            ClearTranscriptView();
        }

        SetStatus($"Removed {item.DisplayName} from queue.");
        RefreshQueueState();
    }

    [RelayCommand(CanExecute = nameof(CanUseTranscript))]
    private async Task CopyTranscriptAsync()
    {
        var job = GetViewingJob();
        if (job is null || job.Segments.Count == 0)
        {
            return;
        }

        var text = TranscriptFormatter.BuildCopyText(job.Segments, ShowTimestamps);
        await _clipboardService.SetTextAsync(text);
        SetStatus("Copied transcript to clipboard.", AppMessageSeverity.Success);
    }

    [RelayCommand(CanExecute = nameof(CanUseTranscript))]
    private async Task ExportTxtAsync() => await ExportAsync(ExportFormat.Txt);

    [RelayCommand(CanExecute = nameof(CanUseTranscript))]
    private async Task DownloadTxtAsync()
    {
        var job = GetViewingJob();
        if (job is null || job.Segments.Count == 0)
        {
            return;
        }

        var suggestedName = string.IsNullOrWhiteSpace(job.DisplayName)
            ? Path.GetFileNameWithoutExtension(job.FileName)
            : job.DisplayName;

        var savePath = await _filePickerService.PickSaveFileAsync(suggestedName, ".txt");
        if (string.IsNullOrWhiteSpace(savePath))
        {
            return;
        }

        try
        {
            var content = TranscriptFormatter.BuildCopyText(job.Segments, includeTimestamps: false);
            await File.WriteAllTextAsync(savePath, content, Encoding.UTF8);
            SetStatus($"Saved transcript to {Path.GetFileName(savePath)}.", AppMessageSeverity.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Download failed: {ex.Message}", AppMessageSeverity.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseTranscript))]
    private async Task ExportSrtAsync() => await ExportAsync(ExportFormat.Srt);

    [RelayCommand(CanExecute = nameof(CanUseTranscript))]
    private async Task ExportVttAsync() => await ExportAsync(ExportFormat.Vtt);

    [RelayCommand(CanExecute = nameof(CanUseTranscript))]
    private async Task ExportJsonAsync() => await ExportAsync(ExportFormat.Json);

    [RelayCommand(CanExecute = nameof(CanUseTranscript))]
    private void OpenOutputFolder()
    {
        var folder = _settingsService.Current.OutputFolder;
        _shellService.OpenFolder(folder);
        SetStatus("Opened output folder.");
    }

    private async Task ExportAsync(ExportFormat format)
    {
        var job = GetViewingJob();
        if (job is null || job.Segments.Count == 0)
        {
            return;
        }

        try
        {
            var path = await _exportService.ExportAsync(job, format);
            _shellService.OpenFile(path);
            SetStatus($"Exported {format} to {Path.GetFileName(path)}.", AppMessageSeverity.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Export failed: {ex.Message}", AppMessageSeverity.Error);
        }
    }

    private async Task EnqueueFilesInternalAsync(IEnumerable<string> filePaths)
    {
        var allowed = MediaFileTypes.FilterAllowed(filePaths).ToList();
        if (allowed.Count == 0)
        {
            SetStatus("No supported audio or video files were found.", AppMessageSeverity.Warning);
            return;
        }

        await _jobQueueService.EnqueueAsync(allowed);
        SetStatus($"Added {allowed.Count} file(s) to the queue. Press Start to transcribe.", AppMessageSeverity.Success);
        RefreshQueueState();
    }

    private bool CanStartQueue() =>
        _jobQueueService.Queue.Any(job => job.Status == TranscriptionJobStatus.Queued)
        && !_jobQueueService.IsProcessing;

    private bool CanPauseQueue() => _jobQueueService.IsProcessing && !_jobQueueService.IsPaused;

    private bool CanControlActiveJob() => _jobQueueService.ActiveJob is not null;

    private bool CanClearQueue() =>
        _jobQueueService.Queue.Any(job => job.Status == TranscriptionJobStatus.Queued)
        && !_jobQueueService.IsProcessing;

    private bool CanUseTranscript() => GetViewingJob() is { Segments.Count: > 0 };

    private void OnQueueChanged(object? sender, EventArgs e) => RefreshQueueState();

    private void OnJobUpdated(object? sender, TranscriptionJob job)
    {
        UpdateQueueItem(job);
        UpdateActiveJobPanel(job);

        if (_jobQueueService.ActiveJob?.JobId == job.JobId
            && job.Status is TranscriptionJobStatus.LoadingModel
                or TranscriptionJobStatus.Transcribing
                or TranscriptionJobStatus.Exporting)
        {
            ClearTranscriptView();
        }

        if (job.Status == TranscriptionJobStatus.Done && job.Progress >= 100 && job.Segments.Count > 0)
        {
            SelectJob(job.JobId);
            SetStatus($"Finished {job.DisplayName}.", AppMessageSeverity.Success);
        }
        else if (job.Status == TranscriptionJobStatus.Error)
        {
            SetStatus(job.ErrorMessage ?? job.LogMessage, AppMessageSeverity.Error);
        }
        else if (job.Status == TranscriptionJobStatus.Cancelled)
        {
            SetStatus($"Cancelled {job.DisplayName}.", AppMessageSeverity.Warning);
        }

        UpdateRightPanel();
        RefreshQueueState();
    }

    private void RefreshQueueState()
    {
        SyncQueueItems();

        QueueCount = _jobQueueService.Queue.Count;
        OnPropertyChanged(nameof(QueueCountLabel));
        IsQueueEmpty = QueueCount == 0;
        IsProcessing = _jobQueueService.IsProcessing;
        IsPaused = _jobQueueService.IsPaused;

        var activeJob = _jobQueueService.ActiveJob;
        if (activeJob is not null)
        {
            UpdateActiveJobPanel(activeJob);
        }
        else if (!ShowTranscriptPanel)
        {
            HasActiveJob = false;
            ActiveProgress = 0;
            ActiveLogMessage = IsQueueEmpty
                ? "Waiting for files..."
                : IsProcessing ? "Preparing next job..." : "Queue idle.";
            ActiveFileName = string.Empty;
        }

        if (activeJob is null && !IsQueueEmpty && !IsProcessing && !ShowTranscriptPanel)
        {
            SetStatus("Queue ready. Add more files or press Start.");
        }

        UpdateRightPanel();
        OnPropertyChanged(nameof(ProcessingStatusLabel));
        NotifyCommandStates();
    }

    private void NotifyCommandStates()
    {
        StartQueueCommand.NotifyCanExecuteChanged();
        PauseQueueCommand.NotifyCanExecuteChanged();
        CancelActiveCommand.NotifyCanExecuteChanged();
        ClearQueueCommand.NotifyCanExecuteChanged();
        CopyTranscriptCommand.NotifyCanExecuteChanged();
        ExportTxtCommand.NotifyCanExecuteChanged();
        DownloadTxtCommand.NotifyCanExecuteChanged();
        ExportSrtCommand.NotifyCanExecuteChanged();
        ExportVttCommand.NotifyCanExecuteChanged();
        ExportJsonCommand.NotifyCanExecuteChanged();
        OpenOutputFolderCommand.NotifyCanExecuteChanged();
    }

    private void SyncQueueItems()
    {
        var activeId = _jobQueueService.ActiveJob?.JobId;
        var currentJobs = _jobQueueService.Queue;
        var currentIds = currentJobs.Select(job => job.JobId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var index = QueueItems.Count - 1; index >= 0; index--)
        {
            if (!currentIds.Contains(QueueItems[index].JobId))
            {
                if (_viewingJobId == QueueItems[index].JobId)
                {
                    _viewingJobId = null;
                    LoadTranscriptView();
                }

                _queueItemLookup.Remove(QueueItems[index].JobId);
                QueueItems.RemoveAt(index);
            }
        }

        foreach (var job in currentJobs)
        {
            if (_queueItemLookup.TryGetValue(job.JobId, out var item))
            {
                item.UpdateFrom(job, job.JobId == activeId);
                continue;
            }

            var newItem = new QueueJobItemViewModel();
            newItem.UpdateFrom(job, job.JobId == activeId);
            _queueItemLookup[job.JobId] = newItem;
            QueueItems.Add(newItem);
        }
    }

    private void UpdateQueueItem(TranscriptionJob job)
    {
        if (_queueItemLookup.TryGetValue(job.JobId, out var item))
        {
            item.UpdateFrom(job, _jobQueueService.ActiveJob?.JobId == job.JobId);
        }
    }

    private void UpdateActiveJobPanel(TranscriptionJob job)
    {
        var isActive = job.Status is TranscriptionJobStatus.LoadingModel
            or TranscriptionJobStatus.Transcribing
            or TranscriptionJobStatus.Exporting
            or TranscriptionJobStatus.Paused;

        if (!isActive)
        {
            HasActiveJob = false;
            IsModelLoading = false;
            return;
        }

        HasActiveJob = true;
        ActiveFileName = job.DisplayName;
        ActiveProgress = job.Progress;
        ActiveLogMessage = job.LogMessage;
        IsModelLoading = job.Status == TranscriptionJobStatus.LoadingModel;
    }

    private void ClearTranscriptView()
    {
        _viewingJobId = null;
        SelectedQueueItem = null;
        TranscriptLines.Clear();
        ViewingFileName = string.Empty;
        TranscriptStatsLabel = string.Empty;
        UpdateRightPanel();
        NotifyCommandStates();
    }

    private void LoadTranscriptView()
    {
        TranscriptLines.Clear();
        var job = GetViewingJob();

        if (job is null || job.Segments.Count == 0)
        {
            ViewingFileName = string.Empty;
            TranscriptStatsLabel = string.Empty;
            return;
        }

        ViewingFileName = job.DisplayName;
        TranscriptStatsLabel = BuildStatsLabel(job);

        foreach (var segment in job.Segments)
        {
            var line = new TranscriptLineViewModel(segment) { ShowTimestamps = ShowTimestamps };
            TranscriptLines.Add(line);
        }
    }

    private void UpdateRightPanel()
    {
        var activeJob = _jobQueueService.ActiveJob;
        var viewingJob = GetViewingJob();
        var activeIsProcessing = activeJob is not null
            && activeJob.Status is TranscriptionJobStatus.LoadingModel
                or TranscriptionJobStatus.Transcribing
                or TranscriptionJobStatus.Exporting
                or TranscriptionJobStatus.Paused;

        var showTranscript = viewingJob is { Segments.Count: > 0 }
            && viewingJob.Status == TranscriptionJobStatus.Done
            && (!activeIsProcessing || _viewingJobId != activeJob?.JobId);

        ShowTranscriptPanel = showTranscript;
        ShowProgressPanel = activeIsProcessing && !showTranscript;
        ShowEmptyPanel = !ShowTranscriptPanel && !ShowProgressPanel;

        OnPropertyChanged(nameof(ProgressPanelVisibility));
        OnPropertyChanged(nameof(TranscriptPanelVisibility));
        OnPropertyChanged(nameof(EmptyPanelVisibility));
    }

    private TranscriptionJob? GetViewingJob()
    {
        if (string.IsNullOrWhiteSpace(_viewingJobId))
        {
            return null;
        }

        return _jobQueueService.Queue.FirstOrDefault(job =>
            job.JobId.Equals(_viewingJobId, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildStatsLabel(TranscriptionJob job)
    {
        var parts = new List<string>();

        if (job.SegmentCount > 0)
        {
            parts.Add($"{job.SegmentCount} segments");
        }

        if (job.AudioDurationSeconds is > 0)
        {
            parts.Add($"{TimeFormatHelper.FormatDuration(job.AudioDurationSeconds.Value)} duration");
        }

        if (job.ElapsedMinutes is > 0)
        {
            parts.Add($"{job.ElapsedMinutes:F1} min elapsed");
        }

        if (!string.IsNullOrWhiteSpace(job.DetectedLanguage))
        {
            parts.Add($"Language: {job.DetectedLanguage}");
        }

        if (!string.IsNullOrWhiteSpace(job.Device))
        {
            parts.Add($"Device: {job.Device}");
        }

        return parts.Count > 0 ? string.Join(" · ", parts) : string.Empty;
    }

    partial void OnActiveProgressChanged(int value)
    {
        OnPropertyChanged(nameof(ActiveProgressLabel));
    }

    partial void OnShowTimestampsChanged(bool value)
    {
        foreach (var line in TranscriptLines)
        {
            line.ShowTimestamps = value;
        }
    }

    public string ModelLabel => $"Model: {_settingsService.Current.DefaultModel}";

    public string QueueCountLabel => $"{QueueCount} files";

    public string LanguageLabel => $"Language: {SelectedLanguage}";

    public string ProcessingStatusLabel
    {
        get
        {
            if (!_jobQueueService.IsProcessing)
            {
                return "Status: Idle";
            }

            var activeJob = _jobQueueService.ActiveJob;
            if (activeJob?.Status == TranscriptionJobStatus.Exporting)
            {
                return "Status: Saving files";
            }

            return IsPaused ? "Status: Paused" : "Status: Processing";
        }
    }

    public string ActiveProgressLabel => IsModelLoading ? "Loading model..." : $"{ActiveProgress}%";

    public bool IsProgressIndeterminate => IsModelLoading;

    partial void OnIsModelLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ActiveProgressLabel));
        OnPropertyChanged(nameof(IsProgressIndeterminate));
    }

    public Visibility ProgressPanelVisibility => ShowProgressPanel ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TranscriptPanelVisibility => ShowTranscriptPanel ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyPanelVisibility => ShowEmptyPanel ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SetupBannerVisibility =>
        !IsTranscriptionReady ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SetupButtonVisibility =>
        !IsTranscriptionReady && !IsInstallingSetup ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SetupProgressVisibility =>
        IsInstallingSetup ? Visibility.Visible : Visibility.Collapsed;

    private void NotifySetupVisibility()
    {
        OnPropertyChanged(nameof(SetupBannerVisibility));
        OnPropertyChanged(nameof(SetupButtonVisibility));
        OnPropertyChanged(nameof(SetupProgressVisibility));
    }

    partial void OnIsTranscriptionReadyChanged(bool value) => NotifySetupVisibility();

    partial void OnIsInstallingSetupChanged(bool value) => NotifySetupVisibility();

    public InfoBarSeverity StatusInfoBarSeverity => StatusSeverity switch
    {
        AppMessageSeverity.Success => InfoBarSeverity.Success,
        AppMessageSeverity.Warning => InfoBarSeverity.Warning,
        AppMessageSeverity.Error => InfoBarSeverity.Error,
        _ => InfoBarSeverity.Informational
    };

    private void SetStatus(string message, AppMessageSeverity severity = AppMessageSeverity.Informational)
    {
        StatusMessage = message;
        StatusSeverity = severity;
        ShowStatusBar = severity != AppMessageSeverity.Informational;
        OnPropertyChanged(nameof(StatusInfoBarSeverity));
    }

    public void RefreshFromQueue() => RefreshQueueState();

    partial void OnSelectedLanguageChanged(string value)
    {
        _settingsService.Current.DefaultLanguage = string.IsNullOrWhiteSpace(value) ? "auto" : value;
        _jobQueueService.SyncQueuedJobsFromSettings();
        OnPropertyChanged(nameof(LanguageLabel));
        RefreshQueueState();
        _ = _settingsService.SaveAsync();
    }

    partial void OnSelectedQueueItemChanged(QueueJobItemViewModel? value)
    {
        if (value is null)
        {
            if (_viewingJobId is not null)
            {
                SelectJob(null);
            }

            return;
        }

        if (!string.Equals(_viewingJobId, value.JobId, StringComparison.OrdinalIgnoreCase))
        {
            _viewingJobId = value.JobId;
            LoadTranscriptView();
            UpdateRightPanel();
            NotifyCommandStates();
        }
    }

    partial void OnShowTranscriptPanelChanged(bool value) => OnPropertyChanged(nameof(ProcessingStatusLabel));

    partial void OnIsProcessingChanged(bool value) => OnPropertyChanged(nameof(ProcessingStatusLabel));
}