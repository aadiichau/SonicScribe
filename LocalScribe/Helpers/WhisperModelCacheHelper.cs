using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace LocalScribe.Helpers;

public static partial class WhisperModelCacheHelper
{
    private const long MinimumHealthyFractionPercent = 50;
    private static readonly Regex ModelPathRegex = ModelPathPattern();
    private static readonly Regex ModelInPathRegex = ModelInPathPattern();

    public static bool IsCorruptModelError(string? message) =>
        !string.IsNullOrWhiteSpace(message)
        && message.Contains("model.bin", StringComparison.OrdinalIgnoreCase);

    public static string GetHuggingFaceHubRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache",
            "huggingface",
            "hub");

    public static string GetRepoDirectory(string model) =>
        Path.Combine(GetHuggingFaceHubRoot(), $"models--Systran--faster-whisper-{model}");

    public static bool IsModelCacheHealthy(string model)
    {
        var repoDirectory = GetRepoDirectory(model);
        if (!Directory.Exists(repoDirectory))
        {
            return true;
        }

        var snapshotsDirectory = Path.Combine(repoDirectory, "snapshots");
        if (!Directory.Exists(snapshotsDirectory))
        {
            return false;
        }

        var expectedBytes = WhisperModelCatalog.GetExpectedBytes(model);
        var minimumBytes = Math.Max(50_000_000, expectedBytes * MinimumHealthyFractionPercent / 100);

        foreach (var snapshotDirectory in Directory.EnumerateDirectories(snapshotsDirectory))
        {
            var modelBinPath = Path.Combine(snapshotDirectory, "model.bin");
            if (!File.Exists(modelBinPath))
            {
                continue;
            }

            var size = new FileInfo(modelBinPath).Length;
            if (size >= minimumBytes)
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryClearModelCache(string model, out string summary)
    {
        summary = string.Empty;
        var repoDirectory = GetRepoDirectory(model);
        if (!Directory.Exists(repoDirectory))
        {
            summary = $"No cached files found for {model}.";
            return false;
        }

        try
        {
            Directory.Delete(repoDirectory, recursive: true);
            summary =
                $"Removed incomplete {model} model cache. The model will download again (~{WhisperModelCatalog.FormatBytes(WhisperModelCatalog.GetExpectedBytes(model))}).";
            return true;
        }
        catch (Exception ex)
        {
            summary = $"Could not remove model cache: {ex.Message}";
            return false;
        }
    }

    public static bool TryExtractModelDirectory(string? message, out string modelDirectory)
    {
        modelDirectory = string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var match = ModelInPathRegex.Match(message);
        if (!match.Success)
        {
            match = ModelPathRegex.Match(message);
        }

        if (!match.Success)
        {
            return false;
        }

        modelDirectory = match.Groups["path"].Value.Trim().Trim('\'', '"');
        return Directory.Exists(modelDirectory)
            || Directory.Exists(Path.GetDirectoryName(modelDirectory) ?? string.Empty);
    }

    public static bool TryRepairCorruptModel(string? errorMessage, out string repairSummary) =>
        TryRepairCorruptModel(errorMessage, model: null, out repairSummary);

    public static bool TryRepairCorruptModel(string? errorMessage, string? model, out string repairSummary)
    {
        repairSummary = string.Empty;

        if (!string.IsNullOrWhiteSpace(model) && TryClearModelCache(model, out repairSummary))
        {
            return true;
        }

        if (!IsCorruptModelError(errorMessage))
        {
            repairSummary = "This error is not a corrupted Whisper model cache.";
            return false;
        }

        if (!TryExtractModelDirectory(errorMessage, out var modelDirectory))
        {
            if (!string.IsNullOrWhiteSpace(model))
            {
                return TryClearModelCache(model, out repairSummary);
            }

            repairSummary = "Could not locate the corrupted model folder to repair.";
            return false;
        }

        var deletedPaths = new List<string>();
        var snapshotDirectory = modelDirectory;
        var repoDirectory = Directory.GetParent(snapshotDirectory)?.FullName;

        if (!string.IsNullOrWhiteSpace(repoDirectory)
            && repoDirectory.Contains("models--", StringComparison.OrdinalIgnoreCase))
        {
            TryDeleteDirectory(repoDirectory, deletedPaths);
        }
        else
        {
            TryDeleteDirectory(snapshotDirectory, deletedPaths);
        }

        if (deletedPaths.Count == 0)
        {
            repairSummary = "No corrupted model files were found to remove.";
            return false;
        }

        repairSummary =
            "Removed corrupted Whisper model cache. Press Start again — the model will re-download (~3 GB for large-v3).";
        return true;
    }

    public static void EnsureHealthyModelCacheOrClear(string model, ILogger? logger = null)
    {
        if (IsModelCacheHealthy(model))
        {
            return;
        }

        if (TryClearModelCache(model, out var summary))
        {
            logger?.LogWarning("Cleared unhealthy {Model} cache before transcription: {Summary}", model, summary);
        }
    }

    private static void TryDeleteDirectory(string path, ICollection<string> deletedPaths)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        Directory.Delete(path, recursive: true);
        deletedPaths.Add(path);
    }

    [GeneratedRegex(@"in model\s+'(?<path>[^']+)'", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ModelInPathPattern();

    [GeneratedRegex(@"model\s+'(?<path>[^']+)'", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ModelPathPattern();
}