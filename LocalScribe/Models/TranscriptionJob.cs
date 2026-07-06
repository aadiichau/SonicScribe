namespace LocalScribe.Models;

public sealed class TranscriptionJob
{
    public string JobId { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public TranscriptionJobStatus Status { get; set; } = TranscriptionJobStatus.Queued;

    public int Progress { get; set; }

    public int? DownloadPercent { get; set; }

    public long? DownloadBytes { get; set; }

    public long? DownloadTotal { get; set; }

    public string LogMessage { get; set; } = string.Empty;

    public string Model { get; set; } = "large-v3";

    public string Language { get; set; } = "auto";

    public string? DetectedLanguage { get; set; }

    public double? AudioDurationSeconds { get; set; }

    public int SegmentCount { get; set; }

    public double? ElapsedSeconds { get; set; }

    public double? ElapsedMinutes { get; set; }

    public double? RealtimeFactor { get; set; }

    public string Device { get; set; } = "?";

    public string GpuName { get; set; } = string.Empty;

    public IReadOnlyList<TranscriptSegment> Segments { get; set; } = Array.Empty<TranscriptSegment>();

    public string? ErrorMessage { get; set; }

    public string? TxtFile { get; set; }

    public string? SrtFile { get; set; }

    public string? JsonFile { get; set; }

    public string? TimestampedTxtFile { get; set; }

    public string? TranscribedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset? CompletedAt { get; set; }

    public bool IsTerminal =>
        Status is TranscriptionJobStatus.Done
            or TranscriptionJobStatus.Error
            or TranscriptionJobStatus.Cancelled;
}