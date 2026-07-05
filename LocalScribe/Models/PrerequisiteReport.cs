namespace LocalScribe.Models;

public sealed class PrerequisiteReport
{
    public IReadOnlyList<PrerequisiteItemStatus> Items { get; init; } = [];

    public bool IsWingetAvailable { get; init; }

    public string? PythonPath { get; init; }

    public bool IsTranscriptionReady => Items
        .Where(item => item.Kind is PrerequisiteKind.Python or PrerequisiteKind.FasterWhisper or PrerequisiteKind.PyTorch)
        .All(item => item.IsReady);

    public bool AllReady => IsTranscriptionReady
        && Items.First(item => item.Kind == PrerequisiteKind.Ffmpeg).IsReady;

    public IReadOnlyList<PrerequisiteItemStatus> MissingItems =>
        Items.Where(item => !item.IsReady).ToList();
}