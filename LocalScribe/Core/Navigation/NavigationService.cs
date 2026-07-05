using LocalScribe.Helpers;
using LocalScribe.Views;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace LocalScribe.Core.Navigation;

public sealed class NavigationService : INavigationService
{
    private Frame? _frame;

    public string? CurrentTag { get; private set; }

    public void Attach(Frame frame)
    {
        _frame = frame;
        _frame.Navigated += OnFrameNavigated;
    }

    public bool Navigate(string tag, object? parameter = null)
    {
        if (_frame is null)
        {
            return false;
        }

        var pageType = tag switch
        {
            NavigationTag.Transcribe => typeof(TranscribePage),
            NavigationTag.History => typeof(HistoryPage),
            NavigationTag.Settings => typeof(SettingsPage),
            NavigationTag.About => typeof(AboutPage),
            _ => null
        };

        if (pageType is null)
        {
            return false;
        }

        if (_frame.Content?.GetType() == pageType)
        {
            CurrentTag = tag;
            return true;
        }

        try
        {
            var success = _frame.Navigate(
                pageType,
                parameter,
                new SuppressNavigationTransitionInfo());

            if (success)
            {
                CurrentTag = tag;
            }

            return success;
        }
        catch (Exception ex)
        {
            CrashLog.Write("Navigation", ex);
            return false;
        }
    }

    public bool GoBack()
    {
        if (_frame?.CanGoBack != true)
        {
            return false;
        }

        _frame.GoBack();
        return true;
    }

    private void OnFrameNavigated(object sender, NavigationEventArgs e)
    {
        CurrentTag = e.SourcePageType switch
        {
            var type when type == typeof(TranscribePage) => NavigationTag.Transcribe,
            var type when type == typeof(HistoryPage) => NavigationTag.History,
            var type when type == typeof(SettingsPage) => NavigationTag.Settings,
            var type when type == typeof(AboutPage) => NavigationTag.About,
            _ => CurrentTag
        };
    }
}