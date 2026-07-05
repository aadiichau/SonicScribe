using Microsoft.UI.Xaml;

namespace LocalScribe.Services;

public interface IWindowHandleProvider
{
    IntPtr WindowHandle { get; set; }

    XamlRoot? XamlRoot { get; set; }
}