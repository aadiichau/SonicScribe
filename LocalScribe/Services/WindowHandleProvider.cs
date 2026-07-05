using Microsoft.UI.Xaml;

namespace LocalScribe.Services;

public sealed class WindowHandleProvider : IWindowHandleProvider
{
    public IntPtr WindowHandle { get; set; }

    public XamlRoot? XamlRoot { get; set; }
}