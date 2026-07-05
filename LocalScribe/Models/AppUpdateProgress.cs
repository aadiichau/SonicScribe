namespace LocalScribe.Models;

public sealed class AppUpdateProgress
{
    public string Stage { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public double? Percent { get; init; }
}