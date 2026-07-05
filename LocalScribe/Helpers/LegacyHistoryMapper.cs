using LocalScribe.Models;

namespace LocalScribe.Helpers;

public static class LegacyHistoryMapper
{
    public static TranscriptionJob ToTranscriptionJob(LegacyHistoryMetadataEntry entry)
        => MapEntry(entry);

    private static TranscriptionJob MapEntry(LegacyHistoryMetadataEntry entry, int segmentCountOverride = 0)
    {
        var elapsed = entry.Elapsed;
        var duration = entry.Duration;
        double? realtime = null;
        if (elapsed is > 0 && duration is > 0)
        {
            realtime = duration.Value / elapsed.Value;
        }

        return new TranscriptionJob
        {
            JobId = entry.JobId,
            FileName = entry.FileName ?? entry.Name ?? "Unknown",
            DisplayName = entry.Name ?? Path.GetFileNameWithoutExtension(entry.FileName ?? "Untitled"),
            FilePath = string.Empty,
            Status = MapStatus(entry.Status),
            Progress = entry.Progress,
            LogMessage = entry.Log,
            Model = entry.Model ?? "large-v3",
            Language = entry.Language ?? "auto",
            DetectedLanguage = entry.DetectedLanguage,
            AudioDurationSeconds = entry.Duration,
            SegmentCount = entry.SegmentCount > 0 ? entry.SegmentCount : segmentCountOverride,
            ElapsedSeconds = entry.Elapsed,
            ElapsedMinutes = entry.ElapsedMin,
            RealtimeFactor = realtime,
            Device = entry.Device ?? "?",
            GpuName = entry.GpuName ?? string.Empty,
            Segments = Array.Empty<TranscriptSegment>(),
            ErrorMessage = entry.Error,
            TxtFile = entry.TxtFile,
            SrtFile = entry.SrtFile,
            JsonFile = entry.JsonFile,
            TimestampedTxtFile = entry.TimestampedTxtFile,
            TranscribedAt = entry.TranscribedAt,
            CompletedAt = TranscriptionJobStatus.Done == MapStatus(entry.Status)
                ? DateTimeOffset.Now
                : null
        };
    }

    private static TranscriptionJobStatus MapStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "queued" => TranscriptionJobStatus.Queued,
            "loading_model" => TranscriptionJobStatus.LoadingModel,
            "transcribing" => TranscriptionJobStatus.Transcribing,
            "paused" => TranscriptionJobStatus.Paused,
            "done" => TranscriptionJobStatus.Done,
            "error" => TranscriptionJobStatus.Error,
            "cancelled" => TranscriptionJobStatus.Cancelled,
            _ => TranscriptionJobStatus.Done
        };
    }
}