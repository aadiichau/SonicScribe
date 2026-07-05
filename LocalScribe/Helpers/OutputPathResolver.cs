using LocalScribe.Core;
using LocalScribe.Models;

namespace LocalScribe.Helpers;

public static class OutputPathResolver
{
    public static string? ResolveFile(string? fileName, string outputFolder)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        foreach (var folder in GetCandidateFolders(outputFolder))
        {
            var path = Path.Combine(folder, fileName);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    public static string? ResolveJsonPath(TranscriptionJob job, string outputFolder)
        => ResolveFile(job.JsonFile, outputFolder);

    public static string? ResolveTxtPath(TranscriptionJob job, string outputFolder)
        => ResolveFile(job.TxtFile, outputFolder);

    public static string? ResolveSrtPath(TranscriptionJob job, string outputFolder)
        => ResolveFile(job.SrtFile, outputFolder);

    public static string? ResolveTimestampedTxtPath(TranscriptionJob job, string outputFolder)
        => ResolveFile(job.TimestampedTxtFile, outputFolder);

    public static string? ResolveVttPath(TranscriptionJob job, string outputFolder)
    {
        var baseName = string.IsNullOrWhiteSpace(job.DisplayName)
            ? Path.GetFileNameWithoutExtension(job.FileName)
            : job.DisplayName;
        var fileName = $"{job.JobId}_{baseName}.vtt";

        return ResolveFile(fileName, outputFolder);
    }

    private static IEnumerable<string> GetCandidateFolders(string outputFolder)
    {
        var folders = new List<string>();

        if (!string.IsNullOrWhiteSpace(outputFolder))
        {
            folders.Add(outputFolder);
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        folders.Add(Path.Combine(userProfile, "transcribe_app_extracted", "transcribe_app", "outputs"));
        folders.Add(Path.Combine(userProfile, "transcribe_app", "outputs"));
        folders.Add(Path.Combine(userProfile, "Documents", "transcribe_app", "outputs"));
        folders.Add(Path.Combine(userProfile, "Desktop", "transcribe_app", "outputs"));
        folders.Add(Path.Combine(userProfile, "Documents", AppBranding.DataFolderName, "Outputs"));
        folders.Add(Path.Combine(userProfile, "Documents", AppBranding.LegacyDataFolderName, "Outputs"));

        return folders
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}