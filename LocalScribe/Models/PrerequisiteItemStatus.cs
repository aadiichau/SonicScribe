namespace LocalScribe.Models;

public sealed class PrerequisiteItemStatus
{
    public PrerequisiteKind Kind { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool IsReady { get; init; }

    public string Detail { get; init; } = string.Empty;
}