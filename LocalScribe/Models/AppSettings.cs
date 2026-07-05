using LocalScribe.Core;

namespace LocalScribe.Models;

public sealed class AppSettings
{
    public string DefaultModel { get; set; } = "large-v3";

    public string DefaultLanguage { get; set; } = "auto";

    public string OutputFolder { get; set; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            AppBranding.DataFolderName,
            "Outputs");

    public bool ImportLegacyHistoryOnFirstLaunch { get; set; } = true;

    public bool LegacyHistoryImported { get; set; }

    public string? LegacyHistoryPath { get; set; }

    public string? PythonExecutablePath { get; set; }

    public bool HasDismissedSetupPrompt { get; set; }

    public DateTimeOffset? LastUpdateCheckUtc { get; set; }

    public string? DismissedUpdateVersion { get; set; }
}