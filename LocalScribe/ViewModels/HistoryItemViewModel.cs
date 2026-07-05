using CommunityToolkit.Mvvm.ComponentModel;
using LocalScribe.Helpers;
using LocalScribe.Models;

namespace LocalScribe.ViewModels;

public partial class HistoryItemViewModel : ObservableObject
{
    public string JobId { get; private set; } = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _metaLabel = string.Empty;

    [ObservableProperty]
    private string _statusLabel = "Done";

    public void UpdateFrom(TranscriptionJob job)
    {
        JobId = job.JobId;
        DisplayName = job.DisplayName;
        FileName = job.FileName;
        MetaLabel = BuildMetaLabel(job);
        StatusLabel = job.Status == TranscriptionJobStatus.Done ? "Done" : job.Status.ToString();
    }

    private static string BuildMetaLabel(TranscriptionJob job)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(job.TranscribedAt))
        {
            parts.Add(job.TranscribedAt);
        }
        else if (job.CompletedAt is not null)
        {
            parts.Add(job.CompletedAt.Value.ToString("yyyy-MM-dd HH:mm"));
        }

        if (job.AudioDurationSeconds is > 0)
        {
            parts.Add(TimeFormatHelper.FormatDuration(job.AudioDurationSeconds.Value));
        }

        if (job.SegmentCount > 0)
        {
            parts.Add($"{job.SegmentCount} segments");
        }

        return parts.Count > 0 ? string.Join(" · ", parts) : "Completed transcription";
    }
}