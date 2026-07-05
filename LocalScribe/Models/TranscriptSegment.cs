namespace LocalScribe.Models;

public sealed class TranscriptSegment
{
    public double Start { get; init; }

    public double End { get; init; }

    public string Text { get; init; } = string.Empty;
}