using CommunityToolkit.Mvvm.ComponentModel;
using LocalScribe.Models;
using Microsoft.UI.Xaml;

namespace LocalScribe.ViewModels;

public partial class QueueJobItemViewModel : ObservableObject
{
    public string JobId { get; private set; } = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private TranscriptionJobStatus _status;

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private string _statusLabel = string.Empty;

    [ObservableProperty]
    private string _logMessage = string.Empty;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isError;

    [ObservableProperty]
    private bool _canRemove;

    public void UpdateFrom(TranscriptionJob job, bool isActive)
    {
        JobId = job.JobId;
        DisplayName = job.DisplayName;
        FileName = job.FileName;
        Status = job.Status;
        Progress = job.Progress;
        LogMessage = job.LogMessage;
        IsActive = isActive;
        IsError = job.Status == TranscriptionJobStatus.Error;
        CanRemove = !isActive && job.Status is not TranscriptionJobStatus.LoadingModel
            and not TranscriptionJobStatus.Transcribing
            and not TranscriptionJobStatus.Exporting
            and not TranscriptionJobStatus.Paused;
        StatusLabel = FormatStatusLabel(job);
        NotifyVisibilityProperties();
    }

    public Visibility IsActiveVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;

    public Visibility IsErrorVisibility => IsError ? Visibility.Visible : Visibility.Collapsed;

    public Visibility IsNotErrorVisibility => IsError ? Visibility.Collapsed : Visibility.Visible;

    partial void OnIsActiveChanged(bool value) => NotifyVisibilityProperties();

    partial void OnIsErrorChanged(bool value) => NotifyVisibilityProperties();

    partial void OnCanRemoveChanged(bool value) => OnPropertyChanged(nameof(RemoveButtonVisibility));

    public Visibility RemoveButtonVisibility => CanRemove ? Visibility.Visible : Visibility.Collapsed;

    private void NotifyVisibilityProperties()
    {
        OnPropertyChanged(nameof(IsActiveVisibility));
        OnPropertyChanged(nameof(IsErrorVisibility));
        OnPropertyChanged(nameof(IsNotErrorVisibility));
        OnPropertyChanged(nameof(RemoveButtonVisibility));
    }

    private static string FormatStatusLabel(TranscriptionJob job)
        => job.Status switch
        {
            TranscriptionJobStatus.Queued => "Queued",
            TranscriptionJobStatus.LoadingModel => "Loading model",
            TranscriptionJobStatus.Transcribing => $"{job.Progress}%",
            TranscriptionJobStatus.Exporting => "Saving",
            TranscriptionJobStatus.Paused => "Paused",
            TranscriptionJobStatus.Done => "Done",
            TranscriptionJobStatus.Error => "Error",
            TranscriptionJobStatus.Cancelled => "Cancelled",
            _ => job.Status.ToString()
        };
}