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
    private bool _isRestoringSelection;

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

        if (ContentFrame.Content is null)
        {
            _navigationService.Navigate(NavigationTag.Transcribe);
        }

        SyncNavSelection(_navigationService.CurrentTag ?? NavigationTag.Transcribe);
        await ViewModel.InitializeAsync();
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked || _isRestoringSelection)
        {
            return;
        }

        if (args.InvokedItemContainer is NavigationViewItem { Tag: string tag })
        {
            NavigateToTag(tag);
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_isRestoringSelection)
        {
            return;
        }

        if (args.SelectedItem is NavigationViewItem { Tag: string tag })
        {
            NavigateToTag(tag);
        }
    }

    private void NavigateToTag(string tag)
    {
        ViewModel.SelectedNavigationTag = tag;

        if (_navigationService.Navigate(tag))
        {
            return;
        }

        SyncNavSelection(_navigationService.CurrentTag ?? NavigationTag.Transcribe);
    }

    private void SyncNavSelection(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>())
        {
            if (!string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _isRestoringSelection = true;
            NavView.SelectedItem = item;
            _isRestoringSelection = false;
            ViewModel.SelectedNavigationTag = tag;
            return;
        }
    }

    private async void PaneAddFiles_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        NavigateToTag(NavigationTag.Transcribe);

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