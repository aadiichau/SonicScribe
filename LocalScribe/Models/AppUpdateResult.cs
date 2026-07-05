namespace LocalScribe.Models;

public sealed class AppUpdateResult
{
    public bool Success { get; init; }

    public bool RestartScheduled { get; init; }

    public string? ErrorMessage { get; init; }
}