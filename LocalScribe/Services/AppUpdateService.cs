using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using LocalScribe.Helpers;
using LocalScribe.Models;
using Microsoft.Extensions.Logging;

namespace LocalScribe.Services;

public sealed class AppUpdateService : IAppUpdateService, IDisposable
{
    private readonly ILogger<AppUpdateService> _logger;
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(30)
    };

    public AppUpdateService(ILogger<AppUpdateService> logger)
    {
        _logger = logger;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SonicScribe-Updater");
    }

    public bool IsInstalledCopy
    {
        get
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath))
            {
                return false;
            }

            var directory = Path.GetDirectoryName(exePath) ?? string.Empty;
            string[] installMarkers =
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "SonicScribe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "SonicScribe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "SonicScribe")
            ];

            return installMarkers.Any(marker =>
                directory.StartsWith(marker, StringComparison.OrdinalIgnoreCase));
        }
    }

    public async Task<AppUpdateResult> DownloadAndApplyAsync(
        UpdateCheckResult update,
        IProgress<AppUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!update.IsUpdateAvailable || string.IsNullOrWhiteSpace(update.LatestVersion))
        {
            return new AppUpdateResult { ErrorMessage = "No update is available." };
        }

        var updatesFolder = Path.Combine(AppDataPathHelper.GetAppDataFolder(), "Updates");
        Directory.CreateDirectory(updatesFolder);

        try
        {
            // Always use the portable zip for in-app updates. Silent installers often fail on Windows
            // (UAC prompts, locked files) while the app is still running.
            return await ApplyPortableUpdateAsync(update, updatesFolder, progress, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new AppUpdateResult { ErrorMessage = "Update was cancelled." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "In-app update failed.");
            return new AppUpdateResult { ErrorMessage = ex.Message };
        }
    }

    private async Task<AppUpdateResult> ApplyPortableUpdateAsync(
        UpdateCheckResult update,
        string updatesFolder,
        IProgress<AppUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        var downloadUrl = update.PortableDownloadUrl;
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return new AppUpdateResult { ErrorMessage = "Portable update package was not found." };
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return new AppUpdateResult { ErrorMessage = "Could not determine the running app location." };
        }

        var targetFolder = Path.GetDirectoryName(exePath)!;
        var zipPath = Path.Combine(updatesFolder, $"SonicScribe-v{update.LatestVersion}-Portable-win-x64.zip");
        var stagingFolder = Path.Combine(updatesFolder, $"staging-v{update.LatestVersion}");

        if (Directory.Exists(stagingFolder))
        {
            Directory.Delete(stagingFolder, recursive: true);
        }

        progress?.Report(new AppUpdateProgress
        {
            Stage = "Downloading",
            Message = $"Downloading SonicScribe v{update.LatestVersion}...",
            Percent = 0
        });

        await DownloadFileAsync(downloadUrl, zipPath, progress, cancellationToken).ConfigureAwait(false);

        progress?.Report(new AppUpdateProgress
        {
            Stage = "Preparing",
            Message = "Extracting update package...",
            Percent = 95
        });

        Directory.CreateDirectory(stagingFolder);
        ZipFile.ExtractToDirectory(zipPath, stagingFolder, overwriteFiles: true);

        var scriptPath = Path.Combine(updatesFolder, "apply-portable-update.cmd");
        var needsElevation = RequiresElevationForFolder(targetFolder);
        var script = $"""
            @echo off
            taskkill /IM SonicScribe.exe /F >nul 2>&1
            ping 127.0.0.1 -n 5 >nul
            robocopy "{stagingFolder}" "{targetFolder}" /E /IS /IT /R:3 /W:2 /NFL /NDL /NJH /NJS /nc /ns /np
            if errorlevel 8 exit /b 1
            start "" "{exePath}"
            exit /b 0
            """;

        await File.WriteAllTextAsync(scriptPath, script, cancellationToken);

        progress?.Report(new AppUpdateProgress
        {
            Stage = "Installing",
            Message = "Applying update. SonicScribe will restart automatically...",
            Percent = 100
        });

        var scriptStartInfo = new ProcessStartInfo
        {
            FileName = scriptPath,
            CreateNoWindow = true,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        if (needsElevation)
        {
            scriptStartInfo.Verb = "runas";
        }

        try
        {
            Process.Start(scriptStartInfo);
        }
        catch (System.ComponentModel.Win32Exception) when (needsElevation)
        {
            return new AppUpdateResult
            {
                ErrorMessage =
                    "Windows needs administrator approval to update SonicScribe in Program Files. "
                    + "Approve the prompt, or download the installer from GitHub and run it manually."
            };
        }

        _logger.LogInformation("Launched portable update script for v{Version}", update.LatestVersion);
        return new AppUpdateResult { Success = true, RestartScheduled = true };
    }

    private async Task DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<AppUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(destinationPath);

        var buffer = new byte[1024 * 128];
        long downloaded = 0;
        int read;

        while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            downloaded += read;

            if (totalBytes is > 0)
            {
                var percent = downloaded * 100.0 / totalBytes.Value;
                progress?.Report(new AppUpdateProgress
                {
                    Stage = "Downloading",
                    Message = $"Downloading... {downloaded / (1024.0 * 1024.0):F1} / {totalBytes.Value / (1024.0 * 1024.0):F1} MB",
                    Percent = percent
                });
            }
        }
    }

    private static bool RequiresElevationForFolder(string folder)
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return folder.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase)
            || folder.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _httpClient.Dispose();
}