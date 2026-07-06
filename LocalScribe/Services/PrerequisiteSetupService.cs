using System.Text.RegularExpressions;
using LocalScribe.Helpers;
using LocalScribe.Models;
using Microsoft.Extensions.Logging;

namespace LocalScribe.Services;

public sealed class PrerequisiteSetupService : IPrerequisiteSetupService
{
    private const string PythonWingetId = "Python.Python.3.12";
    private const string FfmpegWingetId = "Gyan.FFmpeg";
    private const string CudaPipIndex = "https://download.pytorch.org/whl/cu124";

    private readonly ISettingsService _settingsService;
    private readonly IDeviceDetectionService _deviceDetectionService;
    private readonly ILogger<PrerequisiteSetupService> _logger;

    public PrerequisiteSetupService(
        ISettingsService settingsService,
        IDeviceDetectionService deviceDetectionService,
        ILogger<PrerequisiteSetupService> logger)
    {
        _settingsService = settingsService;
        _deviceDetectionService = deviceDetectionService;
        _logger = logger;
    }

    public async Task<PrerequisiteReport> CheckAsync(CancellationToken cancellationToken = default)
    {
        var wingetAvailable = await IsWingetAvailableAsync(cancellationToken);
        var pythonPath = await ResolvePythonPathAsync(cancellationToken);
        var pythonVersion = pythonPath is null
            ? null
            : await GetPythonVersionAsync(pythonPath, cancellationToken);

        var pythonReady = pythonPath is not null && IsSupportedPythonVersion(pythonVersion);
        var fasterWhisperReady = pythonReady && await HasPythonModuleAsync(pythonPath!, "faster_whisper", cancellationToken);
        var torchReady = pythonReady && await HasPythonModuleAsync(pythonPath!, "torch", cancellationToken);
        var ffmpegReady = await IsFfmpegAvailableAsync(cancellationToken);

        var items = new List<PrerequisiteItemStatus>
        {
            new()
            {
                Kind = PrerequisiteKind.Winget,
                Name = "Windows Package Manager",
                Description = "Used to install Python and FFmpeg automatically.",
                IsReady = wingetAvailable,
                Detail = wingetAvailable ? "Available" : "Install App Installer from the Microsoft Store"
            },
            new()
            {
                Kind = PrerequisiteKind.Python,
                Name = "Python 3.11 or 3.12",
                Description = "Runs the Whisper transcription engine.",
                IsReady = pythonReady,
                Detail = pythonReady
                    ? $"{pythonPath} ({pythonVersion})"
                    : pythonPath is null
                        ? "Not found"
                        : $"Found {pythonVersion} at {pythonPath}, but 3.11/3.12 is required"
            },
            new()
            {
                Kind = PrerequisiteKind.FasterWhisper,
                Name = "faster-whisper",
                Description = "Local speech-to-text library.",
                IsReady = fasterWhisperReady,
                Detail = fasterWhisperReady ? "Installed" : "Not installed"
            },
            new()
            {
                Kind = PrerequisiteKind.PyTorch,
                Name = "PyTorch",
                Description = "Machine learning runtime (GPU or CPU).",
                IsReady = torchReady,
                Detail = torchReady ? "Installed" : "Not installed"
            },
            new()
            {
                Kind = PrerequisiteKind.Ffmpeg,
                Name = "FFmpeg",
                Description = "Recommended for video files (MP4, MKV, etc.).",
                IsReady = ffmpegReady,
                Detail = ffmpegReady ? "Available on PATH" : "Not found (optional but recommended)"
            }
        };

        return new PrerequisiteReport
        {
            Items = items,
            IsWingetAvailable = wingetAvailable,
            PythonPath = pythonPath
        };
    }

    public async Task<PrerequisiteReport> InstallMissingAsync(
        IProgress<PrerequisiteSetupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        void Report(string step, string message, string? logLine = null, int? percent = null, bool isIndeterminate = false) =>
            progress?.Report(new PrerequisiteSetupProgress
            {
                Step = step,
                Message = message,
                LogLine = logLine,
                Percent = percent,
                IsIndeterminate = isIndeterminate
            });

        var report = await CheckAsync(cancellationToken);
        if (report.IsTranscriptionReady && report.Items.First(item => item.Kind == PrerequisiteKind.Ffmpeg).IsReady)
        {
            Report("Done", "Everything is already installed.");
            return report;
        }

        var pythonItem = report.Items.First(item => item.Kind == PrerequisiteKind.Python);
        var ffmpegItem = report.Items.First(item => item.Kind == PrerequisiteKind.Ffmpeg);
        var needsWinget = !pythonItem.IsReady || !ffmpegItem.IsReady;

        if (needsWinget && !report.IsWingetAvailable)
        {
            throw new InvalidOperationException(
                "Windows Package Manager (winget) is required for automatic setup. " +
                "Install App Installer from the Microsoft Store, then try again.");
        }

        if (!pythonItem.IsReady)
        {
            Report("Python", "Installing Python 3.12 (this may take a few minutes)...", percent: 5, isIndeterminate: true);
            await InstallWithWingetAsync(PythonWingetId, Report, cancellationToken, basePercent: 5, spanPercent: 10);
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }

        var pythonPath = await WaitForSupportedPythonAsync(Report, cancellationToken);
        _settingsService.Current.PythonExecutablePath = pythonPath;
        await _settingsService.SaveAsync(cancellationToken);
        Report("Python", $"Using Python at {pythonPath}", percent: 18);

        Report("Packages", "Preparing pip...", percent: 20, isIndeterminate: true);
        await EnsurePipReadyAsync(pythonPath, Report, cancellationToken);

        report = await CheckAsync(cancellationToken);
        if (!report.Items.First(item => item.Kind == PrerequisiteKind.FasterWhisper).IsReady)
        {
            Report("Packages", "Installing faster-whisper...", percent: 25, isIndeterminate: true);
            await RunPythonPipAsync(
                pythonPath,
                "-m pip install faster-whisper",
                Report,
                cancellationToken,
                timeoutMs: 600_000,
                basePercent: 25,
                spanPercent: 15);
        }

        report = await CheckAsync(cancellationToken);
        if (!report.Items.First(item => item.Kind == PrerequisiteKind.PyTorch).IsReady)
        {
            var useCuda = await HasNvidiaGpuAsync(cancellationToken);
            if (useCuda)
            {
                Report("Packages", "Installing PyTorch with NVIDIA GPU support (large download)...", percent: 42, isIndeterminate: true);
                try
                {
                    await RunPythonPipAsync(
                        pythonPath,
                        $"-m pip install torch torchvision torchaudio --index-url {CudaPipIndex}",
                        Report,
                        cancellationToken,
                        timeoutMs: 1_800_000,
                        basePercent: 42,
                        spanPercent: 38);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "CUDA PyTorch install failed. Falling back to CPU build.");
                    Report("Packages", "GPU install failed. Installing CPU PyTorch instead...", percent: 42, isIndeterminate: true);
                    await RunPythonPipAsync(
                        pythonPath,
                        "-m pip install torch torchvision torchaudio",
                        Report,
                        cancellationToken,
                        timeoutMs: 1_800_000,
                        basePercent: 42,
                        spanPercent: 38);
                }
            }
            else
            {
                Report("Packages", "Installing PyTorch for CPU (large download)...", percent: 42, isIndeterminate: true);
                await RunPythonPipAsync(
                    pythonPath,
                    "-m pip install torch torchvision torchaudio",
                    Report,
                    cancellationToken,
                    timeoutMs: 1_800_000,
                    basePercent: 42,
                    spanPercent: 38);
            }
        }

        report = await CheckAsync(cancellationToken);
        if (!report.Items.First(item => item.Kind == PrerequisiteKind.Ffmpeg).IsReady)
        {
            try
            {
                Report("FFmpeg", "Installing FFmpeg for video support...", percent: 85, isIndeterminate: true);
                await InstallWithWingetAsync(FfmpegWingetId, Report, cancellationToken, basePercent: 85, spanPercent: 10);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FFmpeg install failed. Continuing without FFmpeg.");
                Report("FFmpeg", "FFmpeg install failed (optional). Video files may not work until FFmpeg is installed.", percent: 90);
            }
        }

        _deviceDetectionService.InvalidateCache();
        await _deviceDetectionService.DetectAsync(forceRefresh: true, cancellationToken: cancellationToken);

        var final = await CheckAsync(cancellationToken);
        if (!final.IsTranscriptionReady)
        {
            var missing = string.Join(
                ", ",
                final.MissingItems
                    .Where(item => item.Kind is PrerequisiteKind.Python or PrerequisiteKind.FasterWhisper or PrerequisiteKind.PyTorch)
                    .Select(item => item.Name));

            throw new InvalidOperationException(
                $"Automatic setup could not finish. Still missing: {missing}. Try again or check Settings.");
        }

        Report("Done", "Setup complete! You can start transcribing.", percent: 100);
        return final;
    }

    private async Task<string> WaitForSupportedPythonAsync(
        Action<string, string, string?, int?, bool> report,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 12; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pythonPath = await ResolvePythonPathAsync(cancellationToken);
            if (pythonPath is not null)
            {
                return pythonPath;
            }

            var waitPercent = 8 + (int)Math.Round(attempt / 11.0 * 8);
            report("Python", $"Waiting for Python to finish installing ({attempt + 1}/12)...", null, waitPercent, true);
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }

        throw new InvalidOperationException(
            "Python 3.11 or 3.12 was not found after install. Restart SonicScribe and tap Install everything again.");
    }

    private async Task<string?> ResolvePythonPathAsync(CancellationToken cancellationToken)
    {
        return await PythonLocator.LocateSupportedAsync(
            _settingsService.Current.PythonExecutablePath,
            cancellationToken);
    }

    private static async Task EnsurePipReadyAsync(
        string pythonPath,
        Action<string, string, string?, int?, bool> report,
        CancellationToken cancellationToken)
    {
        var pipCheck = await ProcessRunner.RunAsync(
            pythonPath,
            "-m pip --version",
            cancellationToken,
            timeoutMs: 30_000);

        if (pipCheck.ExitCode != 0)
        {
            report("Packages", "Bootstrapping pip...", null, 20, true);
            await RunPythonPipAsync(
                pythonPath,
                "-m ensurepip --upgrade",
                report,
                cancellationToken,
                timeoutMs: 120_000,
                basePercent: 20,
                spanPercent: 3);
        }

        await RunPythonPipAsync(
            pythonPath,
            "-m pip install --upgrade pip setuptools wheel",
            report,
            cancellationToken,
            timeoutMs: 300_000,
            basePercent: 22,
            spanPercent: 3);
    }

    private static bool IsSupportedPythonVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var parts = version.Split('.');
        if (parts.Length < 2 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
        {
            return false;
        }

        return major == 3 && minor is 11 or 12;
    }

    private static async Task<bool> IsWingetAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await ProcessRunner.RunAsync("winget", "--version", cancellationToken, timeoutMs: 10_000);
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string?> GetPythonVersionAsync(string pythonPath, CancellationToken cancellationToken)
    {
        try
        {
            var result = await ProcessRunner.RunAsync(
                pythonPath,
                "-c \"import sys; print(f'{sys.version_info.major}.{sys.version_info.minor}')\"",
                cancellationToken,
                timeoutMs: 15_000);

            return result.ExitCode == 0 ? result.StandardOutput.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> HasPythonModuleAsync(
        string pythonPath,
        string moduleName,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await ProcessRunner.RunAsync(
                pythonPath,
                $"-c \"import importlib.util; print(1 if importlib.util.find_spec('{moduleName}') else 0)\"",
                cancellationToken,
                timeoutMs: 30_000);

            return result.ExitCode == 0 && result.StandardOutput.Trim() == "1";
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsFfmpegAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await ProcessRunner.RunAsync("ffmpeg", "-version", cancellationToken, timeoutMs: 10_000);
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> HasNvidiaGpuAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await ProcessRunner.RunAsync(
                "nvidia-smi",
                "--query-gpu=name --format=csv,noheader",
                cancellationToken,
                timeoutMs: 10_000);

            return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsWingetSuccess(int exitCode, string output)
    {
        if (exitCode == 0)
        {
            return true;
        }

        return output.Contains("already installed", StringComparison.OrdinalIgnoreCase)
            || output.Contains("No available upgrade found", StringComparison.OrdinalIgnoreCase)
            || output.Contains("Found an existing package", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task InstallWithWingetAsync(
        string packageId,
        Action<string, string, string?, int?, bool> report,
        CancellationToken cancellationToken,
        int basePercent = 0,
        int spanPercent = 10)
    {
        var arguments =
            $"install -e --id {packageId} --scope user --accept-package-agreements --accept-source-agreements --disable-interactivity";

        var result = await ProcessRunner.RunWithLiveOutputAsync(
            "winget",
            arguments,
            line => ReportInstallLine(report, "Install", line, basePercent, spanPercent),
            line => ReportInstallLine(report, "Install", line, basePercent, spanPercent),
            cancellationToken,
            timeoutMs: 900_000);

        var combinedOutput = $"{result.StandardOutput}\n{result.StandardError}";
        if (!IsWingetSuccess(result.ExitCode, combinedOutput))
        {
            throw new InvalidOperationException($"Failed to install {packageId} via winget: {combinedOutput}");
        }
    }

    private static async Task RunPythonPipAsync(
        string pythonPath,
        string arguments,
        Action<string, string, string?, int?, bool> report,
        CancellationToken cancellationToken,
        int timeoutMs,
        int basePercent = 0,
        int spanPercent = 10)
    {
        var result = await ProcessRunner.RunWithLiveOutputAsync(
            pythonPath,
            arguments,
            line => ReportInstallLine(report, "Packages", line, basePercent, spanPercent),
            line => ReportInstallLine(report, "Packages", line, basePercent, spanPercent),
            cancellationToken,
            timeoutMs);

        if (result.ExitCode != 0)
        {
            var details = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput
                : result.StandardError;
            throw new InvalidOperationException($"pip failed ({arguments}): {details}");
        }

        report("Packages", "Package install finished.", null, basePercent + spanPercent, false);
    }

    private static void ReportInstallLine(
        Action<string, string, string?, int?, bool> report,
        string step,
        string line,
        int basePercent,
        int spanPercent)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var trimmed = line.Trim();
        var percent = TryParseDownloadPercent(trimmed);
        if (percent is >= 0 and <= 100)
        {
            var mapped = basePercent + (int)Math.Round(spanPercent * (percent.Value / 100.0));
            report(step, trimmed, trimmed, mapped, false);
            return;
        }

        report(step, trimmed, trimmed, basePercent, true);
    }

    private static int? TryParseDownloadPercent(string line)
    {
        var percentMatch = Regex.Match(line, @"(\d{1,3})%");
        if (percentMatch.Success && int.TryParse(percentMatch.Groups[1].Value, out var directPercent))
        {
            return directPercent;
        }

        var mbMatch = Regex.Match(line, @"(\d+(?:\.\d+)?)\s*/\s*(\d+(?:\.\d+)?)\s*MB", RegexOptions.IgnoreCase);
        if (mbMatch.Success
            && double.TryParse(mbMatch.Groups[1].Value, out var downloadedMb)
            && double.TryParse(mbMatch.Groups[2].Value, out var totalMb)
            && totalMb > 0)
        {
            return (int)Math.Clamp(Math.Round(downloadedMb / totalMb * 100.0), 0, 100);
        }

        return null;
    }
}