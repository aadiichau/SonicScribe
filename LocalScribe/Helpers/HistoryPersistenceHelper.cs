using LocalScribe.Models;

namespace LocalScribe.Helpers;

public static class HistoryPersistenceHelper
{
    public static TranscriptionJob ToPersistedCopy(TranscriptionJob job)
        => new()
        {
            JobId = job.JobId,
            FileName = job.FileName,
            DisplayName = job.DisplayName,
            FilePath = job.FilePath,
            Status = job.Status,
            Progress = job.Progress,
            LogMessage = job.LogMessage,
            Model = job.Model,
            Language = job.Language,
            DetectedLanguage = job.DetectedLanguage,
            AudioDurationSeconds = job.AudioDurationSeconds,
            SegmentCount = job.SegmentCount,
            ElapsedSeconds = job.ElapsedSeconds,
            ElapsedMinutes = job.ElapsedMinutes,
            RealtimeFactor = job.RealtimeFactor,
            Device = job.Device,
            GpuName = job.GpuName,
            Segments = Array.Empty<TranscriptSegment>(),
            ErrorMessage = job.ErrorMessage,
            TxtFile = job.TxtFile,
            SrtFile = job.SrtFile,
            JsonFile = job.JsonFile,
            TimestampedTxtFile = job.TimestampedTxtFile,
            TranscribedAt = job.TranscribedAt,
            CreatedAt = job.CreatedAt,
            CompletedAt = job.CompletedAt
        };
}