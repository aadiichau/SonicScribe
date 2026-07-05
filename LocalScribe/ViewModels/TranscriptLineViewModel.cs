using CommunityToolkit.Mvvm.ComponentModel;
using LocalScribe.Models;

namespace LocalScribe.ViewModels;

public partial class TranscriptLineViewModel : ObservableObject
{
    public TranscriptLineViewModel(TranscriptSegment segment)
    {
        Segment = segment;
    }

    public TranscriptSegment Segment { get; }

    [ObservableProperty]
    private bool _showTimestamps = true;

    public string Timestamp => FormatTimestamp(Segment.Start);

    public string Text => Segment.Text;

    public string DisplayText => ShowTimestamps ? $"{Timestamp}  {Text}" : Text;

    partial void OnShowTimestampsChanged(bool value) => OnPropertyChanged(nameof(DisplayText));

    private static string FormatTimestamp(double seconds)
    {
        var total = (int)Math.Floor(seconds);
        var hours = total / 3600;
        var minutes = (total % 3600) / 60;
        var secs = total % 60;
        return hours > 0
            ? $"{hours:D2}:{minutes:D2}:{secs:D2}"
            : $"{minutes:D2}:{secs:D2}";
    }
}