using LocalScribe.Core;
using LocalScribe.Helpers;

namespace LocalScribe.Models;

public sealed class AppSettings
{
    public string DefaultModel { get; set; } = "large-v3";

    public string DefaultLanguage { get; set; } = "auto";

    public string OutputFolder { get; set; } = AppDataPathHelper.GetDefaultOutputFolder();

    public bool ImportLegacyHistoryOnFirstLaunch { get; set; } = true;

    public bool LegacyHistoryImported { get; set; }

    public bool UserClearedHistory { get; set; }

    public string? LegacyHistoryPath { get; set; }

    public string? PythonExecutablePath { get; set; }

    public bool HasDismissedSetupPrompt { get; set; }

    public DateTimeOffset? LastUpdateCheckUtc { get; set; }

    public string? DismissedUpdateVersion { get; set; }
}