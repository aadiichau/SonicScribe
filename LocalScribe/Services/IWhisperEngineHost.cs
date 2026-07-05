using LocalScribe.Models;

namespace LocalScribe.Services;

public interface IWhisperEngineHost : IAsyncDisposable
{
    Task RunTranscriptionAsync(
        TranscriptionJob job,
        IProgress<TranscriptionProgress> progress,
        CancellationToken cancellationToken = default);

    void InvalidateWorker();
}