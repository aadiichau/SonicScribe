using LocalScribe.Models;
using Microsoft.Extensions.Logging;

namespace LocalScribe.Services;

public sealed class TranscriptionService : ITranscriptionService
{
    private readonly IWhisperEngineHost _whisperEngineHost;
    private readonly IExportService _exportService;
    private readonly ILogger<TranscriptionService> _logger;

    public TranscriptionService(
        IWhisperEngineHost whisperEngineHost,
        IExportService exportService,
        ILogger<TranscriptionService> logger)
    {
        _whisperEngineHost = whisperEngineHost;
        _exportService = exportService;
        _logger = logger;
    }

    public async Task<TranscriptionJob> TranscribeAsync(
        TranscriptionJob job,
        IProgress<TranscriptionProgress> progress,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(job.FilePath))
        {
            job.Status = TranscriptionJobStatus.Error;
            job.ErrorMessage = $"File not found: {job.FilePath}";
            job.LogMessage = job.ErrorMessage;
            progress.Report(new TranscriptionProgress
            {
                Status = job.Status,
                Progress = 0,
                LogMessage = job.LogMessage
            });
            return job;
        }

        _logger.LogInformation(
            "Starting faster-whisper transcription for job {JobId}, file {FileName}, model {Model}",
            job.JobId,
            job.FileName,
            job.Model);

        try
        {
            await _whisperEngineHost.RunTranscriptionAsync(job, progress, cancellationToken);

            if (job.Status != TranscriptionJobStatus.Done)
            {
                return job;
            }

            var completionLog = job.LogMessage;

            progress.Report(new TranscriptionProgress
            {
                Status = TranscriptionJobStatus.Exporting,
                Progress = 98,
                LogMessage = "Saving export files...",
                DetectedLanguage = job.DetectedLanguage,
                AudioDurationSeconds = job.AudioDurationSeconds,
                Segments = job.Segments
            });

            await ExportOutputsAsync(job, cancellationToken);
            job.Status = TranscriptionJobStatus.Done;
            job.Progress = 100;
            job.LogMessage = completionLog;
            job.CompletedAt = DateTimeOffset.Now;
            job.TranscribedAt = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm");

            progress.Report(new TranscriptionProgress
            {
                Status = job.Status,
                Progress = job.Progress,
                LogMessage = job.LogMessage,
                DetectedLanguage = job.DetectedLanguage,
                AudioDurationSeconds = job.AudioDurationSeconds,
                Segments = job.Segments
            });

            _logger.LogInformation(
                "Completed transcription for job {JobId} with {SegmentCount} segments",
                job.JobId,
                job.SegmentCount);

            return job;
        }
        catch (OperationCanceledException)
        {
            job.Status = TranscriptionJobStatus.Cancelled;
            job.LogMessage = "Cancelled.";
            job.CompletedAt = DateTimeOffset.Now;
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcription failed for job {JobId}", job.JobId);
            job.Status = TranscriptionJobStatus.Error;
            job.ErrorMessage = ex.Message;
            job.LogMessage = $"Error: {ex.Message}";
            job.CompletedAt = DateTimeOffset.Now;

            progress.Report(new TranscriptionProgress
            {
                Status = job.Status,
                Progress = 0,
                LogMessage = job.LogMessage
            });

            return job;
        }
    }

    private async Task ExportOutputsAsync(TranscriptionJob job, CancellationToken cancellationToken)
    {
        var txtPath = await _exportService.ExportAsync(job, ExportFormat.Txt, cancellationToken: cancellationToken);
        var tsPath = await _exportService.ExportAsync(job, ExportFormat.TimestampedTxt, cancellationToken: cancellationToken);
        var srtPath = await _exportService.ExportAsync(job, ExportFormat.Srt, cancellationToken: cancellationToken);
        var vttPath = await _exportService.ExportAsync(job, ExportFormat.Vtt, cancellationToken: cancellationToken);
        var jsonPath = await _exportService.ExportAsync(job, ExportFormat.Json, cancellationToken: cancellationToken);

        job.TxtFile = Path.GetFileName(txtPath);
        job.SrtFile = Path.GetFileName(srtPath);
        job.JsonFile = Path.GetFileName(jsonPath);
        job.TimestampedTxtFile = Path.GetFileName(tsPath);

        _logger.LogInformation(
            "Exported outputs for job {JobId}: TXT, SRT, VTT, JSON, timestamped TXT",
            job.JobId);
    }
}