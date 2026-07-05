using System.Text.Json;
using LocalScribe.Helpers;
using LocalScribe.Models;
using Microsoft.Extensions.Logging;

namespace LocalScribe.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsFilePath;

    public AppSettings Current { get; private set; } = new();

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        var folder = AppDataPathHelper.GetAppDataFolder();
        _settingsFilePath = Path.Combine(folder, "settings.json");
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsFilePath))
        {
            Current = new AppSettings();
            await SaveAsync(cancellationToken);
            return;
        }

        try
        {
            await using var stream = File.OpenRead(_settingsFilePath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken);
            Current = settings ?? new AppSettings();
            Current.OutputFolder = AppDataPathHelper.NormalizeOutputFolder(Current.OutputFolder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings from {Path}", _settingsFilePath);
            Current = new AppSettings();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);

        await using var stream = File.Create(_settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, Current, JsonOptions, cancellationToken);
    }
}