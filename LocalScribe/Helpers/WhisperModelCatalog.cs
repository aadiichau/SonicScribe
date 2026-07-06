namespace LocalScribe.Helpers;

public static class WhisperModelCatalog
{
    public static IReadOnlyList<WhisperModelOption> All { get; } =
    [
        new("large-v3", "large-v3 — Best quality", 3_000_000_000L),
        new("medium", "medium — Faster", 1_500_000_000L),
        new("small", "small — Fastest", 500_000_000L),
        new("base", "base — Ultra fast", 150_000_000L)
    ];

    public static long GetExpectedBytes(string model) =>
        All.FirstOrDefault(option => string.Equals(option.Code, model, StringComparison.OrdinalIgnoreCase))?.ExpectedBytes
        ?? 1_000_000_000L;

    public static string FormatBytes(long bytes)
    {
        if (bytes >= 1_000_000_000)
        {
            return $"{bytes / 1_000_000_000.0:0.0} GB";
        }

        if (bytes >= 1_000_000)
        {
            return $"{bytes / 1_000_000.0:0} MB";
        }

        return $"{bytes / 1_000.0:0} KB";
    }
}

public sealed record WhisperModelOption(string Code, string DisplayName, long ExpectedBytes);