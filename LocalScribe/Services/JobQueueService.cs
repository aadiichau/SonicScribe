using LocalScribe.Helpers;
using LocalScribe.Models;
using Microsoft.Extensions.Logging;

namespace LocalScribe.Services;

public sealed class JobQueueService : IJobQueueService
{
    private readonly ISettingsService _settingsService;
    private readonly ITranscriptionService _transcriptionService;
    private readonly IHistoryService _historyService;
    private readonly IWhisperEngineHost _whisperEngineHost;
    private readonly ILogger<JobQueueService> _logger;
    private readonly List<TranscriptionJob> _queue = [];
    private readonly object _sync = new();

    private CancellationTokenSource? _processingCts;
    private Task? _processingTask;
    private bool _isPaused;

    public JobQueueService(
        ISettingsService settingsService,
        ITranscriptionService transcriptionService,
        IHistoryService historyService,
        IWhisperEngineHost whisperEngineHost,
        ILogger<JobQueueService> logger)
    {
        _settingsService = settingsService;
        _transcriptionService = transcriptionService;
        _historyService = historyService;
        _whisperEngineHost = whisperEngineHost;
        _logger = logger;
    }

    public IReadOnlyList<TranscriptionJob> Queue
    {
        get
        {
            lock (_sync)
            {
                return _queue.ToList();
            }
        }
    }

    public TranscriptionJob? ActiveJob { get; private set; }

    public bool IsProcessing => _processingTask is { IsCompleted: false };

    public bool IsPaused => _isPaused;

    public event EventHandler? QueueChanged;

    public event EventHandler<TranscriptionJob>? JobUpdated;

    public Task EnqueueAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            foreach (var filePath in filePaths)
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Skipped missing file: {FilePath}", filePath);
                    continue;
                }

                var fileName = Path.GetFileName(filePath);
                var job = new TranscriptionJob
                {
                    JobId = Guid.NewGuid().ToString("N")[..8],
                    FileName = fileName,
                    DisplayName = Path.GetFileNameWithoutExtension(fileName),
                    FilePath = filePath,
                    Model = _settingsService.Current.DefaultModel,
                    Language = NormalizeLanguage(_settingsService.Current.DefaultLanguage),
                    Status = TranscriptionJobStatus.Queued,
                    LogMessage = "Queued...",
                    CreatedAt = DateTimeOffset.Now
                };

                _queue.Add(job);
                _logger.LogInformation("Queued job {JobId} for file {FileName}", job.JobId, job.FileName);
            }
        }

        RaiseQueueChanged();
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_processingTask is { IsCompleted: true })
        {
            _processingTask = null;
        }

        if (IsProcessing)
        {
            _logger.LogDebug("Queue processing is already running.");
            return Task.CompletedTask;
        }

        ApplyCurrentSettingsToQueuedJobs();

        _isPaused = false;
        _processingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _processingTask = ProcessQueueAsync(_processingCts.Token);
        _logger.LogInformation("Started queue processing.");
        RaiseQueueChanged();
        return Task.CompletedTask;
    }

    public Task PauseAsync(CancellationToken cancellationToken = default)
    {
        _isPaused = true;
        _processingCts?.Cancel();
        _logger.LogInformation("Pause requested for active queue processing.");
        return Task.CompletedTask;
    }

    public Task CancelActiveAsync(CancellationToken cancellationToken = default)
    {
        _processingCts?.Cancel();

        if (ActiveJob is not null)
        {
            ActiveJob.Status = TranscriptionJobStatus.Cancelled;
            ActiveJob.LogMessage = "Cancelled by user.";
            ActiveJob.CompletedAt = DateTimeOffset.Now;
            NotifyJobUpdated(ActiveJob);
            _logger.LogInformation("Cancelled active job {JobId}", ActiveJob.JobId);
        }

        RaiseQueueChanged();
        return Task.CompletedTask;
    }

    public Task ClearQueueAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _queue.RemoveAll(job => job.Status == TranscriptionJobStatus.Queued);
        }

        RaiseQueueChanged();
        _logger.LogInformation("Cleared queued jobs.");
        return Task.CompletedTask;
    }

    public Task<bool> ResetJobForRetryAsync(string jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (ActiveJob?.JobId.Equals(jobId, StringComparison.OrdinalIgnoreCase) == true)
            {
                return Task.FromResult(false);
            }

            var job = _queue.FirstOrDefault(item =>
                item.JobId.Equals(jobId, StringComparison.OrdinalIgnoreCase));

            if (job is null || job.Status != TranscriptionJobStatus.Error)
            {
                return Task.FromResult(false);
            }

            job.Status = TranscriptionJobStatus.Queued;
            job.Progress = 0;
            job.LogMessage = "Queued...";
            job.ErrorMessage = null;
            _logger.LogInformation("Reset job {JobId} to queued for retry", jobId);
        }

        RaiseQueueChanged();
        return Task.FromResult(true);
    }

    public Task<bool> RemoveJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (ActiveJob?.JobId.Equals(jobId, StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.LogWarning("Cannot remove active job {JobId}", jobId);
                return Task.FromResult(false);
            }

            var job = _queue.FirstOrDefault(item =>
                item.JobId.Equals(jobId, StringComparison.OrdinalIgnoreCase));

            if (job is null)
            {
                return Task.FromResult(false);
            }

            if (job.Status is TranscriptionJobStatus.LoadingModel
                or TranscriptionJobStatus.Transcribing
                or TranscriptionJobStatus.Exporting
                or TranscriptionJobStatus.Paused)
            {
                _logger.LogWarning("Cannot remove in-progress job {JobId}", jobId);
                return Task.FromResult(false);
            }

            _queue.Remove(job);
            _logger.LogInformation("Removed job {JobId} from queue", jobId);
        }

        RaiseQueueChanged();
        return Task.FromResult(true);
    }

    public Task RemoveCompletedJobsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            var removed = _queue.RemoveAll(job =>
                job.IsTerminal
                && !job.JobId.Equals(ActiveJob?.JobId, StringComparison.OrdinalIgnoreCase));

            if (removed > 0)
            {
                _logger.LogInformation("Removed {Count} completed jobs from queue", removed);
            }
        }

        RaiseQueueChanged();
        return Task.CompletedTask;
    }

    public void SyncQueuedJobsFromSettings()
    {
        ApplyCurrentSettingsToQueuedJobs();
        RaiseQueueChanged();
    }

    private void ApplyCurrentSettingsToQueuedJobs()
    {
        lock (_sync)
        {
            foreach (var job in _queue.Where(job => job.Status == TranscriptionJobStatus.Queued))
            {
                ApplyCurrentSettingsToJob(job);
            }
        }
    }

    private void ApplyCurrentSettingsToJob(TranscriptionJob job)
    {
        var settings = _settingsService.Current;
        job.Model = settings.DefaultModel;
        job.Language = NormalizeLanguage(settings.DefaultLanguage);
        _logger.LogDebug(
            "Applied settings to job {JobId}: model={Model}, language={Language}",
            job.JobId,
            job.Model,
            job.Language);
    }

    private static string NormalizeLanguage(string? language) =>
        string.IsNullOrWhiteSpace(language) || string.Equals(language, "auto", StringComparison.OrdinalIgnoreCase)
            ? "auto"
            : language.Trim();

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && !_isPaused)
            {
                var nextJob = GetNextQueuedJob();
                if (nextJob is null)
                {
                    break;
                }

                ApplyCurrentSettingsToJob(nextJob);

                var outputFolder = AppDataPathHelper.EnsureOutputFolderReady(_settingsService.Current.OutputFolder);
                if (!string.Equals(outputFolder, _settingsService.Current.OutputFolder, StringComparison.OrdinalIgnoreCase))
                {
                    _settingsService.Current.OutputFolder = outputFolder;
                    await _settingsService.SaveAsync(cancellationToken);
                }

                nextJob.Status = TranscriptionJobStatus.LoadingModel;
                nextJob.Progress = 0;
                nextJob.LogMessage = "Starting...";
                ActiveJob = nextJob;
                NotifyJobUpdated(nextJob);
                _logger.LogInformation("Processing job {JobId}", nextJob.JobId);

                var progress = new Progress<TranscriptionProgress>(update => ApplyProgress(nextJob, update));
                var modelRepairRetried = false;

                try
                {
                    while (true)
                    {
                        WhisperModelCacheHelper.EnsureHealthyModelCacheOrClear(nextJob.Model, _logger);

                        var result = await _transcriptionService.TranscribeAsync(nextJob, progress, cancellationToken);
                        ApplyJobSnapshot(nextJob, result);

                        if (result.Status == TranscriptionJobStatus.Error
                            && WhisperModelCacheHelper.IsCorruptModelError(result.ErrorMessage)
                            && !modelRepairRetried
                            && WhisperModelCacheHelper.TryRepairCorruptModel(
                                result.ErrorMessage,
                                nextJob.Model,
                                out var repairSummary))
                        {
                            modelRepairRetried = true;
                            _whisperEngineHost.InvalidateWorker();
                            nextJob.Status = TranscriptionJobStatus.LoadingModel;
                            nextJob.Progress = 0;
                            nextJob.ErrorMessage = null;
                            nextJob.LogMessage = repairSummary;
                            NotifyJobUpdated(nextJob);
                            _logger.LogWarning(
                                "Auto-repairing corrupt model cache for job {JobId}: {Summary}",
                                nextJob.JobId,
                                repairSummary);
                            continue;
                        }

                        nextJob.CompletedAt = DateTimeOffset.Now;
                        nextJob.TranscribedAt = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm");

                        if (result.Status == TranscriptionJobStatus.Done)
                        {
                            nextJob.Progress = 100;
                            await _historyService.AddOrUpdateAsync(nextJob, cancellationToken);
                            _logger.LogInformation("Completed job {JobId}", nextJob.JobId);
                        }
                        else if (result.Status == TranscriptionJobStatus.Cancelled)
                        {
                            _logger.LogInformation("Job {JobId} was cancelled.", nextJob.JobId);
                        }
                        else
                        {
                            _logger.LogWarning("Job {JobId} finished with status {Status}", nextJob.JobId, result.Status);
                        }

                        if (result.IsTerminal)
                        {
                            NotifyJobUpdated(nextJob);
                        }

                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    if (nextJob.Status != TranscriptionJobStatus.Cancelled)
                    {
                        nextJob.Status = _isPaused
                            ? TranscriptionJobStatus.Paused
                            : TranscriptionJobStatus.Cancelled;
                        nextJob.LogMessage = _isPaused ? "Paused." : "Cancelled.";
                        nextJob.CompletedAt = DateTimeOffset.Now;
                    }

                    NotifyJobUpdated(nextJob);
                    _logger.LogInformation("Job {JobId} processing was interrupted.", nextJob.JobId);
                    break;
                }
                catch (Exception ex)
                {
                    nextJob.Status = TranscriptionJobStatus.Error;
                    nextJob.ErrorMessage = ex.Message;
                    nextJob.LogMessage = $"Error: {ex.Message}";
                    nextJob.CompletedAt = DateTimeOffset.Now;
                    NotifyJobUpdated(nextJob);
                    _logger.LogError(ex, "Unhandled error while processing job {JobId}", nextJob.JobId);
                }

                ActiveJob = null;
                RaiseQueueChanged();
            }
        }
        finally
        {
            ActiveJob = null;
            RaiseQueueChanged();
            _logger.LogInformation("Queue processing stopped.");
        }
    }

    private TranscriptionJob? GetNextQueuedJob()
    {
        lock (_sync)
        {
            return _queue.FirstOrDefault(job => job.Status == TranscriptionJobStatus.Queued);
        }
    }

    private void ApplyProgress(TranscriptionJob job, TranscriptionProgress progress)
    {
        job.Status = progress.Status;
        job.Progress = progress.Progress;
        job.DownloadPercent = progress.DownloadPercent ?? job.DownloadPercent;
        job.DownloadBytes = progress.DownloadBytes ?? job.DownloadBytes;
        job.DownloadTotal = progress.DownloadTotal ?? job.DownloadTotal;
        job.LogMessage = progress.LogMessage;
        job.DetectedLanguage = progress.DetectedLanguage ?? job.DetectedLanguage;
        job.AudioDurationSeconds = progress.AudioDurationSeconds ?? job.AudioDurationSeconds;

        if (progress.Segments is not null)
        {
            job.Segments = progress.Segments;
            job.SegmentCount = progress.Segments.Count;
        }

        NotifyJobUpdated(job);
    }

    private static void ApplyJobSnapshot(TranscriptionJob target, TranscriptionJob source)
    {
        target.Status = source.Status;
        target.Progress = source.Progress;
        target.LogMessage = source.LogMessage;
        target.DetectedLanguage = source.DetectedLanguage;
        target.AudioDurationSeconds = source.AudioDurationSeconds;
        target.SegmentCount = source.SegmentCount;
        target.ElapsedSeconds = source.ElapsedSeconds;
        target.ElapsedMinutes = source.ElapsedMinutes;
        target.RealtimeFactor = source.RealtimeFactor;
        target.Device = source.Device;
        target.GpuName = source.GpuName;
        target.Segments = source.Segments;
        target.ErrorMessage = source.ErrorMessage;
        target.TxtFile = source.TxtFile;
        target.SrtFile = source.SrtFile;
        target.JsonFile = source.JsonFile;
        target.TimestampedTxtFile = source.TimestampedTxtFile;
    }

    private void NotifyJobUpdated(TranscriptionJob job)
    {
        UiDispatcher.Invoke(() =>
        {
            JobUpdated?.Invoke(this, job);
            QueueChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void RaiseQueueChanged()
    {
        UiDispatcher.Invoke(() => QueueChanged?.Invoke(this, EventArgs.Empty));
    }
}