using LocalScribe.Core;
using LocalScribe.Core.Navigation;
using LocalScribe.Helpers;
using LocalScribe.Models;
using LocalScribe.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LocalScribe;

public partial class App : Application
{
    private Window? _window;

    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
        Services = ConfigureServices();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        CrashLog.Write("UnhandledException", e.Exception);
        e.Handled = true;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var mainWindow = Services.GetRequiredService<MainWindow>();
        _window = mainWindow;
        mainWindow.Activate();
        mainWindow.SetStartupOverlayVisible(true);

        try
        {
            var startup = Services.GetRequiredService<AppStartupService>();
            await startup.InitializeAsync();
            await MaybeRunFirstLaunchSetupAsync(mainWindow);
            await MaybePromptForUpdateAsync(mainWindow);
        }
        catch (Exception ex)
        {
            mainWindow.SetStartupOverlayVisible(false);
            await ShowStartupErrorAsync(ex);
        }
        finally
        {
            mainWindow.SetStartupOverlayVisible(false);
        }
    }

    private static async Task MaybePromptForUpdateAsync(MainWindow mainWindow)
    {
        var settingsService = Services.GetRequiredService<ISettingsService>();
        var updateService = Services.GetRequiredService<IUpdateCheckService>();
        var updatePromptService = Services.GetRequiredService<UpdatePromptService>();

        await settingsService.LoadAsync();
        var result = await updateService.CheckForUpdatesAsync();

        if (!updateService.ShouldShowUpdatePrompt(result, settingsService.Current.DismissedUpdateVersion))
        {
            return;
        }

        if (mainWindow.Content?.XamlRoot is null)
        {
            return;
        }

        var notes = string.IsNullOrWhiteSpace(result.ReleaseNotes)
            ? string.Empty
            : $"\n\n{result.ReleaseNotes}";

        var dialog = new ContentDialog
        {
            Title = "Update available",
            Content =
                $"SonicScribe v{result.LatestVersion} is available (you have v{result.CurrentVersion}).\n\n" +
                "Update now to download and install silently inside the app, then restart automatically. " +
                "Your settings and history are kept." +
                notes,
            PrimaryButtonText = "Update now",
            SecondaryButtonText = "Later",
            CloseButtonText = "Skip this version",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = mainWindow.Content.XamlRoot
        };

        var dialogResult = await dialog.ShowAsync();
        if (dialogResult == ContentDialogResult.Primary)
        {
            await updatePromptService.ApplyUpdateAsync(result);
            return;
        }

        if (dialogResult == ContentDialogResult.None && result.LatestVersion is not null)
        {
            updateService.MarkUpdatePromptDismissed(result.LatestVersion);
        }
    }

    private static async Task MaybeRunFirstLaunchSetupAsync(MainWindow mainWindow)
    {
        var settingsService = Services.GetRequiredService<ISettingsService>();
        await settingsService.LoadAsync();

        if (settingsService.Current.HasDismissedSetupPrompt)
        {
            return;
        }

        var prerequisiteSetup = Services.GetRequiredService<IPrerequisiteSetupService>();
        var report = await prerequisiteSetup.CheckAsync();
        if (report.IsTranscriptionReady)
        {
            return;
        }

        mainWindow.SetStartupOverlayVisible(false);

        var missingSummary = string.Join(
            Environment.NewLine,
            report.MissingItems
                .Where(item => item.Kind is not PrerequisiteKind.Winget)
                .Select(item => $"• {item.Name}"));

        var dialog = new ContentDialog
        {
            Title = "Set up SonicScribe automatically?",
            Content =
                "SonicScribe needs Python, faster-whisper, and PyTorch before it can transcribe audio.\n\n" +
                "Missing:\n" + missingSummary + "\n\n" +
                "SonicScribe can install everything for you (10–30 min, large download).",
            PrimaryButtonText = "Install everything",
            SecondaryButtonText = "Later",
            CloseButtonText = "Open Settings",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = mainWindow.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Secondary)
        {
            settingsService.Current.HasDismissedSetupPrompt = true;
            await settingsService.SaveAsync();
            return;
        }

        if (result == ContentDialogResult.None)
        {
            if (Services.GetService(typeof(INavigationService)) is INavigationService navigationService)
            {
                navigationService.Navigate(NavigationTag.Settings);
            }

            return;
        }

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        mainWindow.SetStartupOverlayVisible(true);
        mainWindow.SetStartupOverlayMessage("Setting up SonicScribe...", "Installing Python, faster-whisper, and PyTorch");

        try
        {
            var progress = new Progress<PrerequisiteSetupProgress>(update =>
            {
                mainWindow.SetStartupOverlayMessage(
                    update.Step,
                    update.Message);
            });

            await prerequisiteSetup.InstallMissingAsync(progress);

            if (Services.GetService(typeof(IWhisperEngineHost)) is IWhisperEngineHost whisperHost)
            {
                whisperHost.InvalidateWorker();
            }

            if (Services.GetService(typeof(IDeviceDetectionService)) is IDeviceDetectionService deviceDetection)
            {
                deviceDetection.InvalidateCache();
                await deviceDetection.DetectAsync(forceRefresh: true);
            }
        }
        catch (Exception ex)
        {
            mainWindow.SetStartupOverlayVisible(false);
            await ShowMessageAsync(mainWindow, "Setup failed", ex.Message);
        }
    }

    public static async Task ShutdownAsync()
    {
        if (Services.GetService(typeof(IWhisperEngineHost)) is IAsyncDisposable whisperHost)
        {
            await whisperHost.DisposeAsync();
        }

        if (Services is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static async Task ShowStartupErrorAsync(Exception ex)
    {
        if (Current is not App app || app._window?.Content is null)
        {
            return;
        }

        await ShowMessageAsync(app._window, "SonicScribe failed to start", ex.Message);
    }

    private static async Task ShowMessageAsync(Window window, string title, string message)
    {
        if (window.Content is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "Close",
            XamlRoot = window.Content.XamlRoot
        };

        await dialog.ShowAsync();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddLocalScribeServices();
        return services.BuildServiceProvider();
    }
}