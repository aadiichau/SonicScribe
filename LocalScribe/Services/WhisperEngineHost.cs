using LocalScribe.Helpers;
using LocalScribe.Models;
using Microsoft.Extensions.Logging;

namespace LocalScribe.Services;

public sealed class WhisperEngineHost : IWhisperEngineHost, IAsyncDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly IDeviceDetectionService _deviceDetectionService;
    private readonly ILogger<WhisperEngineHost> _logger;
    private readonly SemaphoreSlim _workerLock = new(1, 1);

    private StreamingProcessHost? _worker;
    private string? _pythonPath;
    private string? _workerScriptPath;
    private Func<string, Task>? _lineHandler;

    public WhisperEngineHost(
        ISettingsService settingsService,
        IDeviceDetectionService deviceDetectionService,
        ILogger<WhisperEngineHost> logger)
    {
        _settingsService = settingsService;
        _deviceDetectionService = deviceDetectionService;
        _logger = logger;
    }

    public async Task RunTranscriptionAsync(
        TranscriptionJob job,
        IProgress<TranscriptionProgress> progress,
        CancellationToken cancellationToken = default)
    {
        await _workerLock.WaitAsync(cancellationToken);

        try
        {
            await EnsureRuntimeAsync(cancellationToken);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var segments = new List<TranscriptSegment>();

            linkedCts.Token.Register(() =>
            {
                _logger.LogWarning("Cancellation requested for job {JobId}. Terminating worker.", job.JobId);
                KillWorker();
                completionSource.TrySetCanceled(linkedCts.Token);
            });

            _lineHandler = line =>
            {
                var workerEvent = WorkerEvent.Parse(line);
                if (workerEvent is null)
                {
                    return Task.CompletedTask;
                }

                switch (workerEvent.Type)
                {
                    case "done":
                        ApplyWorkerEvent(job, workerEvent, segments, progress);
                        if (workerEvent.Segments is not null)
                        {
                            segments.Clear();
                            segments.AddRange(workerEvent.Segments);
                            job.Segments = segments;
                            job.SegmentCount = segments.Count;
                        }

                        completionSource.TrySetResult(true);
                        break;

                    case "error":
                        ApplyWorkerEvent(job, workerEvent, segments, progress);
                        job.Status = workerEvent.Status.Equals("cancelled", StringComparison.OrdinalIgnoreCase)
                            ? TranscriptionJobStatus.Cancelled
                            : TranscriptionJobStatus.Error;
                        job.ErrorMessage = workerEvent.Error;
                        completionSource.TrySetException(
                            new InvalidOperationException(workerEvent.Error ?? workerEvent.Log));
                        break;

                    case "progress":
                    case "status":
                    case "pong":
                        ApplyWorkerEvent(job, workerEvent, segments, progress);
                        break;
                }

                return Task.CompletedTask;
            };

            await EnsureWorkerRunningAsync(linkedCts.Token);

            await _worker!.SendCommandAsync(
                new
                {
                    cmd = "transcribe",
                    job_id = job.JobId,
                    file = job.FilePath,
                    model = job.Model,
                    language = job.Language
                },
                linkedCts.Token);

            await completionSource.Task;
        }
        finally
        {
            _lineHandler = null;
            _workerLock.Release();
        }
    }

    public void InvalidateWorker()
    {
        _logger.LogInformation("Invalidating faster-whisper worker process.");

        if (_worker is not null)
        {
            var worker = _worker;
            _worker = null;
            _ = GracefulShutdownAsync(worker);
        }

        _pythonPath = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_worker is not null)
        {
            await _worker.DisposeAsync();
            _worker = null;
        }

        _workerLock.Dispose();
    }

    private async Task EnsureRuntimeAsync(CancellationToken cancellationToken)
    {
        _workerScriptPath = WorkerScriptLocator.Locate();
        if (_workerScriptPath is null)
        {
            throw new FileNotFoundException("Could not locate Engine/transcribe_worker.py.");
        }

        var device = await _deviceDetectionService.DetectAsync(cancellationToken: cancellationToken);
        _pythonPath = device.PythonPath ?? _settingsService.Current.PythonExecutablePath;

        if (string.IsNullOrWhiteSpace(_pythonPath))
        {
            _pythonPath = await PythonLocator.LocateAsync(
                _settingsService.Current.PythonExecutablePath,
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(_pythonPath))
        {
            throw new InvalidOperationException(
                "Python was not found. Install Python 3.12 with faster-whisper and CUDA PyTorch.");
        }

        _settingsService.Current.PythonExecutablePath = _pythonPath;
        await _settingsService.SaveAsync(cancellationToken);
    }

    private async Task EnsureWorkerRunningAsync(CancellationToken cancellationToken)
    {
        if (_worker is not null && !_worker.HasExited)
        {
            return;
        }

        if (_worker is not null)
        {
            await _worker.DisposeAsync();
            _worker = null;
        }

        _logger.LogInformation(
            "Starting faster-whisper worker using {PythonPath} and {ScriptPath}",
            _pythonPath,
            _workerScriptPath);

        _worker = await StreamingProcessHost.StartAsync(
            _pythonPath!,
            $"\"{_workerScriptPath}\"",
            line => _lineHandler?.Invoke(line) ?? Task.CompletedTask,
            message => _logger.LogDebug("Worker stderr: {Message}", message),
            cancellationToken);
    }

    private void KillWorker()
    {
        if (_worker is null)
        {
            return;
        }

        var worker = _worker;
        _worker = null;
        worker.Kill();
    }

    private async Task GracefulShutdownAsync(StreamingProcessHost worker)
    {
        try
        {
            await worker.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Graceful worker shutdown failed. Forcing termination.");
            worker.Kill();
        }
    }

    private static void ApplyWorkerEvent(
        TranscriptionJob job,
        WorkerEvent workerEvent,
        List<TranscriptSegment> segments,
        IProgress<TranscriptionProgress> progress)
    {
        var status = MapStatus(workerEvent.Status);
        job.Status = status;
        job.Progress = workerEvent.Progress;
        job.LogMessage = workerEvent.Log;
        job.DetectedLanguage = workerEvent.DetectedLanguage ?? job.DetectedLanguage;
        job.AudioDurationSeconds = workerEvent.Duration ?? job.AudioDurationSeconds;
        job.ElapsedSeconds = workerEvent.Elapsed ?? job.ElapsedSeconds;
        job.ElapsedMinutes = workerEvent.ElapsedMin ?? job.ElapsedMinutes;
        job.Device = workerEvent.Device ?? job.Device;
        job.GpuName = workerEvent.GpuName ?? job.GpuName;
        job.SegmentCount = workerEvent.SegmentCount > 0 ? workerEvent.SegmentCount : segments.Count;

        if (workerEvent.Duration is > 0 && workerEvent.Elapsed is > 0)
        {
            job.RealtimeFactor = workerEvent.Duration / workerEvent.Elapsed;
        }

        if (workerEvent.Segments is not null)
        {
            segments.Clear();
            segments.AddRange(workerEvent.Segments);
            job.Segments = segments;
            job.SegmentCount = segments.Count;
        }

        progress.Report(new TranscriptionProgress
        {
            Status = status,
            Progress = job.Progress,
            LogMessage = job.LogMessage,
            DetectedLanguage = job.DetectedLanguage,
            AudioDurationSeconds = job.AudioDurationSeconds,
            Segments = job.Segments,
            DownloadPercent = workerEvent.DownloadPercent,
            DownloadBytes = workerEvent.DownloadBytes,
            DownloadTotal = workerEvent.DownloadTotal
        });
    }

    private static TranscriptionJobStatus MapStatus(string status)
        => status.ToLowerInvariant() switch
        {
            "queued" => TranscriptionJobStatus.Queued,
            "loading_model" => TranscriptionJobStatus.LoadingModel,
            "transcribing" => TranscriptionJobStatus.Transcribing,
            "paused" => TranscriptionJobStatus.Paused,
            "done" => TranscriptionJobStatus.Done,
            "error" => TranscriptionJobStatus.Error,
            "cancelled" => TranscriptionJobStatus.Cancelled,
            _ => TranscriptionJobStatus.Transcribing
        };
}