using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalScribe.Helpers;
using LocalScribe.Models;
using LocalScribe.Services;
using Microsoft.UI.Xaml;

namespace LocalScribe.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly IHistoryService _historyService;
    private readonly ISettingsService _settingsService;
    private readonly IExportService _exportService;
    private readonly IClipboardService _clipboardService;
    private readonly IShellService _shellService;
    private readonly Dictionary<string, HistoryItemViewModel> _itemLookup = new();
    private string? _selectedJobId;
    private TranscriptionJob? _selectedJob;

    [ObservableProperty]
    private string _pageTitle = "History";

    [ObservableProperty]
    private string _statusMessage = "Select a transcription to view.";

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private int _itemCount;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _showTimestamps = true;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private bool _isLoadingDetail;

    [ObservableProperty]
    private string _viewingFileName = string.Empty;

    [ObservableProperty]
    private string _transcriptStatsLabel = string.Empty;

    public ObservableCollection<HistoryItemViewModel> HistoryItems { get; } = [];

    public ObservableCollection<TranscriptLineViewModel> TranscriptLines { get; } = [];

    public HistoryViewModel(
        IHistoryService historyService,
        ISettingsService settingsService,
        IExportService exportService,
        IClipboardService clipboardService,
        IShellService shellService)
    {
        _historyService = historyService;
        _settingsService = settingsService;
        _exportService = exportService;
        _clipboardService = clipboardService;
        _shellService = shellService;
    }

    public async Task InitializeAsync()
    {
        await _historyService.LoadAsync();
        RebuildHistoryList();
    }

    public void SelectJob(string? jobId)
    {
        _selectedJobId = jobId;
        _ = LoadSelectedJobAsync();
    }

    public async Task RenameSelectedAsync(string newName)
    {
        if (string.IsNullOrWhiteSpace(_selectedJobId) || string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        await _historyService.RenameAsync(_selectedJobId, newName.Trim());
        await _historyService.LoadAsync();
        RebuildHistoryList();
        await LoadSelectedJobAsync();
        StatusMessage = "Renamed transcription.";
    }

    public async Task DeleteJobAsync(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return;
        }

        await _historyService.DeleteAsync(jobId);

        if (_selectedJobId == jobId)
        {
            _selectedJobId = null;
            _selectedJob = null;
            ClearTranscriptView();
        }

        await _historyService.LoadAsync();
        RebuildHistoryList();
        StatusMessage = "Deleted transcription from history.";
    }

    public async Task ClearHistoryAsync()
    {
        await _historyService.ClearAllAsync();
        _selectedJobId = null;
        _selectedJob = null;
        ClearTranscriptView();
        await _historyService.LoadAsync();
        RebuildHistoryList();
        StatusMessage = "History cleared.";
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await _historyService.LoadAsync();
        RebuildHistoryList();

        if (!string.IsNullOrWhiteSpace(_selectedJobId))
        {
            await LoadSelectedJobAsync();
        }

        StatusMessage = "History refreshed.";
    }

    [RelayCommand(CanExecute = nameof(CanUseTranscript))]
    private async Task CopyTranscriptAsync()
    {
        if (_selectedJob is null || _selectedJob.Segments.Count == 0)
        {
            return;
        }

        var text = TranscriptFormatter.BuildCopyText(_selectedJob.Segments, ShowTimestamps);
        await _clipboardService.SetTextAsync(text);
        StatusMessage = "Copied transcript to clipboard.";
    }

    [RelayCommand(CanExecute = nameof(CanUseTranscript))]
    private async Task ExportTxtAsync() => await ExportOrOpenAsync(ExportFormat.Txt);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ExportSrtAsync() => await ExportOrOpenAsync(ExportFormat.Srt);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ExportVttAsync() => await ExportOrOpenAsync(ExportFormat.Vtt);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ExportJsonAsync() => await ExportOrOpenAsync(ExportFormat.Json);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenOutputFolder()
    {
        _shellService.OpenFolder(_settingsService.Current.OutputFolder);
        StatusMessage = "Opened output folder.";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenTxtFile() => OpenExistingExport(job => OutputPathResolver.ResolveTxtPath(job, _settingsService.Current.OutputFolder));

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenSrtFile() => OpenExistingExport(job => OutputPathResolver.ResolveSrtPath(job, _settingsService.Current.OutputFolder));

    private void OpenExistingExport(Func<TranscriptionJob, string?> resolvePath)
    {
        if (_selectedJob is null)
        {
            return;
        }

        var path = resolvePath(_selectedJob);
        if (path is null)
        {
            StatusMessage = "Export file not found. Try re-exporting.";
            return;
        }

        _shellService.OpenFile(path);
        StatusMessage = $"Opened {Path.GetFileName(path)}.";
    }

    private async Task ExportOrOpenAsync(ExportFormat format)
    {
        if (_selectedJob is null)
        {
            return;
        }

        if (_selectedJob.Segments.Count == 0)
        {
            var segments = await HistorySegmentLoader.LoadSegmentsAsync(
                _selectedJob,
                _settingsService.Current.OutputFolder);
            if (segments.Count > 0)
            {
                _selectedJob.Segments = segments;
                _selectedJob.SegmentCount = segments.Count;
                PopulateTranscriptLines();
                NotifyCommandStates();
            }
        }

        var existingPath = format switch
        {
            ExportFormat.Txt => OutputPathResolver.ResolveTxtPath(_selectedJob, _settingsService.Current.OutputFolder),
            ExportFormat.Srt => OutputPathResolver.ResolveSrtPath(_selectedJob, _settingsService.Current.OutputFolder),
            ExportFormat.Vtt => OutputPathResolver.ResolveVttPath(_selectedJob, _settingsService.Current.OutputFolder),
            ExportFormat.Json => OutputPathResolver.ResolveJsonPath(_selectedJob, _settingsService.Current.OutputFolder),
            _ => null
        };

        if (_selectedJob.Segments.Count == 0 && existingPath is not null)
        {
            _shellService.OpenFile(existingPath);
            StatusMessage = $"Opened {Path.GetFileName(existingPath)}.";
            return;
        }

        if (_selectedJob.Segments.Count == 0)
        {
            StatusMessage = "No transcript data available to export.";
            return;
        }

        try
        {
            var path = await _exportService.ExportAsync(_selectedJob, format);
            _shellService.OpenFile(path);
            StatusMessage = $"Exported {format} to {Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    private async Task LoadSelectedJobAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedJobId))
        {
            _selectedJob = null;
            ClearTranscriptView();
            return;
        }

        IsLoadingDetail = true;
        HasSelection = true;

        try
        {
            _selectedJob = await _historyService.GetByIdAsync(_selectedJobId);
            if (_selectedJob is null)
            {
                ClearTranscriptView();
                StatusMessage = "Transcription not found.";
                return;
            }

            if (_selectedJob.Segments.Count == 0)
            {
                var segments = await HistorySegmentLoader.LoadSegmentsAsync(
                    _selectedJob,
                    _settingsService.Current.OutputFolder);
                if (segments.Count > 0)
                {
                    _selectedJob.Segments = segments;
                    _selectedJob.SegmentCount = segments.Count;
                }
            }

            ViewingFileName = _selectedJob.DisplayName;
            TranscriptStatsLabel = BuildStatsLabel(_selectedJob);
            PopulateTranscriptLines();
            StatusMessage = TranscriptLines.Count > 0
                ? "Showing transcript."
                : "No transcript segments found. Export files may still be available.";
        }
        finally
        {
            IsLoadingDetail = false;
            NotifyCommandStates();
        }
    }

    private void PopulateTranscriptLines()
    {
        TranscriptLines.Clear();

        if (_selectedJob is null)
        {
            return;
        }

        foreach (var segment in _selectedJob.Segments)
        {
            TranscriptLines.Add(new TranscriptLineViewModel(segment) { ShowTimestamps = ShowTimestamps });
        }
    }

    private void ClearTranscriptView()
    {
        HasSelection = false;
        IsLoadingDetail = false;
        ViewingFileName = string.Empty;
        TranscriptStatsLabel = string.Empty;
        TranscriptLines.Clear();
        StatusMessage = "Select a transcription to view.";
        NotifyCommandStates();
    }

    private void RebuildHistoryList()
    {
        HistoryItems.Clear();
        _itemLookup.Clear();

        var query = SearchQuery.Trim();
        var jobs = _historyService.Items
            .Where(job => job.Status == TranscriptionJobStatus.Done)
            .Where(job => string.IsNullOrWhiteSpace(query)
                || job.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || job.FileName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var job in jobs)
        {
            var item = new HistoryItemViewModel();
            item.UpdateFrom(job);
            _itemLookup[job.JobId] = item;
            HistoryItems.Add(item);
        }

        ItemCount = _historyService.Items.Count(job => job.Status == TranscriptionJobStatus.Done);
        IsEmpty = HistoryItems.Count == 0;
        OnPropertyChanged(nameof(CanClearHistory));
    }

    public bool CanClearHistory => ItemCount > 0;

    private bool CanUseTranscript() => _selectedJob is { Segments.Count: > 0 };

    private void NotifyCommandStates()
    {
        CopyTranscriptCommand.NotifyCanExecuteChanged();
        ExportTxtCommand.NotifyCanExecuteChanged();
        ExportSrtCommand.NotifyCanExecuteChanged();
        ExportVttCommand.NotifyCanExecuteChanged();
        ExportJsonCommand.NotifyCanExecuteChanged();
        OpenOutputFolderCommand.NotifyCanExecuteChanged();
        OpenTxtFileCommand.NotifyCanExecuteChanged();
        OpenSrtFileCommand.NotifyCanExecuteChanged();
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
            parts.Add($"{TimeFormatHelper.FormatDuration(job.AudioDurationSeconds.Value)} audio");
        }

        if (job.ElapsedMinutes is > 0)
        {
            parts.Add($"{job.ElapsedMinutes:F1} min elapsed");
        }

        if (!string.IsNullOrWhiteSpace(job.DetectedLanguage))
        {
            parts.Add($"Language: {job.DetectedLanguage}");
        }

        if (job.RealtimeFactor is > 0)
        {
            parts.Add($"{job.RealtimeFactor:F1}x realtime");
        }

        if (!string.IsNullOrWhiteSpace(job.Model))
        {
            parts.Add($"Model: {job.Model}");
        }

        return parts.Count > 0 ? string.Join(" · ", parts) : string.Empty;
    }

    partial void OnSearchQueryChanged(string value) => RebuildHistoryList();

    partial void OnShowTimestampsChanged(bool value)
    {
        foreach (var line in TranscriptLines)
        {
            line.ShowTimestamps = value;
        }
    }

    partial void OnHasSelectionChanged(bool value) => NotifyDetailVisibility();

    partial void OnIsLoadingDetailChanged(bool value) => NotifyDetailVisibility();

    private void NotifyDetailVisibility()
    {
        OnPropertyChanged(nameof(TranscriptVisibility));
        OnPropertyChanged(nameof(LoadingVisibility));
        OnPropertyChanged(nameof(EmptyDetailVisibility));
    }

    public Visibility TranscriptVisibility => HasSelection && !IsLoadingDetail
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility LoadingVisibility => IsLoadingDetail ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyDetailVisibility => !HasSelection && !IsLoadingDetail
        ? Visibility.Visible
        : Visibility.Collapsed;
}