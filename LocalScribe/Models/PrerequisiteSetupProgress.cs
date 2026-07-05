namespace LocalScribe.Models;

public sealed class PrerequisiteSetupProgress
{
    public string Step { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? LogLine { get; init; }
}