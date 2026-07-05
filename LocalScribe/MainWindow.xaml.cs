using LocalScribe.Helpers;
using LocalScribe.Services;
using LocalScribe.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace LocalScribe;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        UiDispatcher.Initialize(DispatcherQueue.GetForCurrentThread());

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        ConfigureWindowIcon();

        ConfigureWindowSize();

        var windowHandleProvider = App.Services.GetRequiredService<IWindowHandleProvider>();
        windowHandleProvider.WindowHandle = WindowNative.GetWindowHandle(this);

        var shellPage = App.Services.GetRequiredService<ShellPage>();
        RootFrame.Content = shellPage;

        Closed += OnWindowClosed;
    }

    public void SetStartupOverlayVisible(bool isVisible)
    {
        StartupOverlay.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ConfigureWindowIcon()
    {
        var iconPath = AssetPathHelper.GetAssetPath("Assets/AppIcon.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }

        var titleBarIconPath = AssetPathHelper.GetAssetPath("Assets/TitleBarIcon.png");
        if (File.Exists(titleBarIconPath))
        {
            AppTitleBar.IconSource = new ImageIconSource
            {
                ImageSource = AssetPathHelper.CreateBitmap("Assets/TitleBarIcon.png", decodePixelWidth: 128),
            };
        }
    }

    private void ConfigureWindowSize()
    {
        const int width = 1280;
        const int height = 820;

        AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
        }
    }

    private async void OnWindowClosed(object sender, WindowEventArgs args)
    {
        await App.ShutdownAsync();
    }
}