using LocalScribe.Models;

namespace LocalScribe.Helpers;

public static class TranscriptFormatter
{
    public static string BuildCopyText(IReadOnlyList<TranscriptSegment> segments, bool includeTimestamps)
    {
        if (segments.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            segments.Select(segment =>
                includeTimestamps
                    ? $"[{TimeFormatHelper.FormatDuration(segment.Start)}]  {segment.Text}"
                    : segment.Text));
    }
}