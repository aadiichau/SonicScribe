namespace LocalScribe.Services;

public sealed class WindowHandleProvider : IWindowHandleProvider
{
    public IntPtr WindowHandle { get; set; }
}