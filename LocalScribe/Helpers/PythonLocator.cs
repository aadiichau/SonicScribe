namespace LocalScribe.Helpers;

public static class PythonLocator
{
    private static readonly string[] CandidateCommands =
    [
        "python",
        "python3",
        "py"
    ];

    public static async Task<string?> LocateAsync(string? preferredPath = null, CancellationToken cancellationToken = default)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(preferredPath))
        {
            candidates.Add(preferredPath);
        }

        foreach (var command in CandidateCommands)
        {
            var resolved = await TryResolveAsync(command, cancellationToken);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                candidates.Add(resolved);
            }
        }

        candidates.AddRange(GetCommonInstallPaths());

        var distinctCandidates = candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        string? torchPython = null;
        string? anyPython = null;

        foreach (var candidate in distinctCandidates)
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            if (!await CanExecuteAsync(candidate, cancellationToken))
            {
                continue;
            }

            anyPython ??= candidate;

            if (await HasTorchAsync(candidate, cancellationToken))
            {
                torchPython = candidate;
                break;
            }
        }

        return torchPython ?? anyPython;
    }

    private static IEnumerable<string> GetCommonInstallPaths()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(localAppData, "Programs", "Python", "Python312", "python.exe");
        yield return Path.Combine(localAppData, "Programs", "Python", "Python313", "python.exe");
        yield return Path.Combine(localAppData, "Programs", "Python", "Python311", "python.exe");
    }

    private static async Task<string?> TryResolveAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            var arguments = command == "py" ? "-3 -c \"import sys; print(sys.executable)\"" : "-c \"import sys; print(sys.executable)\"";
            var result = await ProcessRunner.RunAsync(command, arguments, cancellationToken);
            if (result.ExitCode != 0)
            {
                return null;
            }

            var executable = result.StandardOutput.Trim();
            return string.IsNullOrWhiteSpace(executable) ? null : executable;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> CanExecuteAsync(string executable, CancellationToken cancellationToken)
    {
        try
        {
            var result = await ProcessRunner.RunAsync(executable, "-c \"import sys; print(sys.version)\"", cancellationToken);
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> HasTorchAsync(string executable, CancellationToken cancellationToken)
    {
        try
        {
            var result = await ProcessRunner.RunAsync(
                executable,
                "-c \"import importlib.util; print(1 if importlib.util.find_spec('torch') else 0)\"",
                cancellationToken);

            return result.ExitCode == 0 && result.StandardOutput.Trim() == "1";
        }
        catch
        {
            return false;
        }
    }
}