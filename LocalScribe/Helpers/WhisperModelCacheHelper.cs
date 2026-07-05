using System.Text.RegularExpressions;

namespace LocalScribe.Helpers;

public static partial class WhisperModelCacheHelper
{
    private static readonly Regex ModelPathRegex = ModelPathPattern();

    public static bool IsCorruptModelError(string? message) =>
        !string.IsNullOrWhiteSpace(message)
        && message.Contains("model.bin", StringComparison.OrdinalIgnoreCase);

    public static bool TryExtractModelDirectory(string? message, out string modelDirectory)
    {
        modelDirectory = string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var match = ModelPathRegex.Match(message);
        if (!match.Success)
        {
            return false;
        }

        modelDirectory = match.Groups["path"].Value.Trim().Trim('\'', '"');
        return Directory.Exists(modelDirectory) || Directory.Exists(Path.GetDirectoryName(modelDirectory) ?? string.Empty);
    }

    public static bool TryRepairCorruptModel(string? errorMessage, out string repairSummary)
    {
        repairSummary = string.Empty;

        if (!IsCorruptModelError(errorMessage))
        {
            repairSummary = "This error is not a corrupted Whisper model cache.";
            return false;
        }

        if (!TryExtractModelDirectory(errorMessage, out var modelDirectory))
        {
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

    private static void TryDeleteDirectory(string path, ICollection<string> deletedPaths)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        Directory.Delete(path, recursive: true);
        deletedPaths.Add(path);
    }

    [GeneratedRegex(@"model\s+'(?<path>[^']+)'", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ModelPathPattern();
}