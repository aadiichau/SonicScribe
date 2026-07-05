using LocalScribe.Core;
using LocalScribe.Helpers;
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

        var dialog = new ContentDialog
        {
            Title = "SonicScribe failed to start",
            Content = $"An error occurred during startup:\n\n{ex.Message}",
            CloseButtonText = "Close",
            XamlRoot = app._window.Content.XamlRoot
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