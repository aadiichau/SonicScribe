using LocalScribe.Core.Navigation;
using LocalScribe.Services;
using LocalScribe.ViewModels;
using LocalScribe.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LocalScribe.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLocalScribeServices(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        services.AddSingleton<IWindowHandleProvider, WindowHandleProvider>();
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<LegacyHistoryImporter>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDeviceDetectionService, DeviceDetectionService>();
        services.AddSingleton<IPrerequisiteSetupService, PrerequisiteSetupService>();
        services.AddSingleton<IUpdateCheckService, UpdateCheckService>();
        services.AddSingleton<IHistoryService, HistoryService>();
        services.AddSingleton<AppStartupService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<IWhisperEngineHost, WhisperEngineHost>();
        services.AddSingleton<ITranscriptionService, TranscriptionService>();
        services.AddSingleton<IJobQueueService, JobQueueService>();

        services.AddTransient<ShellViewModel>();
        services.AddSingleton<TranscribeViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AboutViewModel>();

        services.AddTransient<MainWindow>();
        services.AddTransient<ShellPage>();
        services.AddTransient<TranscribePage>();
        services.AddTransient<HistoryPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<AboutPage>();

        return services;
    }
}