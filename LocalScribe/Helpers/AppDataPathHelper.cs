using LocalScribe.Core;
using LocalScribe.Models;

namespace LocalScribe.Helpers;

public static class AppDataPathHelper
{
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

    public static string NormalizeOutputFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return new AppSettings().OutputFolder;
        }

        return folder.Replace(
            $"{Path.DirectorySeparatorChar}{AppBranding.LegacyDataFolderName}{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}{AppBranding.DataFolderName}{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase);
    }
}