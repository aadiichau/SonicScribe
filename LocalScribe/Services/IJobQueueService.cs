using LocalScribe.Models;

namespace LocalScribe.Services;

public interface IJobQueueService
{
    IReadOnlyList<TranscriptionJob> Queue { get; }

    TranscriptionJob? ActiveJob { get; }

    bool IsProcessing { get; }

    bool IsPaused { get; }

    event EventHandler? QueueChanged;

    event EventHandler<TranscriptionJob>? JobUpdated;

    Task EnqueueAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default);

    Task StartAsync(CancellationToken cancellationToken = default);

    Task PauseAsync(CancellationToken cancellationToken = default);

    Task CancelActiveAsync(CancellationToken cancellationToken = default);

    Task ClearQueueAsync(CancellationToken cancellationToken = default);

    Task<bool> RemoveJobAsync(string jobId, CancellationToken cancellationToken = default);

    Task RemoveCompletedJobsAsync(CancellationToken cancellationToken = default);

    void SyncQueuedJobsFromSettings();
}