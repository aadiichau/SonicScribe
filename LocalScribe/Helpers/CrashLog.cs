using LocalScribe.Core;

namespace LocalScribe.Helpers;

public static class CrashLog
{
    private static readonly string LogFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppBranding.DataFolderName,
        "logs");

    public static void WriteMessage(string source, string message)
    {
        try
        {
            Directory.CreateDirectory(LogFolder);
            var path = Path.Combine(LogFolder, "crash.log");
            var entry = $"""
                [{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {source}
                {message}
                ---

                """;

            File.AppendAllText(path, entry);
        }
        catch
        {
            // Best effort crash logging.
        }
    }

    public static void Write(string source, Exception exception)
    {
        try
        {
            Directory.CreateDirectory(LogFolder);
            var path = Path.Combine(LogFolder, "crash.log");
            var entry = $"""
                [{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {source}
                {exception}
                ---

                """;

            File.AppendAllText(path, entry);
        }
        catch
        {
            // Best effort crash logging.
        }
    }
}