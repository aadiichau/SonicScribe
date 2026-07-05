using System.Text.Json;
using LocalScribe.Helpers;
using Microsoft.Extensions.Logging;

namespace LocalScribe.Services;

public sealed class DeviceDetectionService : IDeviceDetectionService
{
    private static readonly JsonSerializerOptions ProbeJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string ProbeScript = """
        import json
        try:
            import torch
            available = torch.cuda.is_available()
            payload = {
                "device": "cuda" if available else "cpu",
                "name": torch.cuda.get_device_name(0) if available else "CPU",
                "available": available,
            }
        except Exception as exc:
            payload = {
                "device": "cpu",
                "name": "CPU",
                "available": False,
                "error": str(exc),
            }
        print(json.dumps(payload))
        """;

    private readonly ISettingsService _settingsService;
    private readonly ILogger<DeviceDetectionService> _logger;
    private DeviceInfo? _cachedInfo;

    public DeviceDetectionService(ISettingsService settingsService, ILogger<DeviceDetectionService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public event EventHandler<DeviceInfo>? DeviceChanged;

    public void InvalidateCache() => _cachedInfo = null;

    public async Task<DeviceInfo> DetectAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (forceRefresh)
        {
            _cachedInfo = null;
        }

        if (_cachedInfo is not null)
        {
            return _cachedInfo;
        }

        var pythonPath = await PythonLocator.LocateSupportedAsync(
            _settingsService.Current.PythonExecutablePath,
            cancellationToken);

        if (pythonPath is null)
        {
            _logger.LogWarning("Python was not found. Falling back to CPU device info.");
            _cachedInfo = new DeviceInfo
            {
                DeviceType = "CPU",
                DisplayName = "Python not found",
                PythonPath = null,
                IsPythonAvailable = false
            };
            NotifyDeviceChanged(_cachedInfo);
            return _cachedInfo;
        }

        _settingsService.Current.PythonExecutablePath = pythonPath;
        await _settingsService.SaveAsync(cancellationToken);

        var tempScript = Path.Combine(Path.GetTempPath(), $"localscribe_gpu_probe_{Guid.NewGuid():N}.py");

        try
        {
            await File.WriteAllTextAsync(tempScript, ProbeScript, cancellationToken);

            var result = await ProcessRunner.RunAsync(
                pythonPath,
                $"\"{tempScript}\"",
                cancellationToken,
                timeoutMs: 20_000);

            if (result.ExitCode != 0)
            {
                _logger.LogWarning(
                    "GPU probe failed. ExitCode={ExitCode}, Error={Error}",
                    result.ExitCode,
                    result.StandardError);

                _cachedInfo = BuildCpuFallback(pythonPath, result.StandardError);
                return _cachedInfo;
            }

            var payload = JsonSerializer.Deserialize<GpuProbeResult>(result.StandardOutput, ProbeJsonOptions);
            if (payload is null)
            {
                _logger.LogWarning("GPU probe returned empty payload.");
                _cachedInfo = BuildCpuFallback(pythonPath, "Empty probe response");
                return _cachedInfo;
            }

            _cachedInfo = new DeviceInfo
            {
                DeviceType = payload.Device?.ToUpperInvariant() ?? "CPU",
                DisplayName = payload.Name ?? "CPU",
                PythonPath = pythonPath,
                IsPythonAvailable = true,
                IsCudaAvailable = payload.Available,
                ProbeError = payload.Error
            };

            _logger.LogInformation(
                "Detected device {Device} ({Name}) using Python at {PythonPath}",
                _cachedInfo.DeviceType,
                _cachedInfo.DisplayName,
                pythonPath);

            NotifyDeviceChanged(_cachedInfo);
            return _cachedInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during GPU detection.");
            _cachedInfo = BuildCpuFallback(pythonPath, ex.Message);
            return _cachedInfo;
        }
        finally
        {
            try
            {
                if (File.Exists(tempScript))
                {
                    File.Delete(tempScript);
                }
            }
            catch
            {
                // Best effort temp file cleanup.
            }
        }
    }

    private DeviceInfo BuildCpuFallback(string pythonPath, string? reason)
    {
        var info = new DeviceInfo
        {
            DeviceType = "CPU",
            DisplayName = string.IsNullOrWhiteSpace(reason) ? "CPU" : $"CPU ({reason})",
            PythonPath = pythonPath,
            IsPythonAvailable = true,
            IsCudaAvailable = false,
            ProbeError = reason
        };

        NotifyDeviceChanged(info);
        return info;
    }

    private void NotifyDeviceChanged(DeviceInfo info) => DeviceChanged?.Invoke(this, info);

    private sealed class GpuProbeResult
    {
        public string? Device { get; set; }

        public string? Name { get; set; }

        public bool Available { get; set; }

        public string? Error { get; set; }
    }
}