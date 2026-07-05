using LocalScribe.Models;

namespace LocalScribe.Services;

public interface IHistoryService
{
    event EventHandler? HistoryChanged;

    IReadOnlyList<TranscriptionJob> Items { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

    Task<int> ImportLegacyHistoryIfNeededAsync(CancellationToken cancellationToken = default);

    Task AddOrUpdateAsync(TranscriptionJob job, CancellationToken cancellationToken = default);

    Task RenameAsync(string jobId, string name, CancellationToken cancellationToken = default);

    Task DeleteAsync(string jobId, CancellationToken cancellationToken = default);

    Task ClearAllAsync(CancellationToken cancellationToken = default);

    Task<TranscriptionJob?> GetByIdAsync(string jobId, CancellationToken cancellationToken = default);
}