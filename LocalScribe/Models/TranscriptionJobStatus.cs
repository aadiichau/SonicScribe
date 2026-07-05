namespace LocalScribe.Models;

public enum TranscriptionJobStatus
{
    Queued,
    LoadingModel,
    Transcribing,
    Exporting,
    Paused,
    Done,
    Error,
    Cancelled
}