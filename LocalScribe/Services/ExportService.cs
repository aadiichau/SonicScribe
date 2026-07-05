using System.Text;
using System.Text.Json;
using LocalScribe.Helpers;
using LocalScribe.Models;
using Microsoft.Extensions.Logging;

namespace LocalScribe.Services;

public sealed class ExportService : IExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ISettingsService _settingsService;
    private readonly ILogger<ExportService> _logger;

    public ExportService(ISettingsService settingsService, ILogger<ExportService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<string> ExportAsync(
        TranscriptionJob job,
        ExportFormat format,
        string? outputFolder = null,
        CancellationToken cancellationToken = default)
    {
        var folder = outputFolder ?? _settingsService.Current.OutputFolder;
        Directory.CreateDirectory(folder);

        var baseName = string.IsNullOrWhiteSpace(job.DisplayName)
            ? Path.GetFileNameWithoutExtension(job.FileName)
            : job.DisplayName;

        var extension = format switch
        {
            ExportFormat.Txt => ".txt",
            ExportFormat.TimestampedTxt => "_timestamped.txt",
            ExportFormat.Srt => ".srt",
            ExportFormat.Vtt => ".vtt",
            ExportFormat.Json => ".json",
            _ => ".txt"
        };

        var suffix = format == ExportFormat.TimestampedTxt ? "_timestamped.txt" : extension;
        var fileName = format == ExportFormat.TimestampedTxt
            ? $"{job.JobId}_{baseName}{suffix}"
            : $"{job.JobId}_{baseName}{extension}";
        var outputPath = Path.Combine(folder, fileName);

        var content = format switch
        {
            ExportFormat.Txt => BuildPlainText(job.Segments),
            ExportFormat.TimestampedTxt => BuildTimestampedText(job.Segments),
            ExportFormat.Srt => BuildSrt(job.Segments),
            ExportFormat.Vtt => BuildVtt(job.Segments),
            ExportFormat.Json => JsonSerializer.Serialize(job.Segments, JsonOptions),
            _ => BuildPlainText(job.Segments)
        };

        await File.WriteAllTextAsync(outputPath, content, Encoding.UTF8, cancellationToken);
        _logger.LogInformation("Exported {Format} for job {JobId} to {Path}", format, job.JobId, outputPath);
        return outputPath;
    }

    private static string BuildPlainText(IReadOnlyList<TranscriptSegment> segments)
        => string.Join(Environment.NewLine, segments.Select(segment => segment.Text));

    private static string BuildTimestampedText(IReadOnlyList<TranscriptSegment> segments)
        => string.Join(
            Environment.NewLine,
            segments.Select(segment => $"[{TimeFormatHelper.FormatDuration(segment.Start)}]  {segment.Text}"));

    private static string BuildSrt(IReadOnlyList<TranscriptSegment> segments)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            builder.AppendLine((index + 1).ToString());
            builder.AppendLine(
                $"{TimeFormatHelper.FormatSrtTimestamp(segment.Start)} --> {TimeFormatHelper.FormatSrtTimestamp(segment.End)}");
            builder.AppendLine(segment.Text);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildVtt(IReadOnlyList<TranscriptSegment> segments)
    {
        var builder = new StringBuilder();
        builder.AppendLine("WEBVTT");
        builder.AppendLine();

        foreach (var segment in segments)
        {
            builder.AppendLine(
                $"{TimeFormatHelper.FormatVttTimestamp(segment.Start)} --> {TimeFormatHelper.FormatVttTimestamp(segment.End)}");
            builder.AppendLine(segment.Text);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }
}