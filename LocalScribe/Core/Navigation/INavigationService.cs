using Microsoft.UI.Xaml.Controls;

namespace LocalScribe.Core.Navigation;

public interface INavigationService
{
    string? CurrentTag { get; }

    void Attach(Frame frame);

    bool Navigate(string tag, object? parameter = null);

    bool GoBack();
}