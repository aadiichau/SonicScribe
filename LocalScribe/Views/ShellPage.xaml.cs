using LocalScribe.Core.Navigation;
using LocalScribe.Helpers;
using LocalScribe.Services;
using LocalScribe.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace LocalScribe.Views;

public sealed partial class ShellPage : Page
{
    private readonly INavigationService _navigationService;
    private readonly IFilePickerService _filePickerService;
    private readonly IJobQueueService _jobQueueService;

    public ShellViewModel ViewModel { get; }

    public ShellPage()
    {
        ViewModel = App.Services.GetRequiredService<ShellViewModel>();
        _navigationService = App.Services.GetRequiredService<INavigationService>();
        _filePickerService = App.Services.GetRequiredService<IFilePickerService>();
        _jobQueueService = App.Services.GetRequiredService<IJobQueueService>();

        InitializeComponent();

        _navigationService.Attach(ContentFrame);
        _navigationService.Navigate(NavigationTag.Transcribe);
    }

    private async void ShellPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        AppLogoImage.Source = AssetPathHelper.CreateBitmap("Assets/StoreLogo.png", decodePixelWidth: 112);

        if (NavView.MenuItems.Count > 0)
        {
            NavView.SelectedItem = NavView.MenuItems[0];
        }

        if (ContentFrame.Content is null)
        {
            _navigationService.Navigate(NavigationTag.Transcribe);
        }

        await ViewModel.InitializeAsync();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

        ViewModel.SelectedNavigationTag = tag;
        _navigationService.Navigate(tag);
    }

    private async void PaneAddFiles_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (NavView.MenuItems[0] is NavigationViewItem transcribeItem)
        {
            NavView.SelectedItem = transcribeItem;
        }

        _navigationService.Navigate(NavigationTag.Transcribe);

        var paths = await _filePickerService.PickMediaFilesAsync();
        if (paths.Count == 0)
        {
            return;
        }

        var allowed = MediaFileTypes.FilterAllowed(paths).ToList();
        if (allowed.Count == 0)
        {
            return;
        }

        await _jobQueueService.EnqueueAsync(allowed);

        if (App.Services.GetService(typeof(TranscribeViewModel)) is TranscribeViewModel transcribeViewModel)
        {
            transcribeViewModel.RefreshFromQueue();
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
    }
}