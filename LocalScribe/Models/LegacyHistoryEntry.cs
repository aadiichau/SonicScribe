using System.Text.Json.Serialization;

namespace LocalScribe.Models;

/// <summary>
/// DTO matching the snake_case JSON format produced by the Flask web app.
/// </summary>
public sealed class LegacyHistoryEntry
{
    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "done";

    [JsonPropertyName("progress")]
    public int Progress { get; set; }

    [JsonPropertyName("log")]
    public string Log { get; set; } = string.Empty;

    [JsonPropertyName("detected_language")]
    public string? DetectedLanguage { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    [JsonPropertyName("segment_count")]
    public int SegmentCount { get; set; }

    [JsonPropertyName("txt_file")]
    public string? TxtFile { get; set; }

    [JsonPropertyName("srt_file")]
    public string? SrtFile { get; set; }

    [JsonPropertyName("json_file")]
    public string? JsonFile { get; set; }

    [JsonPropertyName("ts_file")]
    public string? TimestampedTxtFile { get; set; }

    [JsonPropertyName("segments")]
    public List<LegacyTranscriptSegment> Segments { get; set; } = [];

    [JsonPropertyName("elapsed")]
    public double? Elapsed { get; set; }

    [JsonPropertyName("elapsed_min")]
    public double? ElapsedMin { get; set; }

    [JsonPropertyName("filename")]
    public string? FileName { get; set; }

    [JsonPropertyName("device")]
    public string? Device { get; set; }

    [JsonPropertyName("gpu_name")]
    public string? GpuName { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("transcribed_at")]
    public string? TranscribedAt { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class LegacyTranscriptSegment
{
    [JsonPropertyName("start")]
    public double Start { get; set; }

    [JsonPropertyName("end")]
    public double End { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}