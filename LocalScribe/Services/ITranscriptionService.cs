using LocalScribe.Models;

namespace LocalScribe.Services;

public interface ITranscriptionService
{
    Task<TranscriptionJob> TranscribeAsync(
        TranscriptionJob job,
        IProgress<TranscriptionProgress> progress,
        CancellationToken cancellationToken = default);
}

public sealed class TranscriptionProgress
{
    public TranscriptionJobStatus Status { get; init; }

    public int Progress { get; init; }

    public string LogMessage { get; init; } = string.Empty;

    public string? DetectedLanguage { get; init; }

    public double? AudioDurationSeconds { get; init; }

    public IReadOnlyList<TranscriptSegment>? Segments { get; init; }
}