using LocalScribe.Models;

namespace LocalScribe.Services;

public enum ExportFormat
{
    Txt,
    TimestampedTxt,
    Srt,
    Vtt,
    Json
}

public interface IExportService
{
    Task<string> ExportAsync(
        TranscriptionJob job,
        ExportFormat format,
        string? outputFolder = null,
        CancellationToken cancellationToken = default);
}