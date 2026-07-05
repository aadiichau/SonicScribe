using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalScribe.Models;

public sealed class WorkerEvent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("progress")]
    public int Progress { get; set; }

    [JsonPropertyName("log")]
    public string Log { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("detected_language")]
    public string? DetectedLanguage { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    [JsonPropertyName("segment_count")]
    public int SegmentCount { get; set; }

    [JsonPropertyName("elapsed")]
    public double? Elapsed { get; set; }

    [JsonPropertyName("elapsed_min")]
    public double? ElapsedMin { get; set; }

    [JsonPropertyName("device")]
    public string? Device { get; set; }

    [JsonPropertyName("gpu_name")]
    public string? GpuName { get; set; }

    [JsonPropertyName("job_id")]
    public string? JobId { get; set; }

    [JsonPropertyName("segments")]
    public List<TranscriptSegment>? Segments { get; set; }

    public static WorkerEvent? Parse(string jsonLine)
    {
        if (string.IsNullOrWhiteSpace(jsonLine))
        {
            return null;
        }

        return JsonSerializer.Deserialize<WorkerEvent>(jsonLine, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}