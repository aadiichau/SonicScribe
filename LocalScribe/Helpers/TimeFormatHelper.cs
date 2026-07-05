namespace LocalScribe.Helpers;

public static class TimeFormatHelper
{
    public static string FormatDuration(double seconds)
    {
        var minutes = (int)(seconds / 60);
        var secs = (int)(seconds % 60);
        return $"{minutes}:{secs:D2}";
    }

    public static string FormatSrtTimestamp(double seconds)
    {
        var hours = (int)(seconds / 3600);
        var minutes = (int)((seconds % 3600) / 60);
        var secs = (int)(seconds % 60);
        var milliseconds = (int)((seconds % 1) * 1000);
        return $"{hours:D2}:{minutes:D2}:{secs:D2},{milliseconds:D3}";
    }

    public static string FormatVttTimestamp(double seconds)
    {
        var hours = (int)(seconds / 3600);
        var minutes = (int)((seconds % 3600) / 60);
        var secs = (int)(seconds % 60);
        var milliseconds = (int)((seconds % 1) * 1000);
        return $"{hours:D2}:{minutes:D2}:{secs:D2}.{milliseconds:D3}";
    }
}