namespace LocalScribe.Helpers;

public static class MediaFileTypes
{
    public static readonly IReadOnlySet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".mp4", ".wav", ".m4a", ".flac", ".ogg", ".webm", ".mkv", ".avi", ".mov"
    };

    public static readonly IReadOnlyList<string> DisplayExtensions =
        AllowedExtensions.OrderBy(extension => extension).ToList();

    public static bool IsAllowed(string filePath)
        => AllowedExtensions.Contains(Path.GetExtension(filePath));

    public static IEnumerable<string> FilterAllowed(IEnumerable<string> filePaths)
        => filePaths.Where(IsAllowed);
}