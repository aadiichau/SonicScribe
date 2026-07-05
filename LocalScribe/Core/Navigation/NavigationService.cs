using LocalScribe.Views;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace LocalScribe.Core.Navigation;

public sealed class NavigationService : INavigationService
{
    private Frame? _frame;

    public void Attach(Frame frame)
    {
        _frame = frame;
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

        return _frame.Navigate(pageType, parameter, new EntranceNavigationTransitionInfo());
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
}