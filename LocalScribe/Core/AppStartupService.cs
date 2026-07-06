using LocalScribe.Helpers;
using LocalScribe.Services;
using Microsoft.Extensions.Logging;

namespace LocalScribe.Core;

public sealed class AppStartupService
{
    private readonly ISettingsService _settingsService;
    private readonly IHistoryService _historyService;
    private readonly IDeviceDetectionService _deviceDetectionService;
    private readonly ILogger<AppStartupService> _logger;

    public AppStartupService(
        ISettingsService settingsService,
        IHistoryService historyService,
        IDeviceDetectionService deviceDetectionService,
        ILogger<AppStartupService> logger)
    {
        _settingsService = settingsService;
        _historyService = historyService;
        _deviceDetectionService = deviceDetectionService;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting {AppName} initialization.", AppBranding.AppName);

        await _settingsService.LoadAsync(cancellationToken);
        var outputFolder = AppDataPathHelper.EnsureOutputFolderReady(_settingsService.Current.OutputFolder);
        if (!string.Equals(outputFolder, _settingsService.Current.OutputFolder, StringComparison.OrdinalIgnoreCase))
        {
            _settingsService.Current.OutputFolder = outputFolder;
            await _settingsService.SaveAsync(cancellationToken);
        }

        try
        {
            var imported = await _historyService.ImportLegacyHistoryIfNeededAsync(cancellationToken);
            if (imported > 0)
            {
                _logger.LogInformation("Imported {Count} legacy transcription records.", imported);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Legacy history import failed. Continuing without imported history.");
        }

        await _historyService.LoadAsync(cancellationToken);

        _deviceDetectionService.InvalidateCache();
        var device = await _deviceDetectionService.DetectAsync(
            forceRefresh: true,
            cancellationToken: cancellationToken);
        _logger.LogInformation(
            "Startup device profile: {DeviceType} / {DisplayName} / Python={PythonPath}",
            device.DeviceType,
            device.DisplayName,
            device.PythonPath ?? "not found");

        _logger.LogInformation("{AppName} initialization complete.", AppBranding.AppName);
    }
}