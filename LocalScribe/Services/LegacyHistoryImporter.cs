using System.Text.Json;
using LocalScribe.Helpers;
using LocalScribe.Models;
using Microsoft.Extensions.Logging;

namespace LocalScribe.Services;

public sealed class LegacyHistoryImporter
{
    private static readonly JsonSerializerOptions LegacyJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<LegacyHistoryImporter> _logger;

    public LegacyHistoryImporter(ILogger<LegacyHistoryImporter> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<string> GetCandidatePaths(string? configuredPath)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            candidates.Add(configuredPath);
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        candidates.Add(Path.Combine(userProfile, "transcribe_app", "history.json"));
        candidates.Add(Path.Combine(userProfile, "transcribe_app_extracted", "transcribe_app", "history.json"));
        candidates.Add(Path.Combine(userProfile, "Documents", "transcribe_app", "history.json"));
        candidates.Add(Path.Combine(userProfile, "Desktop", "transcribe_app", "history.json"));

        return candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<TranscriptionJob>> ImportAsync(
        string legacyHistoryPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(legacyHistoryPath))
        {
            _logger.LogWarning("Legacy history file not found at {Path}", legacyHistoryPath);
            return Array.Empty<TranscriptionJob>();
        }

        _logger.LogInformation("Importing legacy history from {Path}", legacyHistoryPath);

        await using var stream = File.OpenRead(legacyHistoryPath);
        var entries = await JsonSerializer.DeserializeAsync<List<LegacyHistoryMetadataEntry>>(
            stream,
            LegacyJsonOptions,
            cancellationToken);

        if (entries is null || entries.Count == 0)
        {
            _logger.LogInformation("Legacy history file contained no entries.");
            return Array.Empty<TranscriptionJob>();
        }

        var jobs = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.JobId))
            .Select(LegacyHistoryMapper.ToTranscriptionJob)
            .ToList();

        _logger.LogInformation("Mapped {Count} legacy history entries.", jobs.Count);
        return jobs;
    }
}