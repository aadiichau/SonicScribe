using System.Text.Json;
using LocalScribe.Helpers;
using LocalScribe.Models;
using Microsoft.Extensions.Logging;

namespace LocalScribe.Services;

public sealed class HistoryService : IHistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ISettingsService _settingsService;
    private readonly LegacyHistoryImporter _legacyHistoryImporter;
    private readonly ILogger<HistoryService> _logger;
    private readonly string _historyFilePath;
    private readonly List<TranscriptionJob> _items = [];

    public HistoryService(
        ISettingsService settingsService,
        LegacyHistoryImporter legacyHistoryImporter,
        ILogger<HistoryService> logger)
    {
        _settingsService = settingsService;
        _legacyHistoryImporter = legacyHistoryImporter;
        _logger = logger;

        var folder = AppDataPathHelper.GetAppDataFolder();
        _historyFilePath = Path.Combine(folder, "history.json");
    }

    public IReadOnlyList<TranscriptionJob> Items => _items;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _items.Clear();

        if (!File.Exists(_historyFilePath))
        {
            _logger.LogDebug("No native history file found at {Path}", _historyFilePath);
            return;
        }

        try
        {
            await using var stream = File.OpenRead(_historyFilePath);
            var jobs = await JsonSerializer.DeserializeAsync<List<TranscriptionJob>>(stream, JsonOptions, cancellationToken);
            if (jobs is not null)
            {
                _items.AddRange(jobs);
                _logger.LogInformation("Loaded {Count} history entries from {Path}", _items.Count, _historyFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load history from {Path}", _historyFilePath);
        }
    }

    public async Task<int> ImportLegacyHistoryIfNeededAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.Current;

        if (!settings.ImportLegacyHistoryOnFirstLaunch || settings.LegacyHistoryImported)
        {
            return 0;
        }

        await LoadAsync(cancellationToken);
        if (_items.Count > 0)
        {
            _logger.LogInformation("Skipping legacy import because native history already contains {Count} entries.", _items.Count);
            settings.LegacyHistoryImported = true;
            await _settingsService.SaveAsync(cancellationToken);
            return 0;
        }

        foreach (var candidate in _legacyHistoryImporter.GetCandidatePaths(settings.LegacyHistoryPath))
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            var imported = await _legacyHistoryImporter.ImportAsync(candidate, cancellationToken);
            if (imported.Count == 0)
            {
                continue;
            }

            var existingIds = _items.Select(item => item.JobId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var added = 0;

            foreach (var job in imported)
            {
                if (existingIds.Add(job.JobId))
                {
                    _items.Add(job);
                    added++;
                }
            }

            if (added > 0)
            {
                await SaveAsync(cancellationToken);
                settings.LegacyHistoryPath = candidate;
                settings.LegacyHistoryImported = true;
                await _settingsService.SaveAsync(cancellationToken);
                _logger.LogInformation("Imported {Count} legacy history entries from {Path}", added, candidate);
                return added;
            }
        }

        _logger.LogInformation("No legacy history file was found to import.");
        settings.LegacyHistoryImported = true;
        await _settingsService.SaveAsync(cancellationToken);
        return 0;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_historyFilePath)!);

        var persistedItems = _items.Select(HistoryPersistenceHelper.ToPersistedCopy).ToList();
        await using var stream = File.Create(_historyFilePath);
        await JsonSerializer.SerializeAsync(stream, persistedItems, JsonOptions, cancellationToken);
        _logger.LogDebug("Saved {Count} history entries to {Path}", _items.Count, _historyFilePath);
    }

    public async Task AddOrUpdateAsync(TranscriptionJob job, CancellationToken cancellationToken = default)
    {
        var persisted = HistoryPersistenceHelper.ToPersistedCopy(job);
        var index = _items.FindIndex(item => item.JobId == job.JobId);
        if (index >= 0)
        {
            _items[index] = persisted;
            _logger.LogDebug("Updated history entry {JobId}", job.JobId);
        }
        else
        {
            _items.Insert(0, persisted);
            _logger.LogDebug("Added history entry {JobId}", job.JobId);
        }

        await SaveAsync(cancellationToken);
    }

    public async Task RenameAsync(string jobId, string name, CancellationToken cancellationToken = default)
    {
        var job = _items.FirstOrDefault(item => item.JobId == jobId);
        if (job is null)
        {
            _logger.LogWarning("Rename requested for unknown job {JobId}", jobId);
            return;
        }

        job.DisplayName = name;
        await SaveAsync(cancellationToken);
        _logger.LogInformation("Renamed job {JobId} to {Name}", jobId, name);
    }

    public async Task DeleteAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var removed = _items.RemoveAll(item => item.JobId == jobId);
        if (removed == 0)
        {
            _logger.LogWarning("Delete requested for unknown job {JobId}", jobId);
            return;
        }

        await SaveAsync(cancellationToken);
        _logger.LogInformation("Deleted job {JobId} from history", jobId);
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        if (_items.Count == 0)
        {
            return;
        }

        var count = _items.Count;
        _items.Clear();
        await SaveAsync(cancellationToken);
        _logger.LogInformation("Cleared {Count} entries from history", count);
    }

    public Task<TranscriptionJob?> GetByIdAsync(string jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var job = _items.FirstOrDefault(item => item.JobId == jobId);
        return Task.FromResult(job);
    }
}