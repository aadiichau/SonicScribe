using System.Text;
using LocalScribe.Core;
using LocalScribe.Models;

namespace LocalScribe.Helpers;

public static class AppDataPathHelper
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
    public static string GetAppDataFolder()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var newFolder = Path.Combine(appData, AppBranding.DataFolderName);
        var legacyFolder = Path.Combine(appData, AppBranding.LegacyDataFolderName);

        if (Directory.Exists(legacyFolder) && !Directory.Exists(newFolder))
        {
            try
            {
                Directory.Move(legacyFolder, newFolder);
            }
            catch
            {
                // Fall back to the new folder if migration fails.
            }
        }

        Directory.CreateDirectory(newFolder);
        return newFolder;
    }

    public static string GetDefaultOutputFolder()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(documents))
        {
            return Path.Combine(documents, AppBranding.DataFolderName, "Outputs");
        }

        return Path.Combine(GetAppDataFolder(), "Outputs");
    }

    public static string NormalizeOutputFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return GetDefaultOutputFolder();
        }

        return folder
            .Trim()
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace(
                $"{Path.DirectorySeparatorChar}{AppBranding.LegacyDataFolderName}{Path.DirectorySeparatorChar}",
                $"{Path.DirectorySeparatorChar}{AppBranding.DataFolderName}{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase);
    }

    public static string EnsureOutputFolderReady(string? configuredFolder)
    {
        var candidates = new[]
        {
            NormalizeOutputFolder(configuredFolder),
            GetDefaultOutputFolder(),
            Path.Combine(GetAppDataFolder(), "Outputs")
        };

        Exception? lastError = null;

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            try
            {
                Directory.CreateDirectory(candidate);

                var probePath = Path.Combine(candidate, $".sonicscribe-{Guid.NewGuid():N}.tmp");
                File.WriteAllText(probePath, "ok");
                File.Delete(probePath);

                return candidate;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new IOException(
            "Could not create a writable folder for transcript exports. "
            + "Check that your Documents folder exists or pick another folder in Settings.",
            lastError);
    }

    public static string SanitizeFileName(string? value, string fallback = "untitled")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            builder.Append(InvalidFileNameChars.Contains(character) ? '_' : character);
        }

        var sanitized = builder
            .ToString()
            .Trim()
            .TrimEnd('.', ' ');

        if (sanitized.Length > 120)
        {
            sanitized = sanitized[..120].TrimEnd('.', ' ');
        }

        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }
}