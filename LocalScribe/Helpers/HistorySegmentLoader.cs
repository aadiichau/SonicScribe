using System.Text.Json;
using LocalScribe.Models;

namespace LocalScribe.Helpers;

public static class HistorySegmentLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<IReadOnlyList<TranscriptSegment>> LoadSegmentsAsync(
        TranscriptionJob job,
        string outputFolder,
        CancellationToken cancellationToken = default)
    {
        if (job.Segments.Count > 0)
        {
            return job.Segments;
        }

        var jsonPath = OutputPathResolver.ResolveJsonPath(job, outputFolder);
        if (jsonPath is null)
        {
            return Array.Empty<TranscriptSegment>();
        }

        try
        {
            await using var stream = File.OpenRead(jsonPath);
            var segments = await JsonSerializer.DeserializeAsync<List<TranscriptSegment>>(
                stream,
                JsonOptions,
                cancellationToken);

            return segments ?? [];
        }
        catch
        {
            return Array.Empty<TranscriptSegment>();
        }
    }
}