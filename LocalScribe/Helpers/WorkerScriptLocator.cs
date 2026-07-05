namespace LocalScribe.Helpers;

public static class WorkerScriptLocator
{
    public static string? Locate()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, "Engine", "transcribe_worker.py"),
            Path.Combine(baseDirectory, "transcribe_worker.py"),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "Engine", "transcribe_worker.py")),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "Engine", "transcribe_worker.py"))
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}