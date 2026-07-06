using LocalScribe.Helpers;
using LocalScribe.Models;
using LocalScribe.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace LocalScribe.Views;

public sealed partial class TranscribePage : Page
{
    private static readonly SolidColorBrush DragNormalBrush = new(Windows.UI.Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6));
    private static readonly SolidColorBrush DragActiveBrush = new(Windows.UI.Color.FromArgb(0xFF, 0x60, 0xA5, 0xFA));

    public TranscribeViewModel ViewModel { get; }

    public TranscribePage()
    {
        ViewModel = App.Services.GetRequiredService<TranscribeViewModel>();
        InitializeComponent();
    }

    private void Page_DragOver(object sender, DragEventArgs e) => HandleDragOver(e);

    private void DropZone_DragOver(object sender, DragEventArgs e) => HandleDragOver(e);

    private void HandleDragOver(DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Drop to queue";
        e.DragUIOverride.IsContentVisible = true;
        e.DragUIOverride.IsGlyphVisible = true;
        DropZone.BorderBrush = DragActiveBrush;
    }

    private void Page_DragLeave(object sender, RoutedEventArgs e) => ResetDragState();

    private void DropZone_DragLeave(object sender, RoutedEventArgs e) => ResetDragState();

    private void ResetDragState() => DropZone.BorderBrush = DragNormalBrush;

    private async void Page_Drop(object sender, DragEventArgs e) => await HandleDropAsync(e);

    private async void DropZone_Drop(object sender, DragEventArgs e) => await HandleDropAsync(e);

    private async Task HandleDropAsync(DragEventArgs e)
    {
        ResetDragState();

        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        var paths = items
            .OfType<StorageFile>()
            .Select(file => file.Path)
            .Where(path => MediaFileTypes.IsAllowed(path))
            .ToList();

        if (paths.Count == 0)
        {
            ViewModel.NotifyUnsupportedDrop();
            return;
        }

        await ViewModel.EnqueueFilesAsync(paths);
    }

    private async void TranscribePage_Loaded(object sender, RoutedEventArgs e)
    {
        SyncModelComboBoxSelection();
        SyncLanguageComboBoxSelection();
        await ViewModel.InitializeAsync();
    }

    private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelComboBox.SelectedItem is WhisperModelOption option)
        {
            ViewModel.SelectedModel = option.Code;
        }
    }

    private void SyncModelComboBoxSelection()
    {
        var match = ViewModel.AvailableModels.FirstOrDefault(model =>
            string.Equals(model.Code, ViewModel.SelectedModel, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            ModelComboBox.SelectedItem = match;
        }
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem is LanguageOption option)
        {
            ViewModel.SelectedLanguage = option.Code;
        }
    }

    private void SyncLanguageComboBoxSelection()
    {
        var match = ViewModel.AvailableLanguages.FirstOrDefault(language =>
            string.Equals(language.Code, ViewModel.SelectedLanguage, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            LanguageComboBox.SelectedItem = match;
        }
    }

    private async void RemoveQueueItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: QueueJobItemViewModel item })
        {
            await ViewModel.RemoveQueueItemAsync(item);
        }
    }
}