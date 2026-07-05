using LocalScribe.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace LocalScribe.Views;

public sealed partial class HistoryPage : Page
{
    public HistoryViewModel ViewModel { get; }

    public HistoryPage()
    {
        ViewModel = App.Services.GetRequiredService<HistoryViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.InitializeAsync();
    }

    private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryList.SelectedItem is HistoryItemViewModel selected)
        {
            ViewModel.SelectJob(selected.JobId);
        }
        else if (e.RemovedItems.Count > 0 && HistoryList.SelectedItem is null)
        {
            ViewModel.SelectJob(null);
        }
    }

    private async void ClearHistory_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (!ViewModel.CanClearHistory)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Clear all history?",
            Content = $"This removes all {ViewModel.ItemCount} entries from history. Export files on disk are not deleted.",
            PrimaryButtonText = "Clear history",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        await ViewModel.ClearHistoryAsync();
        HistoryList.SelectedItem = null;
    }

    private async void DeleteItem_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string jobId)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Delete transcription?",
            Content = "This removes the entry from history. Export files on disk are not deleted.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        await ViewModel.DeleteJobAsync(jobId);

        if (HistoryList.SelectedItem is HistoryItemViewModel selected && selected.JobId == jobId)
        {
            HistoryList.SelectedItem = null;
        }
    }

    private async void RenameButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var textBox = new TextBox
        {
            Text = ViewModel.ViewingFileName,
            PlaceholderText = "Display name",
            SelectionStart = ViewModel.ViewingFileName.Length
        };

        var dialog = new ContentDialog
        {
            Title = "Rename transcription",
            Content = textBox,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        await ViewModel.RenameSelectedAsync(textBox.Text);
    }
}